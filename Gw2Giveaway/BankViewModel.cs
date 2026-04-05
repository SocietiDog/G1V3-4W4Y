using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Win32;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Gw2Giveaway.Helpers;

namespace Gw2Giveaway
{
    public class SlotViewModel : INotifyPropertyChanged
    {
        private const string CustomPrizePlaceholderImage = "pack://application:,,,/Images/Gold_coin.png";

        public PrizeSlot Slot { get; }
        public ICommand EditCommand { get; }          // Select GW2 item
        public ICommand CustomPrizeCommand { get; }   // Set custom prize
        public ICommand ClearCommand { get; }
        public ICommand SetDisplayStackCommand { get; }
        public ICommand SetGiveawayAmountCommand { get; }
        public string Rarity => Slot.Item?.Rarity ?? "";
        public string Description => Slot.Item?.Description ?? "";
        public bool IsGw2Item => Slot.Item != null;
        public ICommand SetPrizeRarityCommand { get; }

        private bool _showPrizeRarityBadges = true;
        public bool ShowPrizeRarityBadges
        {
            get => _showPrizeRarityBadges;
            set
            {
                if (_showPrizeRarityBadges != value)
                {
                    _showPrizeRarityBadges = value;
                    OnPropertyChanged();
                }
            }
        }

        private readonly Action _save;
        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged();
                }
            }
        }
        public SlotViewModel(PrizeSlot slot, Action save, bool showRarityBadges = true)
        {
            Slot = slot;
            _save = save;
            ShowPrizeRarityBadges = showRarityBadges;

            EditCommand = new RelayCommand(EditGw2Item);
            CustomPrizeCommand = new RelayCommand(SetCustomPrize);
            ClearCommand = new RelayCommand(Clear);
            SetDisplayStackCommand = new RelayCommand(SetDisplayStack);
            SetGiveawayAmountCommand = new RelayCommand(SetGiveawayAmount);
            SetPrizeRarityCommand = new RelayCommand<PrizeRarity>(r => PrizeRarity = r);
        }

        private void EditGw2Item()
        {
            var dialog = new ItemSelectorDialog();
            if (dialog.ShowDialog() != true || dialog.SelectedItem == null) return;

            // Override any custom prize
            Slot.CustomName = null;
            Slot.CustomIconUrl = null;

            Slot.Item = dialog.SelectedItem;
            Slot.ItemId = dialog.SelectedId;

            if (Slot.DisplayStack == 0) Slot.DisplayStack = 1;
            if (Slot.GiveawayAmount <= 0) Slot.GiveawayAmount = 1; // reasonable default

            RaisePropertyChanges();
            _save();
        }

        private void SetCustomPrize()
        {
            var nameDlg = new InputDialog("Custom prize name (e.g. '500 Gems' or 'Steam DLC Key'):", Slot.CustomName ?? "");
            if (nameDlg.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDlg.Result)) return;

            string name = nameDlg.Result.Trim();
            string? url = null;

            var choiceDialog = new ChoiceDialog(
                "Custom Prize Image",
                "Choose icon source:\nYes = Local file\nNo = Online URL\nCancel = No image",
                "Local File",
                "Online URL",
                "No Image");
            choiceDialog.ShowDialog();
            var imageSourceChoice = choiceDialog.Result;

            if (imageSourceChoice == MessageBoxResult.Yes)
            {
                var picker = new OpenFileDialog
                {
                    Title = "Choose custom prize image",
                    Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (picker.ShowDialog() == true)
                {
                    url = new Uri(picker.FileName, UriKind.Absolute).AbsoluteUri;
                }
            }
            else if (imageSourceChoice == MessageBoxResult.No)
            {
                var urlDlg = new InputDialog("Paste image URL (optional, http/https):", Slot.CustomIconUrl ?? "");
                if (urlDlg.ShowDialog() != true)
                    return;

                url = NormalizeWebImageUrl(urlDlg.Result);
                if (!string.IsNullOrWhiteSpace(urlDlg.Result) && string.IsNullOrWhiteSpace(url))
                {
                    new InfoDialog("Invalid URL", "Please use a valid http/https image URL.").ShowDialog();
                    return;
                }
            }

            // Override any GW2 item
            Slot.Item = null;
            Slot.ItemId = 0;

            Slot.CustomName = name;
            Slot.CustomIconUrl = url;

            if (Slot.DisplayStack == 0) Slot.DisplayStack = 1;
            if (Slot.GiveawayAmount <= 0) Slot.GiveawayAmount = 1;

            RaisePropertyChanges();
            _save();
        }

        private static string? NormalizeWebImageUrl(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            string value = input.Trim();
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return null;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            return uri.AbsoluteUri;
        }
        public PrizeRarity PrizeRarity
        {
            get => Slot.PrizeRarity;
            set
            {
                Slot.PrizeRarity = value;
                OnPropertyChanged();
                _save();
            }
        }
        private void SetDisplayStack()
        {
            var dlg = new InputDialog("Display stack size (1-250):", Slot.DisplayStack.ToString());
            if (dlg.ShowDialog() == true && int.TryParse(dlg.Result, out int d) && d >= 1 && d <= 250)
            {
                Slot.DisplayStack = d;
                OnPropertyChanged(nameof(StackText));
                _save();
            }
        }

        private void SetGiveawayAmount()
        {
            var dlg = new InputDialog("Giveaway amount per win (0-250, 0 = disable):", Slot.GiveawayAmount.ToString());
            if (dlg.ShowDialog() == true && int.TryParse(dlg.Result, out int g) && g >= 0 && g <= 250)
            {
                Slot.GiveawayAmount = g;
                _save();
            }
        }

        private void Clear()
        {
            Slot.Item = null;
            Slot.ItemId = 0;
            Slot.CustomName = null;
            Slot.CustomIconUrl = null;
            Slot.DisplayStack = 0;
            Slot.GiveawayAmount = 1;

            RaisePropertyChanges();
            _save();
        }

        private void RaisePropertyChanges()
        {
            OnPropertyChanged(nameof(ImageSource));
            OnPropertyChanged(nameof(StackText));
            OnPropertyChanged(nameof(ItemName));
            OnPropertyChanged(nameof(HasPrize));
        }

        public string ImageSource
            => Slot.CustomIconUrl
               ?? Slot.Item?.Icon
               ?? (!string.IsNullOrWhiteSpace(Slot.CustomName) ? CustomPrizePlaceholderImage : "");
        public string StackText => Slot.DisplayStack > 1 ? Slot.DisplayStack.ToString() : "";
        public string ItemName => Slot.CustomName ?? Slot.Item?.Name ?? "Empty Slot";

        public bool HasPrize => Slot.Item != null || !string.IsNullOrEmpty(Slot.CustomName);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class BankViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SlotViewModel> Slots { get; } = new();

        private bool _showPrizeRarityBadges = true;
        public bool ShowPrizeRarityBadges
        {
            get => _showPrizeRarityBadges;
            set
            {
                if (_showPrizeRarityBadges != value)
                {
                    _showPrizeRarityBadges = value;
                    OnPropertyChanged();
                }
            }
        }

        private long _goldAmount;
        public long GoldAmount
        {
            get => _goldAmount;
            set
            {
                if (_goldAmount != value)
                {
                    _goldAmount = value;
                    _bank.GoldAmount = value;  // ← Critical fix: sync back to the model
                    OnPropertyChanged();
                    _save();
                }
            }
        }

        private readonly PrizeBank _bank;
        private readonly Action _save;

        public BankViewModel(PrizeBank bank, Action save, bool showRarityBadges = true)
        {
            _bank = bank;
            _save = save;
            ShowPrizeRarityBadges = showRarityBadges;
            _goldAmount = bank.GoldAmount;

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    Slots.Add(new SlotViewModel(bank.Slots[r, c], _save, showRarityBadges));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    }