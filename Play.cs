using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SpellBinder
{
    public class PlayForm : Form
    {
        private ComboBox serverBox;
        private Button playButton;
        private Label statusLabel;
        private ListBox playerList;
        private Label playerLabel;
        private Process gameProcess;
        private System.Windows.Forms.Timer watchdog;
        private System.Windows.Forms.Timer playerRefresh;
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private string gameDir;

        // Parsed player data — parallel to playerList.Items
        private System.Collections.Generic.List<PlayerInfo> players = new System.Collections.Generic.List<PlayerInfo>();

        private class PlayerInfo
        {
            public string Account;
            public string Character;
            public string Class;
            public int Level;
            public string Arena;
            public string Team;
            public string Location;
        }

        private static readonly string[][] Servers = new string[][]
        {
            new[] { "Community Server", "45.33.60.131" },
            new[] { "Localhost", "127.0.0.1" },
        };

        // Team colors for orbs
        private static Color TeamColor(string team)
        {
            if (team == null) return Color.FromArgb(160, 150, 190);
            switch (team.ToLower())
            {
                case "dragon":  return Color.FromArgb(255, 100, 80);  // red-orange
                case "phoenix": return Color.FromArgb(255, 200, 60);  // gold
                case "griffin":  return Color.FromArgb(80, 180, 255);  // blue
                case "griffon": return Color.FromArgb(80, 180, 255);  // blue (alt spelling)
                default:        return Color.FromArgb(160, 120, 255); // purple fallback
            }
        }

        [DllImport("user32.dll")]
        private static extern bool IsHungAppWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

        public PlayForm()
        {
            // Game files live in ./game/ subfolder relative to this exe
            gameDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game");

            Text = "SpellBinder: The Nexus Conflict";
            ClientSize = new Size(400, 440);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(20, 16, 28);

            // Title
            var title = new Label
            {
                Text = "SPELLBINDER",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 170, 255),
                BackColor = Color.Transparent,
                Location = new Point(0, 15),
                Size = new Size(400, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var subtitle = new Label
            {
                Text = "The Nexus Conflict",
                Font = new Font("Segoe UI", 10, FontStyle.Italic),
                ForeColor = Color.FromArgb(140, 120, 180),
                BackColor = Color.Transparent,
                Location = new Point(0, 52),
                Size = new Size(400, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Separator
            var sep = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(30, 80),
                Size = new Size(340, 2),
                BackColor = Color.FromArgb(60, 50, 80)
            };

            // Server row
            var serverLabel = new Label
            {
                Text = "Server",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(160, 150, 180),
                BackColor = Color.Transparent,
                Location = new Point(30, 96),
                AutoSize = true
            };

            serverBox = new ComboBox
            {
                Location = new Point(30, 116),
                Size = new Size(340, 28),
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 30, 50),
                ForeColor = Color.FromArgb(220, 210, 240)
            };
            foreach (var s in Servers)
                serverBox.Items.Add(s[0] + "  (" + s[1] + ")");
            serverBox.SelectedIndex = 0;

            // Play button
            playButton = new Button
            {
                Text = "PLAY",
                Location = new Point(30, 160),
                Size = new Size(340, 45),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(80, 50, 140),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            playButton.FlatAppearance.BorderColor = Color.FromArgb(120, 90, 200);
            playButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(100, 70, 170);
            playButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 35, 110);
            playButton.Click += OnPlay;

            // Status
            statusLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(120, 110, 140),
                BackColor = Color.Transparent,
                Location = new Point(30, 215),
                Size = new Size(340, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Player list section
            var sep2 = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(30, 242),
                Size = new Size(340, 2),
                BackColor = Color.FromArgb(60, 50, 80)
            };

            playerLabel = new Label
            {
                Text = "\u2726  Players Online",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 160, 220),
                BackColor = Color.Transparent,
                Location = new Point(30, 250),
                AutoSize = true
            };

            playerList = new ListBox
            {
                Location = new Point(30, 272),
                Size = new Size(340, 150),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(25, 20, 38),
                ForeColor = Color.FromArgb(220, 210, 240),
                BorderStyle = BorderStyle.FixedSingle,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 28
            };
            playerList.DrawItem += OnDrawPlayerItem;

            Controls.AddRange(new Control[] { title, subtitle, sep, serverLabel, serverBox, playButton, statusLabel, sep2, playerLabel, playerList });

            watchdog = new System.Windows.Forms.Timer { Interval = 3000 };
            watchdog.Tick += OnWatchdog;

            playerRefresh = new System.Windows.Forms.Timer { Interval = 30000 };
            playerRefresh.Tick += (s, ev) => FetchPlayers();
            playerRefresh.Start();

            serverBox.SelectedIndexChanged += (s, ev) => FetchPlayers();

            FormClosing += OnFormClosing;
            Shown += (s, ev) => FetchPlayers();
        }

        private string GetServerAddress()
        {
            string text = serverBox.Text;
            int paren = text.IndexOf('(');
            if (paren >= 0)
            {
                int end = text.IndexOf(')', paren);
                if (end > paren)
                    return text.Substring(paren + 1, end - paren - 1).Trim();
            }
            return text.Trim();
        }

        private void WriteMainDat(string address)
        {
            string mainDat = Path.Combine(gameDir, "main.dat");
            if (!File.Exists(mainDat))
            {
                SetStatus("main.dat not found!", true);
                return;
            }
            WritePrivateProfileString("socket", "address", address, mainDat);
        }

        private void SetStatus(string msg, bool error)
        {
            statusLabel.Text = msg;
            statusLabel.ForeColor = error
                ? Color.FromArgb(255, 100, 100)
                : Color.FromArgb(120, 110, 140);
        }

        private void OnPlay(object sender, EventArgs e)
        {
            if (gameProcess != null && !gameProcess.HasExited)
            {
                SetStatus("Game is already running.", false);
                return;
            }

            if (!Directory.Exists(gameDir))
            {
                SetStatus("Game folder not found! Expected: ./game/", true);
                return;
            }

            string address = GetServerAddress();
            if (string.IsNullOrEmpty(address))
            {
                SetStatus("Enter a server address.", true);
                return;
            }

            string gameExe = Path.Combine(gameDir, "game.dll");
            if (!File.Exists(gameExe))
            {
                SetStatus("game.dll not found in ./game/", true);
                return;
            }

            WriteMainDat(address);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = gameExe,
                    WorkingDirectory = gameDir,
                    UseShellExecute = false
                };
                gameProcess = Process.Start(psi);
                SetStatus("Connecting to " + address + "...", false);
                playButton.Enabled = false;
                playButton.Text = "RUNNING...";
                watchdog.Start();
            }
            catch (Exception ex)
            {
                SetStatus("Launch failed: " + ex.Message, true);
            }
        }

        private void OnWatchdog(object sender, EventArgs e)
        {
            if (gameProcess == null || gameProcess.HasExited)
            {
                watchdog.Stop();
                SetStatus("Game exited.", false);
                playButton.Enabled = true;
                playButton.Text = "PLAY";
                gameProcess = null;
                return;
            }

            if (gameProcess.MainWindowHandle != IntPtr.Zero && IsHungAppWindow(gameProcess.MainWindowHandle))
            {
                var result = MessageBox.Show(
                    "SpellBinder appears to be frozen.\nKill the process?",
                    "Game Not Responding",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try { gameProcess.Kill(); } catch { }
                    SetStatus("Game killed.", false);
                    playButton.Enabled = true;
                    playButton.Text = "PLAY";
                    gameProcess = null;
                    watchdog.Stop();
                }
            }
        }

        private void OnDrawPlayerItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Alternating row backgrounds with subtle gradient
            Color rowTop = (e.Index % 2 == 0)
                ? Color.FromArgb(30, 25, 45)
                : Color.FromArgb(38, 32, 55);
            Color rowBot = Color.FromArgb(rowTop.R - 4, rowTop.G - 4, rowTop.B - 4);
            using (var bg = new LinearGradientBrush(e.Bounds, rowTop, rowBot, 90f))
                g.FillRectangle(bg, e.Bounds);

            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            int x = e.Bounds.X;
            int y = e.Bounds.Y;
            int h = e.Bounds.Height;
            int w = e.Bounds.Width;

            // Status message (no player data)
            if (e.Index >= players.Count || players[e.Index] == null)
            {
                string text = playerList.Items[e.Index].ToString();
                using (var f = new Font("Segoe UI", 8.5f, FontStyle.Italic))
                using (var b = new SolidBrush(Color.FromArgb(100, 90, 120)))
                    g.DrawString(text, f, b, new RectangleF(x + 12, y, w - 12, h), sf);
                return;
            }

            var p = players[e.Index];
            Color orb = TeamColor(p.Team);

            // Column layout (340px total):
            //  8  [orb 10] 8  [name ~120] [class+lv ~85] [arena right ~110]
            int orbX = x + 8;
            int orbY = y + (h - 10) / 2;
            int nameX = x + 28;
            int nameW = 115;
            int classX = nameX + nameW;
            int classW = 82;
            int arenaX = classX + classW;
            int arenaW = w - (arenaX - x) - 4;

            // Glowing orb (team-colored)
            using (var glowBrush = new SolidBrush(Color.FromArgb(40, orb)))
                g.FillEllipse(glowBrush, orbX - 3, orbY - 3, 16, 16);
            using (var orbBrush = new LinearGradientBrush(
                new Rectangle(orbX, orbY, 10, 10),
                Color.FromArgb(255, Math.Min(orb.R + 60, 255), Math.Min(orb.G + 60, 255), Math.Min(orb.B + 60, 255)),
                orb, 135f))
                g.FillEllipse(orbBrush, orbX, orbY, 10, 10);
            using (var hi = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
                g.FillEllipse(hi, orbX + 2, orbY + 2, 3, 3);

            // Name (bright)
            using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(235, 225, 255)))
                g.DrawString(p.Account ?? p.Character ?? "???", f, b, new RectangleF(nameX, y, nameW, h), sf);

            // Class + Level (dimmed)
            string classLv = (p.Class ?? "") + " Lv" + p.Level;
            using (var f = new Font("Segoe UI", 8f))
            using (var b = new SolidBrush(Color.FromArgb(140, 130, 170)))
                g.DrawString(classLv, f, b, new RectangleF(classX, y, classW, h), sf);

            // Arena (right side, team-tinted)
            string arena = p.Arena ?? p.Location ?? "";
            // Shorten "[N] Kaelgard Keep" → "Kaelgard"
            if (arena.StartsWith("["))
            {
                int close = arena.IndexOf("] ");
                if (close >= 0) arena = arena.Substring(close + 2);
            }
            // Take first word if still long
            if (arena.Length > 14)
            {
                int sp = arena.IndexOf(' ');
                if (sp > 0) arena = arena.Substring(0, sp);
            }
            var sfRight = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Far, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using (var f = new Font("Segoe UI", 8f))
            using (var b = new SolidBrush(Color.FromArgb(100, orb.R, orb.G, orb.B)))
                g.DrawString(arena, f, b, new RectangleF(arenaX, y, arenaW, h), sfRight);
        }

        private async void FetchPlayers()
        {
            string address = GetServerAddress();
            if (string.IsNullOrEmpty(address))
                return;

            string url = "http://" + address + ":10603/api/players";
            playerLabel.Text = "\u2726  Players Online  (scrying...)";

            try
            {
                string json = await http.GetStringAsync(url);
                json = json.Trim();

                // Get the players array — either top-level or nested in {"players":[...]}
                string arrayJson;
                if (json.StartsWith("["))
                    arrayJson = json;
                else
                    arrayJson = ExtractJsonField(json, "players") ?? ExtractJsonField(json, "Players") ?? "[]";

                var parsed = new System.Collections.Generic.List<PlayerInfo>();
                foreach (string token in SplitJsonArray(arrayJson))
                {
                    string t = token.Trim();
                    if (!t.StartsWith("{")) continue;
                    var pi = new PlayerInfo
                    {
                        Account   = ExtractJsonField(t, "account"),
                        Character = ExtractJsonField(t, "character"),
                        Class     = ExtractJsonField(t, "class"),
                        Arena     = ExtractJsonField(t, "arena"),
                        Team      = ExtractJsonField(t, "team"),
                        Location  = ExtractJsonField(t, "location"),
                    };
                    string lv = ExtractJsonField(t, "level");
                    int lvNum;
                    if (lv != null && int.TryParse(lv, out lvNum)) pi.Level = lvNum;
                    else pi.Level = 1;
                    parsed.Add(pi);
                }

                players = parsed;
                playerList.Items.Clear();

                if (parsed.Count == 0)
                {
                    playerList.Items.Add("(no players online)");
                }
                else
                {
                    foreach (var p in parsed)
                        playerList.Items.Add(p.Account ?? p.Character ?? "Unknown");
                }

                playerLabel.Text = "\u2726  Players Online  \u2014  " + parsed.Count + " summoned";
            }
            catch
            {
                players = new System.Collections.Generic.List<PlayerInfo>();
                playerList.Items.Clear();
                playerList.Items.Add("(server unreachable)");
                playerLabel.Text = "\u2726  Players Online";
            }
        }

        // Minimal JSON helpers — avoids dependency on System.Text.Json / Newtonsoft
        private static System.Collections.Generic.List<string> SplitJsonArray(string json)
        {
            var items = new System.Collections.Generic.List<string>();
            json = json.Trim();
            if (json.Length < 2 || json[0] != '[') return items;
            int depth = 0; bool inStr = false; int start = 1;
            for (int i = 1; i < json.Length - 1; i++)
            {
                char c = json[i];
                if (c == '\\' && inStr) { i++; continue; }
                if (c == '"') inStr = !inStr;
                if (!inStr)
                {
                    if (c == '[' || c == '{') depth++;
                    else if (c == ']' || c == '}') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        items.Add(json.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                }
            }
            string last = json.Substring(start, json.Length - 1 - start).Trim();
            if (last.Length > 0) items.Add(last);
            return items;
        }

        private static string StripQuotes(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                return s.Substring(1, s.Length - 2);
            return s;
        }

        private static string ExtractJsonField(string json, string field)
        {
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return null;
            int vStart = colon + 1;
            while (vStart < json.Length && json[vStart] == ' ') vStart++;
            if (vStart >= json.Length) return null;
            char first = json[vStart];
            if (first == '"')
            {
                int end = json.IndexOf('"', vStart + 1);
                while (end > 0 && json[end - 1] == '\\')
                    end = json.IndexOf('"', end + 1);
                if (end < 0) return null;
                return json.Substring(vStart + 1, end - vStart - 1);
            }
            if (first == '[')
            {
                int depth = 0;
                for (int i = vStart; i < json.Length; i++)
                {
                    if (json[i] == '[') depth++;
                    else if (json[i] == ']') { depth--; if (depth == 0) return json.Substring(vStart, i - vStart + 1); }
                }
            }
            // Bare value (number, bool, null)
            int vEnd = vStart;
            while (vEnd < json.Length && json[vEnd] != ',' && json[vEnd] != '}' && json[vEnd] != ']')
                vEnd++;
            return json.Substring(vStart, vEnd - vStart).Trim();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            watchdog.Stop();
            playerRefresh.Stop();
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PlayForm());
        }
    }
}
