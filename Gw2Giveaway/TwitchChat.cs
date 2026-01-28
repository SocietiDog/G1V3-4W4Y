// Updated TwitchChat.cs – removed OnDisconnected subscription to avoid version-specific signature mismatch
// (We don't need any action on disconnect anyway – manual control)
using System;
using System.Collections.Generic;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Gw2Giveaway
{
    public class TwitchChat
    {
        public string Channel { get; set; } = "";
        public string BotName { get; set; } = "";
        public string OAuth { get; set; } = "";
        public EntryMode EntryMode { get; set; } = EntryMode.Command;
        public string EntryCommand { get; set; } = "!enter";
        public event Action<string, string>? OnMessageReceived;

        private TwitchClient? _client;
        private WebSocketClient? _webSocketClient;

        public event Action? OnConnected;
        public event Action<string>? OnConnectionError;
        public event Action? OnJoinedChannel;
        public event Action<string>? NewEntrant;

        public HashSet<string> _seenUsers = new();

        public TwitchChat()
        {
        }
        public void ClearSeenUsers()
        {
            _seenUsers.Clear();
        }
        private void CreateClient()
        {
            var clientOptions = new ClientOptions
            {
                ReconnectionPolicy = null
            };

            _webSocketClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(_webSocketClient);

            _client.OnConnected += Client_OnConnected;
            _client.OnJoinedChannel += Client_OnJoinedChannel;
            // REMOVED OnDisconnected – not needed and causes signature mismatch in some TwitchLib versions
            _client.OnConnectionError += Client_OnConnectionError;
            _client.OnMessageReceived += Client_OnMessageReceived;
            _client.OnIncorrectLogin += Client_OnIncorrectLogin;
        }

        private void DisposeClient()
        {
            if (_client != null)
            {
                _client.OnConnected -= Client_OnConnected;
                _client.OnJoinedChannel -= Client_OnJoinedChannel;
                // No OnDisconnected
                _client.OnConnectionError -= Client_OnConnectionError;
                _client.OnMessageReceived -= Client_OnMessageReceived;
                _client.OnIncorrectLogin -= Client_OnIncorrectLogin;

                try { _client.SendRaw("QUIT :Goodbye"); } catch { }

                _client = null;
            }
            _webSocketClient = null;
        }

        public void Connect()
        {
            DisposeClient();

            if (string.IsNullOrWhiteSpace(Channel) || string.IsNullOrWhiteSpace(OAuth))
            {
                OnConnectionError?.Invoke("Missing channel or OAuth");
                return;
            }

            CreateClient();

            var credentials = new ConnectionCredentials(Channel.Trim().ToLowerInvariant(), OAuth.Trim());
            _client.Initialize(credentials, Channel.Trim().ToLowerInvariant());

            try
            {
                _client.Connect();
            }
            catch (Exception ex)
            {
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public void Disconnect()
        {
            try
            {
                SendMessage("Giveaway bot going offline. Thanks for playing! 👋");
            }
            catch { }

            DisposeClient();
        }

        public void SendMessage(string message)
        {
            if (_client == null || string.IsNullOrEmpty(Channel)) return;

            try
            {
                _client.SendMessage(Channel, message);
            }
            catch { }
        }

        private void Client_OnConnected(object? sender, OnConnectedArgs e)
        {
            OnConnected?.Invoke();
        }

        private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            OnJoinedChannel?.Invoke();
            SendMessage("Giveaway bot online! Type !enter to join the giveaway.");
        }

        private void Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
        {
            OnConnectionError?.Invoke(e.Error?.Message ?? "Unknown error");
        }

        private void Client_OnIncorrectLogin(object? sender, OnIncorrectLoginArgs e)
        {
            OnConnectionError?.Invoke("Incorrect login – regenerate OAuth at twitchapps.com/tmi/");
        }
        private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            string username = e.ChatMessage.Username;
            string message = e.ChatMessage.Message.Trim();
            OnMessageReceived?.Invoke(username, message);
        }
        /*private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            string username = e.ChatMessage.Username.ToLowerInvariant();
            string message = e.ChatMessage.Message.Trim();

            if (EntryMode == EntryMode.AllChatters)
            {
                if (!_seenUsers.Contains(username))
                {
                    _seenUsers.Add(username);
                    NewEntrant?.Invoke(username);
                }
                return;
            }

            if (EntryMode == EntryMode.Command && message.Equals(EntryCommand, StringComparison.OrdinalIgnoreCase))
            {
                NewEntrant?.Invoke(username);
            }
        }*/
    }
}