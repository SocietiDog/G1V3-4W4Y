using System.IO;
using System.Net.Sockets;

namespace Gw2Giveaway
{
    public class TwitchChat
    {
        public string Channel { get; set; } = "";
        public string BotName { get; set; } = "";
        public string OAuth { get; set; } = "";
        public string EntryCommand { get; set; } = "!enter";

        public event Action<string>? NewEntrant;

        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        private readonly HashSet<string> _entrantLower = new();

        public void Connect()
        {
            _client = new TcpClient("irc.twitch.tv", 6667);
            _reader = new StreamReader(_client.GetStream());
            _writer = new StreamWriter(_client.GetStream());

            _writer.WriteLine("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");
            _writer.WriteLine($"PASS {OAuth}");
            _writer.WriteLine($"NICK {BotName}");
            _writer.WriteLine($"JOIN #{Channel}");
            _writer.Flush();

            new Thread(ReadLoop) { IsBackground = true }.Start();
        }

        private void ReadLoop()
        {
            while (_reader != null)
            {
                string? line = _reader.ReadLine();
                if (line == null) break;

                if (line.StartsWith("PING"))
                {
                    _writer?.WriteLine("PONG :tmi.twitch.tv");
                    _writer?.Flush();
                    continue;
                }

                if (line.Contains("PRIVMSG"))
                {
                    // Parse display-name from tags if available
                    string displayName = ParseUser(line).ToLower(); // fallback to login name
                    if (line.StartsWith("@"))
                    {
                        string tags = line.Substring(1, line.IndexOf(' ') - 1);
                        foreach (string tag in tags.Split(';'))
                        {
                            var parts = tag.Split('=');
                            if (parts[0] == "display-name" && !string.IsNullOrEmpty(parts[1]))
                                displayName = parts[1];
                        }
                    }

                    string message = ParseMessage(line);

                    if (message.Trim().Equals(EntryCommand.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        string lower = displayName.ToLower();
                        if (!_entrantLower.Contains(lower) && lower != BotName.ToLower() && lower != Channel.ToLower())
                        {
                            _entrantLower.Add(lower);
                            NewEntrant?.Invoke(displayName);
                        }
                    }
                }
            }
        }

        private string ParseUser(string line)
        {
            int excl = line.IndexOf('!');
            return excl > 0 ? line.Substring(1, excl - 1) : "";
        }

        private string ParseMessage(string line)
        {
            int colon = line.IndexOf(":", line.IndexOf("PRIVMSG"));
            return colon > 0 ? line.Substring(colon + 1) : "";
        }

        public void SendMessage(string message)
        {
            _writer?.WriteLine($":PRIVMSG #{Channel} :{message}");
            _writer?.Flush();
        }
    }
}