using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Gw2Giveaway.Helpers;
using Gw2Giveaway.Services;

namespace Gw2Giveaway
{
    public class LeaderboardEntry
    {
        public int Rank { get; set; }
        public string Username { get; set; } = string.Empty;
        public long Iq { get; set; }
    }

    public class TriviaViewModel : INotifyPropertyChanged
    {
        private readonly TwitchChat _twitch;
        private readonly TriviaService _trivia = new();
        private readonly DatabaseService _db = new();

        private bool _triviaActive = false;
        private bool _isConnected = false;
        private bool _triviaEnabled = false;
        private bool _triviaPaused = false;

        private string? _currentCorrectLetter;
        private string? _currentCorrectFull;
        private DateTime _questionEndTime;
        private DispatcherTimer? _answerTimer;
        private DateTime _lastViewerTriviaStart = DateTime.MinValue;

        private readonly HashSet<string> _correctAnswerers = new();
        private bool _firstCorrectAwarded = false;

        public ObservableCollection<LeaderboardEntry> TopLeaderboard { get; } = new();

        private bool _leaderboardVisible = false;
        public bool LeaderboardVisible
        {
            get => _leaderboardVisible;
            set => SetField(ref _leaderboardVisible, value);
        }

        public bool IsTriviaPaused
        {
            get => _triviaPaused;
            set
            {
                if (SetField(ref _triviaPaused, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    if (value && _triviaActive) StopTriviaRound();
                }
            }
        }

        public bool IsTriviaEnabled
        {
            get => _triviaEnabled;
            set
            {
                if (SetField(ref _triviaEnabled, value))
                {
                    if (value)
                    {
                        SendBotMessage("I am online and ready for Guild Wars 2 trivia! Type !trivia to start a round.");
                        _twitch.OnMessageReceived += HandleChatMessage;
                    }
                    else
                    {
                        if (_triviaActive) StopTriviaRound();
                        SendBotMessage("I am going offline. Thanks for playing! 👋");
                        _twitch.OnMessageReceived -= HandleChatMessage;
                    }
                    OnPropertyChanged(nameof(TriviaStatus));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string TriviaStatus => IsTriviaEnabled ? "Online" : "Offline";
        public string ConnectionStatus => _isConnected ? "Connected" : "Disconnected";

        // Overlay / reward properties (defaults only — loaded/saved by MainWindow via JSON)
        private bool _autoOpenOverlay = false;
        public bool AutoOpenOverlay
        {
            get => _autoOpenOverlay;
            set => SetField(ref _autoOpenOverlay, value);
        }

        private string _overlayBackground = "#DD000000";
        public string OverlayBackground
        {
            get => _overlayBackground;
            set => SetField(ref _overlayBackground, value);
        }

        private string _overlayInnerBackground = "#EE000000";
        public string OverlayInnerBackground
        {
            get => _overlayInnerBackground;
            set => SetField(ref _overlayInnerBackground, value);
        }

        private string _overlayBorderColor = "#FFD700";
        public string OverlayBorderColor
        {
            get => _overlayBorderColor;
            set => SetField(ref _overlayBorderColor, value);
        }

        private string _titleColor = "#FFD700";
        public string TitleColor
        {
            get => _titleColor;
            set => SetField(ref _titleColor, value);
        }

        private string _headerColor = "#FFCC00";
        public string HeaderColor
        {
            get => _headerColor;
            set => SetField(ref _headerColor, value);
        }

        private string _textColor = "#FFFFFF";
        public string TextColor
        {
            get => _textColor;
            set => SetField(ref _textColor, value);
        }

        private string _accentColor = "#00FFAA";
        public string AccentColor
        {
            get => _accentColor;
            set => SetField(ref _accentColor, value);
        }

        private string _goldColor = "#FFFF00";
        public string GoldColor
        {
            get => _goldColor;
            set => SetField(ref _goldColor, value);
        }

        private int _firstCorrectReward = 150;
        public int FirstCorrectReward
        {
            get => _firstCorrectReward;
            set => SetField(ref _firstCorrectReward, value, () => OnPropertyChanged(nameof(RewardsText)));
        }

        private int _laterCorrectReward = 50;
        public int LaterCorrectReward
        {
            get => _laterCorrectReward;
            set => SetField(ref _laterCorrectReward, value, () => OnPropertyChanged(nameof(RewardsText)));
        }

        public string RewardsText => $"First: +{FirstCorrectReward} IQ • Correct: +{LaterCorrectReward} IQ";

        private string _currentQuestion = "";
        public string CurrentQuestion
        {
            get => _currentQuestion;
            private set => SetField(ref _currentQuestion, value);
        }

        private string _timeLeft = "";
        public string TimeLeft
        {
            get => _timeLeft;
            set => SetField(ref _timeLeft, value);
        }

        public List<string> Categories { get; } = new() { "Random", "Legendaries", "Ranger Pets", "Crafting", "Dyes", "Skills", "Elites", "Maps", "Lore" };

        private string _selectedCategory = "Random";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set => SetField(ref _selectedCategory, value);
        }

        private int _answerTimeSeconds = 20;
        public int AnswerTimeSeconds
        {
            get => _answerTimeSeconds;
            set => SetField(ref _answerTimeSeconds, value);
        }

        public ICommand StartTriviaCommand { get; }
        public ICommand StopTriviaCommand { get; }

        public TriviaViewModel(TwitchChat twitch)
        {
            _twitch = twitch;

            StartTriviaCommand = new RelayCommand(() => StartTriviaRound(), () => IsTriviaEnabled && !_triviaActive && !IsTriviaPaused);
            StopTriviaCommand = new RelayCommand(() => StopTriviaRound(), () => _triviaActive);

            _twitch.OnJoinedChannel += () =>
            {
                _isConnected = true;
                OnPropertyChanged(nameof(ConnectionStatus));
                CommandManager.InvalidateRequerySuggested();
                _ = UpdateLeaderboardAsync();
            };

            _twitch.OnConnectionError += _ =>
            {
                _isConnected = false;
                OnPropertyChanged(nameof(ConnectionStatus));
                CommandManager.InvalidateRequerySuggested();
            };

            _ = Task.Run(async () =>
            {
                await _trivia.EnsureMapsDataLoaded();
                await _trivia.EnsureItemIdsLoaded();
                await _trivia.EnsureColorsLoaded();
            });
        }

        private void SendBotMessage(string message)
        {
            _twitch.SendMessageAsync($"🤖Trivia-Tron: {message}");
        }

        public async void StartTriviaRound()
        {
            if (_triviaActive || IsTriviaPaused) return;

            try
            {
                _triviaActive = true;
                LeaderboardVisible = false;
                _correctAnswerers.Clear();
                _firstCorrectAwarded = false;
                CommandManager.InvalidateRequerySuggested();

                var questionData = await _trivia.GenerateQuestion(SelectedCategory);

                string overlayQuestion = $"[Category: {questionData.Category}] {questionData.QuestionText}\n\n";
                for (int i = 0; i < 4; i++) overlayQuestion += $"{(char)('A' + i)}) {questionData.Options[i]}\n";

                string chatQuestion = $"[Category: {questionData.Category}] {questionData.QuestionText} ";
                for (int i = 0; i < 4; i++) chatQuestion += $"({(char)('A' + i)}) {questionData.Options[i]} " + (i < 3 ? "| " : "");

                CurrentQuestion = overlayQuestion;
                _currentCorrectLetter = questionData.CorrectLetter;
                _currentCorrectFull = questionData.CorrectFull;
                _questionEndTime = DateTime.UtcNow.AddSeconds(AnswerTimeSeconds);
                TimeLeft = $"{AnswerTimeSeconds}s";

                _answerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _answerTimer.Tick += (s, e) =>
                {
                    var remaining = _questionEndTime - DateTime.UtcNow;
                    TimeLeft = remaining.TotalSeconds <= 0 ? "Time's up!" : $"{(int)remaining.TotalSeconds}s";
                };
                _answerTimer.Start();

                SendBotMessage($"TRIVIA TIME! {chatQuestion} Answer with !answer A/B/C/D ({AnswerTimeSeconds}s!)");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(AnswerTimeSeconds * 1000 + 1000);
                    if (!_triviaActive) return;

                    string summary = _correctAnswerers.Any()
                        ? $"Round over! {_correctAnswerers.Count} got it right. Correct: ({_currentCorrectLetter}) {_currentCorrectFull}"
                        : $"Time's up! No one got it. Correct: ({_currentCorrectLetter}) {_currentCorrectFull}";

                    SendBotMessage(summary);

                    await UpdateLeaderboardAsync();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentQuestion = string.Empty;
                        TimeLeft = "";
                        _triviaActive = false;
                        LeaderboardVisible = true;
                        CommandManager.InvalidateRequerySuggested();

                        // Hide leaderboard after 20 seconds
                        Task.Delay(20000).ContinueWith(_ => Application.Current.Dispatcher.Invoke(() => LeaderboardVisible = false));
                    });
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TriviaViewModel.StartTriviaRound", ex);

                _answerTimer?.Stop();
                CurrentQuestion = string.Empty;
                TimeLeft = string.Empty;
                _triviaActive = false;
                CommandManager.InvalidateRequerySuggested();

                SendBotMessage("Trivia-Tron couldn't start a round right now. Please try again in a moment.");
            }
        }

        private void StopTriviaRound()
        {
            if (!_triviaActive) return;
            _triviaActive = false;
            _answerTimer?.Stop();
            CurrentQuestion = string.Empty;
            TimeLeft = "";
            LeaderboardVisible = true;
            CommandManager.InvalidateRequerySuggested();
            SendBotMessage("Trivia round stopped by streamer.");

            Task.Delay(20000).ContinueWith(_ => Application.Current.Dispatcher.Invoke(() => LeaderboardVisible = false));
        }

        private async void HandleChatMessage(string username, string message, bool isSubscriber)
        {
            if (!IsTriviaEnabled) return;
            if (IsTriviaPaused) return;  // giveaway is running — ignore ALL trivia commands

            username = username.ToLowerInvariant();
            string msgLower = message.Trim().ToLowerInvariant();

            // Viewer starts trivia
            if (msgLower == "!trivia" && !_triviaActive && !IsTriviaPaused)
            {
                TimeSpan cooldown = TimeSpan.FromSeconds(60);
                if (DateTime.UtcNow - _lastViewerTriviaStart < cooldown)
                {
                    int secs = (int)(cooldown - (DateTime.UtcNow - _lastViewerTriviaStart)).TotalSeconds;
                    SendBotMessage($"@{username}, please wait {secs}s before starting another round!");
                    return;
                }

                _lastViewerTriviaStart = DateTime.UtcNow;
                SendBotMessage($"@{username} started a trivia round!");
                StartTriviaRound();
                return;
            }

            // !iq / !qr / !gold (legacy aliases)
            if (msgLower == "!iq" || msgLower == "!qr" || msgLower == "!gold")
            {
                long iq = await _db.GetIqAsync(username);
                SendBotMessage($"@{username} has {iq} IQ.");
                return;
            }

            // !leaderboard / !top
            if (msgLower == "!leaderboard" || msgLower == "!top")
            {
                await UpdateLeaderboardAsync();
                if (TopLeaderboard.Count == 0)
                {
                    SendBotMessage("Leaderboard is empty!");
                }
                else
                {
                    string lines = string.Join(" | ", TopLeaderboard.Select(e => $"{e.Rank}. {e.Username} — {e.Iq} IQ"));
                    SendBotMessage($"Top 5: {lines}");
                }
                return;
            }

            // !answer / !a / !ik ("I Know") — all submit an answer
            if (_triviaActive && (msgLower.StartsWith("!answer ") || msgLower.StartsWith("!a ") || msgLower.StartsWith("!ik ")))
            {
                int cmdLen = msgLower.StartsWith("!answer ") ? "!answer ".Length
                           : msgLower.StartsWith("!ik ")     ? "!ik ".Length
                           : "!a ".Length;
                string answer = msgLower[cmdLen..].Trim().ToUpperInvariant();
                if (answer.Length == 1 && "ABCD".Contains(answer) && answer == _currentCorrectLetter && !_correctAnswerers.Contains(username))
                {
                    _correctAnswerers.Add(username);

                    int reward = _firstCorrectAwarded ? LaterCorrectReward : FirstCorrectReward;
                    string bonus = _firstCorrectAwarded ? "" : " (FIRST!)";

                    await _db.AddIqAsync(username, reward);
                    await UpdateLeaderboardAsync();

                    SendBotMessage($"@{username} got it right{bonus}! +{reward} IQ");

                    if (!_firstCorrectAwarded) _firstCorrectAwarded = true;
                }
            }
        }

        private async Task UpdateLeaderboardAsync()
        {
            var top = await _db.GetLeaderboardAsync(5);
            Application.Current.Dispatcher.Invoke(() =>
            {
                TopLeaderboard.Clear();
                for (int i = 0; i < top.Count; i++)
                {
                    TopLeaderboard.Add(new LeaderboardEntry
                    {
                        Rank = i + 1,
                        Username = top[i].Username,
                        Iq = top[i].Iq
                    });
                }
            });
        }

        

        private bool SetField<T>(ref T field, T value, Action? extra = null, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            extra?.Invoke();
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}