using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using QRCoder;
using Windows.Media.Control;
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace XrdRemote
{
    public partial class MainWindow : Window
    {
        private HttpListener _listener;
        private GlobalSystemMediaTransportControlsSessionManager _mediaManager;
        private MMDevice _audioDevice;
        private string _localIp;
        private int _currentPort = 8080;
        private Forms.NotifyIcon _trayIcon;

        private string _sessionToken;
        private readonly Dictionary<string, (int Attempts, DateTime BanUntil)> _rateLimits = new();
        private readonly Dictionary<string, (string DeviceId, DateTime LastSeen)> _activeSessions = new();
        private readonly object _secLock = new();

        private readonly List<WebSocket> _wsClients = new();
        private readonly object _wsLock = new();
        private Timer _wsTimer;

        private string TokenFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RemoteXS", "token.txt");

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeAudio();
            InitializeMediaManager();
            StartServer();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var sw = new SettingsWindow(_currentPort);
            sw.Owner = this;
            sw.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            sw.ShowDialog();
        }

        private void RegenerateToken_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Regenerate PIN?\nAll connected devices will be disconnected.",
                    "RemoteXS", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            RegenerateToken();
        }

        public void UpdateServerPort(int newPort)
        {
            if (_currentPort == newPort) return;
            _currentPort = newPort;
            _listener?.Stop();
            _listener?.Close();
            StartServer();
        }

        private void CloseToTray_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _trayIcon.ShowBalloonTip(2000, "RemoteXS", "Server is running in the background", Forms.ToolTipIcon.Info);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); }

        private void InitializeTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon();
            using (var bmp = new System.Drawing.Bitmap(16, 16))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.FillEllipse(System.Drawing.Brushes.LimeGreen, 2, 2, 12, 12);
                g.DrawEllipse(System.Drawing.Pens.Black, 2, 2, 12, 12);
                _trayIcon.Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
            }
            _trayIcon.Text = "RemoteXS Server";
            _trayIcon.Visible = true;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open", null, (s, e) => ShowWindow());
            menu.Items.Add("-");
            menu.Items.Add("Exit", null, (s, e) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
        private void ExitApp() { _wsTimer?.Dispose(); _trayIcon.Visible = false; _trayIcon.Dispose(); Application.Current.Shutdown(); }

        private void InitializeAudio()
        {
            try { var en = new MMDeviceEnumerator(); _audioDevice = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
            catch { _audioDevice = null; }
        }

        private void EnsureAudioDevice() { if (_audioDevice != null) return; InitializeAudio(); }
        private async void InitializeMediaManager() { try { _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); } catch { } }

        private string GenerateToken() => RandomNumberGenerator.GetInt32(1000, 10000).ToString("D4");

        private string LoadSavedToken()
        {
            try { if (File.Exists(TokenFilePath)) return File.ReadAllText(TokenFilePath).Trim(); } catch { }
            return null;
        }

        private void SaveToken(string t)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(TokenFilePath)!); File.WriteAllText(TokenFilePath, t); } catch { }
        }

        private void StartServer()
        {
            try
            {
                var saved = LoadSavedToken();
                if (string.IsNullOrEmpty(saved) || saved.Length != 4 || !saved.All(char.IsDigit))
                {
                    _sessionToken = GenerateToken();
                    SaveToken(_sessionToken);
                }
                else { _sessionToken = saved; }

                _localIp = GetLocalIpAddress();
                string url = $"http://{_localIp}:{_currentPort}/?token={_sessionToken}";

                IpText.Text = $"{_localIp}:{_currentPort}";
                PinText.Text = $"PIN: {_sessionToken}";
                GenerateQr(url);
                RefreshClientCount();

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://{_localIp}:{_currentPort}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_currentPort}/");
                _listener.Start();
                Task.Run(ListenLoop);

                _wsTimer?.Dispose();
                _wsTimer = new Timer(_ => { _ = BroadcastState(); }, null, 300, 300);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска сервера: {ex.Message}\nПроверь брандмауэр и не занят ли порт.", "Критическая ошибка");
            }
        }

        private void RegenerateToken()
        {
            _sessionToken = GenerateToken();
            SaveToken(_sessionToken);
            lock (_secLock) _activeSessions.Clear();
            string url = $"http://{_localIp}:{_currentPort}/?token={_sessionToken}";
            Dispatcher.Invoke(() => { PinText.Text = $"PIN: {_sessionToken}"; GenerateQr(url); RefreshClientCount(); });
        }

        private string GetLocalIpAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
            }
            catch
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) return ip.ToString();
                return "127.0.0.1";
            }
        }

        private void GenerateQr(string url)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var code = new QRCode(data);
            using var bmp = code.GetGraphic(20);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            var img = new BitmapImage();
            img.BeginInit(); img.StreamSource = ms; img.CacheOption = BitmapCacheOption.OnLoad; img.EndInit();
            QrImage.Source = img;
        }

        private async Task ListenLoop()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    if (ctx.Request.IsWebSocketRequest) _ = Task.Run(() => HandleWebSocket(ctx));
                    else _ = Task.Run(() => HandleRequest(ctx));
                }
                catch { }
            }
        }

        private async Task HandleWebSocket(HttpListenerContext ctx)
        {
            string clientIp = ctx.Request.RemoteEndPoint?.Address?.ToString() ?? "0.0.0.0";
            if (IsRateLimited(clientIp)) { ctx.Response.StatusCode = 429; ctx.Response.Close(); return; }
            string wsToken = ctx.Request.QueryString["token"] ?? "";
            if (wsToken != _sessionToken) { RecordFailedAuth(clientIp); ctx.Response.StatusCode = 401; ctx.Response.Close(); return; }
            string deviceId = ctx.Request.Headers["X-Device-ID"] ?? clientIp;
            RecordSession(clientIp, deviceId);

            var wsCtx = await ctx.AcceptWebSocketAsync(null);
            var ws = wsCtx.WebSocket;
            lock (_wsLock) _wsClients.Add(ws);
            try
            {
                var buf = new byte[4096];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }
                }
            }
            catch { }
            finally { lock (_wsLock) _wsClients.Remove(ws); try { ws.Dispose(); } catch { } Dispatcher.InvokeAsync(RefreshClientCount); }
        }

        private async Task BroadcastState()
        {
            List<WebSocket> clients;
            lock (_wsLock) clients = _wsClients.Where(w => w.State == WebSocketState.Open).ToList();
            if (!clients.Any()) return;
            try
            {
                var state = await GetMediaInfo();
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state));
                var seg = new ArraySegment<byte>(bytes);
                var dead = new List<WebSocket>();
                foreach (var ws in clients)
                {
                    try { await ws.SendAsync(seg, WebSocketMessageType.Text, true, CancellationToken.None); }
                    catch { dead.Add(ws); }
                }
                if (dead.Any()) lock (_wsLock) foreach (var ws in dead) _wsClients.Remove(ws);
            }
            catch { }
        }

        private bool IsRateLimited(string ip)
        {
            lock (_secLock)
            {
                if (!_rateLimits.TryGetValue(ip, out var rec)) return false;
                if (rec.BanUntil > DateTime.UtcNow) return true;
                if (rec.BanUntil != DateTime.MinValue) _rateLimits[ip] = (0, DateTime.MinValue);
                return false;
            }
        }

        private void RecordFailedAuth(string ip)
        {
            lock (_secLock)
            {
                _rateLimits.TryGetValue(ip, out var rec);
                int attempts = rec.Attempts + 1;
                DateTime ban = attempts >= 5 ? DateTime.UtcNow.AddMinutes(5) : DateTime.MinValue;
                _rateLimits[ip] = (attempts, ban);
            }
        }

        private void RecordSession(string ip, string deviceId)
        {
            lock (_secLock)
            {
                _activeSessions[ip] = (deviceId, DateTime.UtcNow);
                var stale = _activeSessions.Where(kv => (DateTime.UtcNow - kv.Value.LastSeen).TotalSeconds > 60).Select(kv => kv.Key).ToList();
                foreach (var k in stale) _activeSessions.Remove(k);
                if (_rateLimits.Count > 500)
                {
                    var expired = _rateLimits.Where(kv => kv.Value.BanUntil < DateTime.UtcNow && kv.Value.Attempts == 0).Select(kv => kv.Key).Take(100).ToList();
                    foreach (var k in expired) _rateLimits.Remove(k);
                }
            }
            Dispatcher.InvokeAsync(RefreshClientCount);
        }

        private void RefreshClientCount()
        {
            int count; lock (_secLock) count = _activeSessions.Count;
            ClientCountText.Text = count == 0 ? "No connections" : count == 1 ? "1 device connected" : $"{count} devices connected";
        }

        private async void HandleRequest(HttpListenerContext ctx)
        {
            string rawUrl = ctx.Request.RawUrl ?? "/";
            string path = rawUrl.Split('?')[0]; 
            string method = ctx.Request.HttpMethod;
            string response = "{}";
            int code = 200;
            string contentType = "application/json";
            byte[] responseBytes = null;

            ctx.Response.AppendHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AppendHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            ctx.Response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, X-Token, X-Device-ID");

            if (method == "OPTIONS") { ctx.Response.Close(); return; }

            if (path == "/discover")
            {
                response = JsonSerializer.Serialize(new { app = "RemoteXS", version = "2.4", host = Environment.MachineName });
                goto SendResponse;
            }

            if (path == "/manifest.json")
            {
                contentType = "application/manifest+json";
                response = "{\"name\":\"RemoteXS\",\"short_name\":\"RemoteXS\",\"start_url\":\"/\",\"display\":\"standalone\",\"background_color\":\"#000000\",\"theme_color\":\"#000000\",\"orientation\":\"portrait\"}";
                goto SendResponse;
            }

            bool isApiRoute = path == "/ping" || path == "/mixer" || path == "/command" || path == "/keyboard" || path == "/clipboard";

            if (isApiRoute)
            {
                string clientIp = ctx.Request.RemoteEndPoint?.Address?.ToString() ?? "0.0.0.0";
                if (IsRateLimited(clientIp)) { ctx.Response.StatusCode = 429; ctx.Response.Close(); return; }

                string clientToken = ctx.Request.Headers["X-Token"] ?? ctx.Request.QueryString["token"] ?? "";
                bool isAuth = false;

                if (clientToken == _sessionToken)
                {
                    isAuth = true;
                    string deviceId = ctx.Request.Headers["X-Device-ID"] ?? clientIp;
                    RecordSession(clientIp, deviceId);
                }
                else
                {
                    lock (_secLock)
                    {
                        if (_activeSessions.TryGetValue(clientIp, out var session))
                        {
                            if ((DateTime.UtcNow - session.LastSeen).TotalHours < 2)
                            {
                                isAuth = true;
                                _activeSessions[clientIp] = (session.DeviceId, DateTime.UtcNow);
                            }
                        }
                    }
                }

                if (!isAuth)
                {
                    RecordFailedAuth(clientIp);
                    ctx.Response.StatusCode = 401;
                    ctx.Response.OutputStream.Write(Encoding.UTF8.GetBytes("{\"error\":\"unauthorized\"}"));
                    ctx.Response.OutputStream.Close();
                    return;
                }
            }

            try
            {
                if (path == "/ping")
                {
                    var media = await GetMediaInfo();
                    response = JsonSerializer.Serialize(media);
                }
                else if (path == "/mixer")
                {
                    var mixer = await Application.Current.Dispatcher.InvokeAsync(GetMixerData);
                    response = JsonSerializer.Serialize(mixer);
                }
                else if (path == "/command" && method == "POST")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream);
                    var json = await reader.ReadToEndAsync();
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var cmd = JsonSerializer.Deserialize<CommandData>(json, opts);
                    Application.Current.Dispatcher.Invoke(() => ExecuteCommand(cmd));
                }
                else if (path == "/clipboard" && method == "GET")
                {
                    string clipText = "";
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try { if (System.Windows.Clipboard.ContainsText()) clipText = System.Windows.Clipboard.GetText(); } catch { }
                    });
                    response = JsonSerializer.Serialize(new { text = clipText });
                }
                else if (path == "/keyboard")
                {
                    contentType = "text/html; charset=utf-8";
                    response = KeyboardModule.GetHtml();
                }
                else
                {
                    string html = GetEmbeddedSite();
                    if (!string.IsNullOrEmpty(html)) { contentType = "text/html; charset=utf-8"; responseBytes = Encoding.UTF8.GetBytes(html); }
                    else { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }
                }
            }
            catch (Exception ex) { code = 500; response = JsonSerializer.Serialize(new { error = ex.Message }); }

            SendResponse:
            if (responseBytes == null) responseBytes = Encoding.UTF8.GetBytes(response);
            ctx.Response.ContentType = contentType;
            ctx.Response.StatusCode = code;
            ctx.Response.ContentLength64 = responseBytes.Length;
            ctx.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            ctx.Response.OutputStream.Close();
        }

        private string GetEmbeddedSite()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("index.html"));
                if (name == null) return null;
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return null;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch { return null; }
        }

        private async Task<object> GetMediaInfo()
        {
            string window = "PC Connected"; string cover = ""; int vol = 0; bool playing = false; double position = 0, duration = 0;
            try
            {
                EnsureAudioDevice();
                if (_audioDevice != null) vol = (int)(_audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                if (_mediaManager != null)
                {
                    var s = _mediaManager.GetCurrentSession();
                    if (s != null)
                    {
                        var pb = s.GetPlaybackInfo();
                        playing = pb.Controls.IsPauseEnabled && pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                        var props = await s.TryGetMediaPropertiesAsync();
                        window = $"{props.Title} • {props.Artist}";
                        if (props.Thumbnail != null)
                        {
                            using var st = await props.Thumbnail.OpenReadAsync();
                            using var ms = new MemoryStream();
                            await st.AsStreamForRead().CopyToAsync(ms);
                            cover = Convert.ToBase64String(ms.ToArray());
                        }
                        try { var tl = s.GetTimelineProperties(); position = tl.Position.TotalSeconds; duration = tl.EndTime.TotalSeconds; } catch { }
                    }
                }
            }
            catch { }
            return new { window, cover, volume = vol, playing, position, duration };
        }

        private List<MixerItem> GetMixerData()
        {
            var list = new List<MixerItem>();
            try
            {
                EnsureAudioDevice(); if (_audioDevice == null) return list;
                list.Add(new MixerItem { pid = -1, name = "Master Volume", vol = (int)(_audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100), icon = "" });
                var sessions = _audioDevice.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    if (s.State != AudioSessionState.AudioSessionStateActive && s.State != AudioSessionState.AudioSessionStateInactive) continue;
                    string name = ""; uint pid = s.GetProcessID; string icon = "";
                    if (pid > 0) { try { var proc = System.Diagnostics.Process.GetProcessById((int)pid); name = proc.ProcessName; icon = GetProcessIcon((int)pid); } catch { } }
                    if (string.IsNullOrEmpty(name)) name = s.DisplayName;
                    if (string.IsNullOrEmpty(name)) name = "System Sound";
                    list.Add(new MixerItem { pid = (int)pid, name = name, vol = (int)(s.SimpleAudioVolume.Volume * 100), icon = icon });
                }
            }
            catch { } return list;
        }

        private string GetProcessIcon(int pid)
        {
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(pid);
                if (proc.MainModule == null) return "";
                using var ico = System.Drawing.Icon.ExtractAssociatedIcon(proc.MainModule.FileName);
                if (ico == null) return "";
                using var bmp = ico.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
            catch { return ""; }
        }

        private void SetAppVolume(int id, int vol)
        {
            float v = Math.Clamp(vol, 0, 100) / 100.0f;
            try
            {
                EnsureAudioDevice(); if (_audioDevice == null) return;
                if (id == -1) _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = v;
                else
                {
                    var sessions = _audioDevice.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++) if (sessions[i].GetProcessID == id) sessions[i].SimpleAudioVolume.Volume = v;
                }
            }
            catch { _audioDevice = null; }
        }

        private async void SetMediaPosition(double posSeconds)
        {
            if (_mediaManager == null) return;
            try
            {
                var s = _mediaManager.GetCurrentSession();
                if (s != null) await s.TryChangePlaybackPositionAsync(Convert.ToInt64(posSeconds * 10000000));
            }
            catch { }
        }

        private void ExecuteCommand(CommandData data)
        {
            if (data == null) return;
            try
            {
                switch (data.cmd)
                {
                    case "prev":       keybd_event(VK_MEDIA_PREV_TRACK, 0, 0, 0); break;
                    case "next":       keybd_event(VK_MEDIA_NEXT_TRACK, 0, 0, 0); break;
                    case "play_pause": keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, 0); break;
                    case "mute":       keybd_event(VK_VOLUME_MUTE, 0, 0, 0);      break;
                    case "vol_up":     keybd_event(VK_VOLUME_UP, 0, 0, 0);        break;
                    case "vol_down":   keybd_event(VK_VOLUME_DOWN, 0, 0, 0);      break;
                    case "set_vol":    SetAppVolume(data.id, data.vol);           break;
                    case "set_pos":    SetMediaPosition(data.pos);                break;
                    case "key_event":  KeyboardModule.HandleCommand(data.cmd, data.id, data.vol > 0); break;
                    case "mouse_move":
                        GetCursorPos(out Point p); SetCursorPos(p.X + (int)data.x, p.Y + (int)data.y); break;
                    case "click_left":  mouse_event(MOUSEEVENTF_LEFTDOWN  | MOUSEEVENTF_LEFTUP,  0, 0, 0, 0); break;
                    case "click_right": mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0); break;
                    case "scroll":
                        int sv = (int)(data.dy != 0 ? data.dy : data.y); mouse_event(MOUSEEVENTF_WHEEL, 0, 0, sv * -1, 0); break;
                    case "clipboard_set":
                        if (!string.IsNullOrEmpty(data.text)) System.Windows.Clipboard.SetText(data.text); break;
                    case "pres_next":  PressKey(VK_RIGHT);  break;
                    case "pres_prev":  PressKey(VK_LEFT);   break;
                    case "pres_start": PressKey(VK_F5);     break;
                    case "pres_end":   PressKey(VK_ESCAPE); break;
                    case "pres_black": PressKey(VK_B);      break;
                }
            }
            catch { }
        }

        private void PressKey(byte vk) { keybd_event(vk, 0, 0, 0); keybd_event(vk, 0, 0x0002, 0); }

        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, int extra);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, int data, int extra);
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X; public int Y; }

        const byte VK_LEFT           = 0x25;
        const byte VK_RIGHT          = 0x27;
        const byte VK_F5             = 0x74;
        const byte VK_ESCAPE         = 0x1B;
        const byte VK_B              = 0x42;
        const byte VK_VOLUME_MUTE    = 0xAD;
        const byte VK_VOLUME_DOWN    = 0xAE;
        const byte VK_VOLUME_UP      = 0xAF;
        const byte VK_MEDIA_NEXT_TRACK  = 0xB0;
        const byte VK_MEDIA_PREV_TRACK  = 0xB1;
        const byte VK_MEDIA_PLAY_PAUSE  = 0xB3;
        const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP   = 0x0010;
        const uint MOUSEEVENTF_WHEEL     = 0x0800;

        private void CloseApp_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class CommandData
    {
        public string cmd  { get; set; }
        public string text { get; set; }
        public double x    { get; set; }
        public double y    { get; set; }
        public double dy   { get; set; }
        public int    id   { get; set; }
        public int    vol  { get; set; }
        public double pos  { get; set; }
    }

    public class MixerItem
    {
        public int    pid  { get; set; }
        public string name { get; set; }
        public int    vol  { get; set; }
        public string icon { get; set; }
    }
}