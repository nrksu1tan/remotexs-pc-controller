using System;
using System.Windows;
using System.Collections.Generic;
using System.Drawing; 
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
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
        private const int PORT = 8080;
        
        // Трей
        private Forms.NotifyIcon _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon(); // Запускаем иконку в трее
            InitializeAudio();     
            InitializeMediaManager(); 
            StartServer();        
        }

        // --- ЛОГИКА ТРЕЯ ---
private void InitializeTrayIcon()
{
    _trayIcon = new Forms.NotifyIcon();
    
    // Рисуем иконку (зеленый круг)
    // Используем System.Drawing явно, чтобы не путать с WPF
    using (var bmp = new System.Drawing.Bitmap(16, 16))
    using (var g = System.Drawing.Graphics.FromImage(bmp))
    {
        g.Clear(System.Drawing.Color.Transparent);
        g.FillEllipse(System.Drawing.Brushes.LimeGreen, 2, 2, 12, 12);
        g.DrawEllipse(System.Drawing.Pens.Black, 2, 2, 12, 12);

        // --- ВОТ ЗДЕСЬ БЫЛА ОШИБКА ---
        // Было: Icon.FromHandle(...) -> Конфликт с this.Icon окна
        // Стало: System.Drawing.Icon.FromHandle(...) -> Четкое указание класса
        _trayIcon.Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }
    
    _trayIcon.Text = "XRD Remote Server";
    _trayIcon.Visible = true;
    
    // Меню трея
    var contextMenu = new Forms.ContextMenuStrip();
    contextMenu.Items.Add("Открыть", null, (s, e) => ShowWindow());
    contextMenu.Items.Add("-");
    contextMenu.Items.Add("Выход", null, (s, e) => ExitApp());
    _trayIcon.ContextMenuStrip = contextMenu;

    _trayIcon.DoubleClick += (s, e) => ShowWindow();
}

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApp()
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Current.Shutdown();
        }

        // Кнопки окна
        private void CloseToTray_Click(object sender, RoutedEventArgs e)
        {
            Hide(); // Скрываем окно, но приложение работает
            _trayIcon.ShowBalloonTip(3000, "XRD Server", "Сервер свернут в трей", Forms.ToolTipIcon.Info);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        
        // Перетаскивание окна за любое место
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        // --- ИНИЦИАЛИЗАЦИЯ ---
        private void InitializeAudio()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                _audioDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch { }
        }

        private async void InitializeMediaManager()
        {
            try
            {
                _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            catch { }
        }

        private void StartServer()
        {
            try
            {
                _localIp = GetLocalIpAddress();
                string url = $"http://{_localIp}:{PORT}";
                IpText.Text = url;
                GenerateQr(url);

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{PORT}/");
                _listener.Start();
                Task.Run(ListenLoop);
            }
            catch (Exception ex) { MessageBox.Show("Ошибка сервера: " + ex.Message); }
        }

        private string GetLocalIpAddress()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
            }
        }

        private void GenerateQr(string url)
        {
            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q))
            using (var code = new QRCode(data))
            using (var bmp = code.GetGraphic(20))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                QrImage.Source = bitmap;
            }
        }

        // --- СЕРВЕР ---
        private async Task ListenLoop()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch { }
            }
        }

private async void HandleRequest(HttpListenerContext context)
        {
            string rawUrl = context.Request.RawUrl;
            string method = context.Request.HttpMethod;
            string response = "{}";
            int code = 200;
            string contentType = "application/json";
            byte[] responseBytes = null; // Буфер для ответа

            context.Response.AppendHeader("Access-Control-Allow-Origin", "*");
            context.Response.AppendHeader("Access-Control-Allow-Methods", "POST, GET");
            context.Response.AppendHeader("Access-Control-Allow-Headers", "Content-Type");

            if (method == "OPTIONS")
            {
                context.Response.Close();
                return;
            }

            try
            {
                if (rawUrl == "/ping")
                {
                    var media = await GetMediaInfo();
                    response = JsonSerializer.Serialize(media);
                }
                else if (rawUrl == "/mixer")
                {
                    var mixerData = await Application.Current.Dispatcher.InvokeAsync(() => GetMixerData());
                    response = JsonSerializer.Serialize(mixerData);
                }
                else if (rawUrl == "/command" && method == "POST")
                {
                    using (var r = new StreamReader(context.Request.InputStream))
                    {
                        var json = await r.ReadToEndAsync();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var cmd = JsonSerializer.Deserialize<CommandData>(json, options);
                        Application.Current.Dispatcher.Invoke(() => ExecuteCommand(cmd));
                    }
                }
                else
                {
                    // --- ГЛАВНОЕ ИЗМЕНЕНИЕ: ЧИТАЕМ ИЗ РЕСУРСОВ ---
                    // Если запрашивают сайт (корень), отдаем встроенный HTML
                    string html = GetEmbeddedSite();
                    if (!string.IsNullOrEmpty(html))
                    {
                        contentType = "text/html";
                        responseBytes = Encoding.UTF8.GetBytes(html);
                    }
                }
            }
            catch (Exception ex)
            {
                code = 500;
                response = JsonSerializer.Serialize(new { error = ex.Message });
            }

            // Если байты не были сформированы (например, это JSON ответ), кодируем строку response
            if (responseBytes == null)
            {
                responseBytes = Encoding.UTF8.GetBytes(response);
            }

            context.Response.ContentType = contentType;
            context.Response.StatusCode = code;
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();
        }

        // --- НОВЫЙ МЕТОД: ДОСТАЕТ САЙТ ИЗ EXE ---
private string GetEmbeddedSite()
{
    try
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        
        // --- МАГИЯ: Ищем ресурс автоматически ---
        // Получаем список ВСЕХ зашитых файлов и берем первый, который кончается на index.html
        string resourceName = assembly.GetManifestResourceNames()
                                      .FirstOrDefault(str => str.EndsWith("index.html"));

        if (string.IsNullOrEmpty(resourceName)) 
        {
            // Если всё еще не находит, выводим список того, что есть (для отладки)
            string allResources = string.Join(", ", assembly.GetManifestResourceNames());
            return $"<h1>Error: index.html not found. Available: {allResources}</h1>";
        }

        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null) return "<h1>Error: Stream is null</h1>";
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
    catch (Exception ex)
    {
        return $"<h1>Error: {ex.Message}</h1>";
    }
}
        private List<MixerItem> GetMixerData()
        {
            var list = new List<MixerItem>();
            try
            {
                if (_audioDevice == null) return list;

                // 1. Master Volume
                list.Add(new MixerItem
                {
                    pid = -1,
                    name = "Общая громкость",
                    vol = (int)(_audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100),
                    icon = "" // Пустая строка = стандартная иконка на клиенте
                });

                // 2. Apps
                var sessions = _audioDevice.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    if (s.State == AudioSessionState.AudioSessionStateActive || s.State == AudioSessionState.AudioSessionStateInactive)
                    {
                        string name = "";
                        uint procId = s.GetProcessID;
                        string iconBase64 = "";

                        if (procId > 0)
                        {
                            try
                            {
                                var p = System.Diagnostics.Process.GetProcessById((int)procId);
                                name = p.ProcessName;
                                iconBase64 = GetProcessIcon((int)procId);
                            }
                            catch { }
                        }

                        if (string.IsNullOrEmpty(name)) name = s.DisplayName;
                        if (string.IsNullOrEmpty(name)) name = "System Sound";

                        list.Add(new MixerItem
                        {
                            pid = (int)procId,
                            name = name,
                            vol = (int)(s.SimpleAudioVolume.Volume * 100),
                            icon = iconBase64
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        private string GetProcessIcon(int pid)
        {
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(pid);
                if (proc.MainModule == null) return "";
                
                string path = proc.MainModule.FileName;
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return "";
                    using (var bmp = icon.ToBitmap())
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        private void SetAppVolume(int id, int vol)
        {
            if (_audioDevice == null) return;
            float v = Math.Clamp(vol, 0, 100) / 100.0f;

            try
            {
                if (id == -1)
                {
                    _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = v;
                }
                else
                {
                    var sessions = _audioDevice.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        if (sessions[i].GetProcessID == id)
                        {
                            sessions[i].SimpleAudioVolume.Volume = v;
                        }
                    }
                }
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
                    case "prev": keybd_event(VK_MEDIA_PREV_TRACK, 0, 0, 0); break;
                    case "next": keybd_event(VK_MEDIA_NEXT_TRACK, 0, 0, 0); break;
                    case "play_pause": keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, 0); break;
                    case "mute": keybd_event(VK_VOLUME_MUTE, 0, 0, 0); break;
                    case "vol_up": keybd_event(VK_VOLUME_UP, 0, 0, 0); break;
                    case "vol_down": keybd_event(VK_VOLUME_DOWN, 0, 0, 0); break;

                    case "set_vol":
                        SetAppVolume(data.id, data.vol);
                        break;

                    case "mouse_move":
                        GetCursorPos(out Point p);
                        SetCursorPos(p.X + (int)data.x, p.Y + (int)data.y);
                        break;
                    case "click_left": mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0); break;
                    case "click_right": mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0); break;

                    case "scroll":
                        int scrollVal = (int)(data.dy != 0 ? data.dy : data.y);
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, scrollVal * -1, 0);
                        break;
                }
            }
            catch { }
        }

        private async Task<object> GetMediaInfo()
        {
            string window = "PC Connected";
            string cover = "";

            try
            {
                if (_mediaManager != null)
                {
                    var s = _mediaManager.GetCurrentSession();
                    if (s != null)
                    {
                        var p = await s.TryGetMediaPropertiesAsync();
                        window = $"{p.Title} • {p.Artist}";

                        if (p.Thumbnail != null)
                        {
                            using (var st = await p.Thumbnail.OpenReadAsync())
                            using (var ms = new MemoryStream())
                            {
                                await st.AsStreamForRead().CopyToAsync(ms);
                                cover = Convert.ToBase64String(ms.ToArray());
                            }
                        }
                    }
                }
            }
            catch { }
            return new { window = window, cover = cover };
        }

        // --- WinAPI ---
        [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point lpPoint);
        [DllImport("user32.dll")] static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X; public int Y; }

        const byte VK_VOLUME_MUTE = 0xAD;
        const byte VK_VOLUME_DOWN = 0xAE;
        const byte VK_VOLUME_UP = 0xAF;
        const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        const byte VK_MEDIA_PREV_TRACK = 0xB1;
        const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_WHEEL = 0x0800;

        private void CloseApp_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class CommandData
    {
        public string cmd { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double dy { get; set; }
        public int id { get; set; }
        public int vol { get; set; }
    }

    public class MixerItem
    {
        public int pid { get; set; }
        public string name { get; set; }
        public int vol { get; set; }
        public string icon { get; set; }
    }
}