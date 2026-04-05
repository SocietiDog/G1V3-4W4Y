using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;

namespace Gw2Giveaway.Services
{
    public class TwitchEventSub
    {
        private readonly EventSubWebsocketClient _client = new();
        private readonly TwitchAPI _api = new();

        private string _broadcasterId = "";
        private string _oauthToken = "";
        private string _clientId = "";

        public event Action<string, string, string, string, string>? OnRewardRedeemed;

        public TwitchEventSub()
        {
            _client.WebsocketConnected += OnWebsocketConnected;
            _client.WebsocketDisconnected += OnWebsocketDisconnected;
            _client.WebsocketReconnected += OnWebsocketReconnected;

            _client.ChannelPointsCustomRewardRedemptionAdd += OnRewardRedeemedHandler;
        }

        private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
        {
            AppLogger.LogInfo("TwitchEventSub", "EventSub websocket connected. Creating subscription.");

            var condition = new Dictionary<string, string>
            {
                { "broadcaster_user_id", _broadcasterId }
            };

            try
            {
                if (string.IsNullOrWhiteSpace(_clientId))
                    throw new InvalidOperationException("Twitch Client ID is required for EventSub subscriptions.");

                await _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.channel_points_custom_reward_redemption.add",
                     "1",
                     condition,
                    EventSubTransportMethod.Websocket,
                     _client.SessionId!
                );

                AppLogger.LogInfo("TwitchEventSub", "EventSub subscription created.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TwitchEventSub.CreateSubscription", ex);
            }
        }

        private async Task OnWebsocketDisconnected(object? sender, WebsocketDisconnectedArgs e)
        {
            AppLogger.LogInfo("TwitchEventSub", "EventSub websocket disconnected.");
            await Task.CompletedTask;
        }

        private async Task OnWebsocketReconnected(object? sender, WebsocketReconnectedArgs e)
        {
            AppLogger.LogInfo("TwitchEventSub", "EventSub websocket reconnected.");
            await Task.CompletedTask;
        }

        private async Task OnRewardRedeemedHandler(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            var ev = e.Payload.Event;

            string username = ev.UserName ?? ev.UserLogin;
            string title = ev.Reward.Title;
            string rewardId = ev.Reward.Id;
            string input = ev.UserInput ?? "";
            string redemptionId = ev.Id;

            OnRewardRedeemed?.Invoke(username, title, rewardId, input, redemptionId);
        }

        public async Task ConnectAsync(string oauthToken, string broadcasterId, string? clientId = null)
        {
            _oauthToken = oauthToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase)
                ? oauthToken["oauth:".Length..]
                : oauthToken;
            _broadcasterId = broadcasterId;
            _clientId = clientId ?? string.Empty;

            _api.Settings.AccessToken = _oauthToken;
            _api.Settings.ClientId = _clientId;

            try
            {
                await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TwitchEventSub.ConnectAsync", ex);
            }
        }

        public async Task DisconnectAsync()
        {
            await _client.DisconnectAsync();
        }
    }
}