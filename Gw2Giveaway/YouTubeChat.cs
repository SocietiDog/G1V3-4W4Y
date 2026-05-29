using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Gw2Giveaway
{
    /// <summary>
    /// [BETA] Reads YouTube live-chat by embedding a hidden WebView2 and injecting
    /// a MutationObserver into the live_chat page.
    ///
    /// Limitations:
    ///  - Read-only. Posting back to YouTube chat requires full OAuth and is not supported here.
    ///  - Requires the user to be logged in to Google on the same Windows profile (WebView2 shares
    ///    the default Edge user-data profile, so an existing Google login usually works).
    ///  - YouTube may change its chat DOM at any time — treat as best-effort / beta.
    ///  - FollowersOnly mode does NOT apply to YouTube viewers (no subscriber equivalent check API).
    ///  - isMember = YouTube channel membership badge (closest equivalent to Twitch subscriber).
    /// </summary>
    public sealed class YouTubeChat : IDisposable
    {
        // ── Public event surface – matches TwitchChat ─────────────────────────────
        /// <summary>Fires when a new chat message is received. Args: username, message, isMember.</summary>
        public event Action<string, string, bool>? OnMessageReceived;

        /// <summary>Fires with a human-readable status string (connection, errors, ready).</summary>
        public event Action<string>? OnStatusChanged;

        // ── Private state ─────────────────────────────────────────────────────────
        private WebView2? _webView;
        private Grid? _hostGrid;
        private bool _observerInjected;
        private bool _disposed;

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static string ExtractVideoId(string input)
        {
            input = input.Trim();
            // Bare 11-char alphanumeric ID
            if (!input.Contains('/') && !input.Contains('=') && input.Length >= 11)
                return input.Length > 11 ? input[..11] : input;

            var m = Regex.Match(input, @"(?:v=|youtu\.be/)([A-Za-z0-9_\-]{11})");
            return m.Success ? m.Groups[1].Value : input;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        /// <summary>Starts connecting to YouTube live chat for the given video ID or URL.</summary>
        public async Task ConnectAsync(string videoIdOrUrl)
        {
            if (_disposed) return;

            string videoId = ExtractVideoId(videoIdOrUrl);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                RaiseStatus("⚠ YouTube: invalid video ID or URL");
                return;
            }

            // WebView2 must be created and hosted on the UI thread
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // Find a suitable host window
                var host = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()
                           ?? Application.Current.Windows.OfType<Window>().FirstOrDefault();
                if (host == null) { RaiseStatus("⚠ YouTube: no host window available"); return; }

                _webView = new WebView2
                {
                    Visibility = Visibility.Collapsed,
                    Width = 1,
                    Height = 1
                };

                // We need the WebView2 to be part of the visual tree.
                // Wrap or inject a tiny hidden grid into the host window.
                _hostGrid = new Grid
                {
                    Width = 1,
                    Height = 1,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Visibility = Visibility.Collapsed
                };
                _hostGrid.Children.Add(_webView);

                // Attach to the existing root panel/grid of the host window
                if (host.Content is Panel panel)
                {
                    panel.Children.Add(_hostGrid);
                }
                else if (host.Content is UIElement existing)
                {
                    // Wrap in a Grid so we can add our hidden element alongside
                    var wrapper = new Grid();
                    host.Content = wrapper;
                    wrapper.Children.Add(existing);
                    wrapper.Children.Add(_hostGrid);
                }

                try
                {
                    var env = await CoreWebView2Environment.CreateAsync();
                    await _webView.EnsureCoreWebView2Async(env);

                    _webView.CoreWebView2.WebMessageReceived += OnWebMessage;
                    _webView.CoreWebView2.DOMContentLoaded   += OnDomContentLoaded;

                    string chatUrl =
                        $"https://www.youtube.com/live_chat?v={videoId}&embed_domain=www.youtube.com";
                    _webView.Source = new Uri(chatUrl);

                    RaiseStatus($"🔴 YouTube Beta: connecting to {videoId}…");
                }
                catch (Exception ex)
                {
                    RaiseStatus($"⚠ YouTube: WebView2 init failed – {ex.Message}");
                }
            });
        }

        /// <summary>Disconnects and navigates the hidden WebView2 to a blank page.</summary>
        public void Disconnect()
        {
            if (_webView == null) return;
            _observerInjected = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                try { _webView.CoreWebView2?.Navigate("about:blank"); } catch { }
            });
            RaiseStatus("YouTube: disconnected");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
            Application.Current.Dispatcher.Invoke(() =>
            {
                _webView?.Dispose();
                _webView = null;
                // Remove the hidden host grid from whatever parent it was added to
                if (_hostGrid?.Parent is Panel p)
                    p.Children.Remove(_hostGrid);
                _hostGrid = null;
            });
        }

        // ── WebView2 event handlers ───────────────────────────────────────────────
        private async void OnDomContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            if (_observerInjected || _webView == null) return;
            _observerInjected = true;

            // MutationObserver injected into the live chat page.
            // Posts each new message back as a JSON string via window.chrome.webview.postMessage.
            const string script = """
                (function () {
                    'use strict';
                    const seen = new Set();

                    function tryAttach() {
                        const root = document.querySelector('yt-live-chat-app') ||
                                     document.getElementById('chat') ||
                                     document.body;
                        if (!root) { setTimeout(tryAttach, 800); return; }

                        const obs = new MutationObserver(mutations => {
                            for (const mut of mutations) {
                                for (const node of mut.addedNodes) {
                                    if (!(node instanceof Element)) continue;
                                    const candidates = [
                                        node,
                                        ...node.querySelectorAll(
                                            'yt-live-chat-text-message-renderer,' +
                                            'yt-live-chat-paid-message-renderer')
                                    ];
                                    for (const item of candidates) {
                                        const id = item.getAttribute('id') ||
                                                   item.getAttribute('data-id') ||
                                                   null;
                                        if (id) {
                                            if (seen.has(id)) continue;
                                            seen.add(id);
                                        }
                                        const authorEl = item.querySelector('#author-name');
                                        const msgEl    = item.querySelector('#message');
                                        const badge    = item.querySelector(
                                            'yt-live-chat-author-badge-renderer');

                                        const username = authorEl ? authorEl.innerText.trim() : '';
                                        const message  = msgEl    ? msgEl.innerText.trim()    : '';
                                        const isMember = !!badge;

                                        if (username && message) {
                                            window.chrome.webview.postMessage(JSON.stringify({
                                                type: 'chat', username, message, isMember
                                            }));
                                        }
                                    }
                                }
                            }
                        });

                        obs.observe(root, { childList: true, subtree: true });
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'status', text: 'observer_ready'
                        }));
                    }

                    tryAttach();
                })();
                """;

            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                RaiseStatus($"⚠ YouTube: script injection failed – {ex.Message}");
            }
        }

        private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string raw = e.TryGetWebMessageAsString();
                var json = JObject.Parse(raw);
                string type = json["type"]?.ToString() ?? string.Empty;

                if (type == "status")
                {
                    if (json["text"]?.ToString() == "observer_ready")
                        RaiseStatus("✅ YouTube Beta: live chat connected!");
                }
                else if (type == "chat")
                {
                    string username = json["username"]?.ToString() ?? string.Empty;
                    string message  = json["message"]?.ToString()  ?? string.Empty;
                    bool isMember   = json["isMember"]?.Value<bool>() ?? false;

                    if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(message))
                        OnMessageReceived?.Invoke(username, message, isMember);
                }
            }
            catch
            {
                // Malformed or unexpected message – ignore silently
            }
        }

        private void RaiseStatus(string message) =>
            OnStatusChanged?.Invoke(message);
    }
}
