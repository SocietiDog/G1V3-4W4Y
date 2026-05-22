using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gw2Giveaway.Services;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace Gw2Giveaway
{
    public class TwitchChat
    {
        public string Channel { get; set; } = "";
        public string BotName { get; set; } = "";
        public string OAuth { get; set; } = "";

        public EntryMode EntryMode { get; set; } = EntryMode.Command;
        public string EntryCommand { get; set; } = "!enter";

        private TwitchClient? _client;

        public event Action? OnConnected;
        public event Action<string>? OnConnectionError;
        public event Action? OnJoinedChannel;
        public event Action<string, string>? OnMessageReceived;

        private readonly HashSet<string> _seenUsers = new();

        public void ClearSeenUsers() => _seenUsers.Clear();

        public async Task<bool> ConnectAsync()
        {
            if (string.IsNullOrWhiteSpace(Channel) || string.IsNullOrWhiteSpace(OAuth))
            {
                OnConnectionError?.Invoke("Missing Channel or OAuth token");
                return false;
            }

            _client = new TwitchClient();

            // Subscribe with async Task handlers
            _client.OnConnected += Client_OnConnected;
            _client.OnJoinedChannel += Client_OnJoinedChannel;
            _client.OnConnectionError += Client_OnConnectionError;
            _client.OnMessageReceived += Client_OnMessageReceived;

            string oauth = OAuth.StartsWith("oauth:") ? OAuth : "oauth:" + OAuth;
            string botUser = string.IsNullOrWhiteSpace(BotName) ? Channel : BotName;
            var credentials = new ConnectionCredentials(botUser, oauth);

            _client.Initialize(credentials, Channel);

            try
            {
                return await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TwitchChat.ConnectAsync", ex);
                OnConnectionError?.Invoke($"Connect failed: {ex.Message}");
                return false;
            }
        }

        private async Task Client_OnConnected(object? sender, OnConnectedEventArgs e)
        {
            OnConnected?.Invoke();
            // No await needed here unless you add async work
            await Task.CompletedTask;
        }

        private async Task Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            OnJoinedChannel?.Invoke();
            await Task.CompletedTask;
        }

        private async Task Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
        {
            if (e.Error != null)
                AppLogger.LogError("TwitchChat.OnConnectionError", new Exception(e.Error.Message ?? "Unknown Twitch client error"));
            OnConnectionError?.Invoke(e.Error.Message ?? "Unknown error");
            await Task.CompletedTask;
        }

        private async Task Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            string username = e.ChatMessage.Username.ToLowerInvariant();
            string message = e.ChatMessage.Message.Trim();
            OnMessageReceived?.Invoke(username, message);
            await Task.CompletedTask;
        }

        public async Task DisconnectAsync()
        {
            try
            {
                await SendMessageAsync("G1V3 - 4W4Y bot going offline. Thanks for playing! 👋");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TwitchChat.DisconnectAsync.SendMessage", ex);
            }

            if (_client != null)
            {
                await _client.DisconnectAsync();

                // Unsubscribe to prevent leaks
                _client.OnConnected -= Client_OnConnected;
                _client.OnJoinedChannel -= Client_OnJoinedChannel;
                _client.OnConnectionError -= Client_OnConnectionError;
                _client.OnMessageReceived -= Client_OnMessageReceived;
            }

            _client = null;
        }

        public async Task SendMessageAsync(string message)
        {
            if (_client?.IsConnected == true)
            {
                await _client.SendMessageAsync(Channel, message);
            }
        }
    }
}