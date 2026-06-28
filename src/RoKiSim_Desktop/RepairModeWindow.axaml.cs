using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RoKiSim_Desktop
{
    public partial class RepairModeWindow : Window
    {
        private string? _apiKey;
        private string? _userName;
        private string? _selectedImagePath;
        private readonly HttpClient _httpClient = new HttpClient();
        
        // Persistent chat history for Gemini multi-turn conversation
        private List<object> _conversationHistory = new List<object>();

        // Phone photo server
        private Process? _photoServerProcess;
        private CancellationTokenSource? _pollCts;
        private HashSet<string> _processedPhotos = new HashSet<string>();
        private bool _phoneConnected = false;
        private string _phoneUrl = "";

        public RepairModeWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            
            string greeting = "Merhaba efendim! Bugün ne tamir ediyoruz? 📸 Bana arızalı parçanın fotoğrafını gönderebilir veya 📱 Telefon Bağla butonuyla telefonundan direkt çekebilirsiniz.";
            AddMessageToChat("Jarvis", greeting, isUser: false);
            // Sohbeti başlatan ilk karşılama mesajını şimdilik Login ekranında gizleyelim
            // OnLoginClick içinde _userName belli olduktan sonra atacağız.
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            StopPhoneServer();
        }

        private string GetSettingsPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_settings.json");
            // Geriye dönük uyumluluk veya geliştirme ortamı için fallback
            if (!File.Exists(path))
            {
                string devPath = "/home/main/roki_ws/src/RoKiSim_Desktop/user_settings.json";
                if (File.Exists(devPath)) return devPath;
            }
            return path;
        }

        private void LoadSettings()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("UserName", out var nameProp))
                    {
                        _userName = nameProp.GetString();
                        var nameBox = this.FindControl<TextBox>("LoginNameTextBox");
                        if (nameBox != null) nameBox.Text = _userName;
                    }
                    if (root.TryGetProperty("ApiKey", out var keyProp))
                    {
                        _apiKey = keyProp.GetString();
                        var keyBox = this.FindControl<TextBox>("LoginApiKeyTextBox");
                        if (keyBox != null) keyBox.Text = _apiKey;
                    }
                }
                else 
                {
                    // Eski api_key.txt dosyası varsa onu okuyalım
                    string oldKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api_key.txt");
                    if (File.Exists(oldKeyPath))
                    {
                        _apiKey = File.ReadAllText(oldKeyPath).Trim();
                        var keyBox = this.FindControl<TextBox>("LoginApiKeyTextBox");
                        if (keyBox != null) keyBox.Text = _apiKey;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Settings Load Error: " + ex.Message);
            }
        }

        private void OnLoginClick(object? sender, RoutedEventArgs e)
        {
            var nameBox = this.FindControl<TextBox>("LoginNameTextBox");
            var keyBox = this.FindControl<TextBox>("LoginApiKeyTextBox");
            
            _userName = nameBox?.Text?.Trim();
            _apiKey = keyBox?.Text?.Trim();

            if (string.IsNullOrEmpty(_userName) || string.IsNullOrEmpty(_apiKey))
            {
                return; // Basit doğrulama, boş bırakılamaz
            }

            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_settings.json");
                
                var data = new { UserName = _userName, ApiKey = _apiKey };
                string json = System.Text.Json.JsonSerializer.Serialize(data);
                File.WriteAllText(path, json);
            }
            catch (Exception ex) { Console.WriteLine("Save Error: " + ex.Message); }

            var overlay = this.FindControl<Border>("LoginOverlay");
            if (overlay != null) overlay.IsVisible = false;

            // Karşılama mesajını şimdi atalım
            string greeting = $"Merhaba {_userName}! Ben RoKiSim yapay zeka asistanı Jarvis. Fanuc robot ile ilgili bir arıza mı var, yoksa üretim hattında bir sorun mu gözlemliyorsun?";
            AddMessageToChat("Jarvis", greeting, isUser: false);
            _conversationHistory.Add(new { role = "model", parts = new[] { new { text = greeting } } });
            
            Task.Run(() => SpeakText($"Merhaba {_userName}! Ben Jarvis. Fanuc robot ile ilgili bir arıza mı var, yoksa üretim hattında bir sorun mu gözlemliyorsun?"));
        }

        // ━━━━━━━━━━━━━ PHONE SERVER ━━━━━━━━━━━━━

        private void OnPhoneConnectClick(object? sender, RoutedEventArgs e)
        {
            if (_phoneConnected)
            {
                StopPhoneServer();
                var banner = this.FindControl<Border>("PhoneBanner");
                if (banner != null) banner.IsVisible = false;
                var btn = this.FindControl<Button>("PhoneConnectBtn");
                if (btn != null) { btn.Content = "📱 Telefon Bağla"; btn.Background = new SolidColorBrush(Color.Parse("#7c3aed")); }
                _phoneConnected = false;
                AddMessageToChat("Sistem", "📱 Telefon bağlantısı kapatıldı.", isUser: false);
            }
            else
            {
                StartPhoneServer();
            }
        }

        private void StartPhoneServer()
        {
            try
            {
                // Kill any existing repair photo server
                try
                {
                    var killInfo = new ProcessStartInfo("pkill", "-f repair_photo_server.py")
                    { UseShellExecute = false, CreateNoWindow = true };
                    Process.Start(killInfo)?.WaitForExit(2000);
                }
                catch { }

                string uploadDir = "/tmp/repair_photos";
                if (Directory.Exists(uploadDir))
                {
                    foreach (var f in Directory.GetFiles(uploadDir))
                        _processedPhotos.Add(f);
                }

                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repair_photo_server.py");
                if (!File.Exists(scriptPath))
                    scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../repair_photo_server.py");
                if (!File.Exists(scriptPath))
                    scriptPath = "/home/main/roki_ws/src/RoKiSim_Desktop/repair_photo_server.py";

                if (!File.Exists(scriptPath))
                {
                    AddMessageToChat("Sistem", "❌ repair_photo_server.py bulunamadı!", isUser: false);
                    return;
                }

                _photoServerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python3",
                        Arguments = scriptPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                _photoServerProcess.Start();

                Task.Run(async () =>
                {
                    try
                    {
                        string? line = await _photoServerProcess.StandardOutput.ReadLineAsync();
                        if (line != null && line.StartsWith("REPAIR_PHOTO_SERVER_URL="))
                        {
                            _phoneUrl = line.Split("=", 2)[1].Trim();
                            
                            Dispatcher.UIThread.Post(() =>
                            {
                                var urlText = this.FindControl<SelectableTextBlock>("PhoneUrlText");
                                if (urlText != null) urlText.Text = _phoneUrl;
                                var banner = this.FindControl<Border>("PhoneBanner");
                                if (banner != null) banner.IsVisible = true;
                                var btn = this.FindControl<Button>("PhoneConnectBtn");
                                if (btn != null) { btn.Content = "📱 Bağlantıyı Kes"; btn.Background = new SolidColorBrush(Color.Parse("#dc2626")); }
                                _phoneConnected = true;
                                
                                AddMessageToChat("Sistem", $"📱 Telefon bağlantısı hazır!\nTelefonundan tarayıcıyı aç ve şu adrese git:\n\n{_phoneUrl}", isUser: false);
                            });
                        }
                    }
                    catch { }
                });

                _pollCts = new CancellationTokenSource();
                Task.Run(() => PollForNewPhotos(_pollCts.Token));
            }
            catch (Exception ex)
            {
                AddMessageToChat("Sistem", $"❌ Sunucu başlatılamadı: {ex.Message}", isUser: false);
            }
        }

        private void StopPhoneServer()
        {
            try
            {
                _pollCts?.Cancel();
                if (_photoServerProcess != null && !_photoServerProcess.HasExited)
                {
                    _photoServerProcess.Kill();
                    _photoServerProcess.Dispose();
                }
                _photoServerProcess = null;
            }
            catch { }
        }

        private async Task PollForNewPhotos(CancellationToken ct)
        {
            string uploadDir = "/tmp/repair_photos";
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1200, ct);
                    
                    if (!Directory.Exists(uploadDir)) continue;
                    
                    foreach (var filePath in Directory.GetFiles(uploadDir))
                    {
                        // Skip description text files
                        if (filePath.EndsWith(".txt")) continue;
                        if (_processedPhotos.Contains(filePath)) continue;
                        _processedPhotos.Add(filePath);

                        await Task.Delay(500, ct);

                        string localPath = filePath;
                        
                        // Read companion description file if exists
                        string description = "";
                        string descPath = localPath + ".txt";
                        if (File.Exists(descPath))
                        {
                            description = File.ReadAllText(descPath).Trim();
                            _processedPhotos.Add(descPath);
                        }
                        
                        Bitmap? bmp = null;
                        try
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                using (var stream = File.OpenRead(localPath))
                                {
                                    bmp = new Bitmap(stream);
                                }
                            });
                        }
                        catch { continue; }

                        string chatMsg = string.IsNullOrEmpty(description) ? "Fotoğraf gönderildi" : description;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            AddMessageToChat("📱 Telefondan", chatMsg, isUser: true, image: bmp);
                        });

                        if (!string.IsNullOrEmpty(_apiKey))
                        {
                            string prompt = string.IsNullOrEmpty(description) 
                                ? "Bu fotoğrafı incele. Arızayı veya parçayı tanımla ve tamir için adım adım ne yapmam gerektiğini söyle."
                                : description;
                            SetThinking(true, "Jarvis fotoğrafı inceliyor...");
                            await ProcessWithGemini(prompt, localPath);
                            SetThinking(false);
                        }
                        else
                        {
                            AddMessageToChat("Jarvis", "Fotoğrafı aldım ama API anahtarı olmadan analiz yapamıyorum.", isUser: false);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine("Photo poll error: " + ex.Message);
                }
            }
        }

        // ━━━━━━━━━━━━━ THINKING INDICATOR ━━━━━━━━━━━━━

        private void SetThinking(bool thinking, string message = "Jarvis düşünüyor...")
        {
            Dispatcher.UIThread.Post(() =>
            {
                var indicator = this.FindControl<Border>("ThinkingIndicator");
                var text = this.FindControl<TextBlock>("ThinkingText");
                if (indicator != null) indicator.IsVisible = thinking;
                if (text != null) text.Text = message;
            });
        }

        // ━━━━━━━━━━━━━ CHAT UI ━━━━━━━━━━━━━

        private void OnClearChatClick(object? sender, RoutedEventArgs e)
        {
            var chatPanel = this.FindControl<StackPanel>("ChatPanel");
            chatPanel?.Children.Clear();
            _conversationHistory.Clear();
            
            string greeting = "Sohbet temizlendi. Yeni bir tamir oturumuna başlayalım! Ne tamir ediyoruz?";
            AddMessageToChat("Jarvis", greeting, isUser: false);
            _conversationHistory.Add(new { role = "model", parts = new[] { new { text = greeting } } });
        }

        private void AddMessageToChat(string sender, string message, bool isUser, Bitmap? image = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var chatPanel = this.FindControl<StackPanel>("ChatPanel");
                var scrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer");

                // Determine colors based on sender type
                IBrush bgBrush;
                if (isUser)
                    bgBrush = new SolidColorBrush(Color.Parse("#1e3a5f"));
                else if (sender == "Sistem")
                    bgBrush = new SolidColorBrush(Color.Parse("#1c2128"));
                else
                    bgBrush = new SolidColorBrush(Color.Parse("#21262d"));

                var bubbleBorder = new Border
                {
                    CornerRadius = new CornerRadius(isUser ? 12 : 2, 12, 12, isUser ? 2 : 12),
                    Padding = new Avalonia.Thickness(12),
                    Margin = new Avalonia.Thickness(isUser ? 60 : 0, 2, isUser ? 0 : 60, 2),
                    Background = bgBrush,
                    HorizontalAlignment = isUser ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left,
                    BorderBrush = new SolidColorBrush(Color.Parse("#30363d")),
                    BorderThickness = new Avalonia.Thickness(1)
                };

                var contentStack = new StackPanel { Spacing = 4 };

                // Sender label with color
                string senderColor = isUser ? "#58a6ff" : (sender == "Sistem" ? "#f0883e" : "#a78bfa");
                contentStack.Children.Add(new TextBlock
                {
                    Text = sender,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse(senderColor)),
                    FontSize = 11
                });

                if (image != null)
                {
                    contentStack.Children.Add(new Image
                    {
                        Source = image,
                        MaxHeight = 220,
                        Stretch = Stretch.Uniform,
                        Margin = new Avalonia.Thickness(0, 4)
                    });
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    contentStack.Children.Add(new TextBlock
                    {
                        Text = message,
                        Foreground = new SolidColorBrush(Color.Parse("#e6edf3")),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13,
                        LineHeight = 20
                    });
                }

                // Timestamp
                contentStack.Children.Add(new TextBlock
                {
                    Text = DateTime.Now.ToString("HH:mm"),
                    Foreground = new SolidColorBrush(Color.Parse("#484f58")),
                    FontSize = 9,
                    HorizontalAlignment = isUser ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left
                });

                bubbleBorder.Child = contentStack;
                chatPanel?.Children.Add(bubbleBorder);

                DispatcherTimer.RunOnce(() => scrollViewer?.ScrollToEnd(), TimeSpan.FromMilliseconds(50));
            });
        }

        // ━━━━━━━━━━━━━ LOCAL FILE UPLOAD ━━━━━━━━━━━━━

        private async void OnUploadImageClick(object? sender, RoutedEventArgs e)
        {
            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Tamir Edilecek Parçanın Fotoğrafını Seç",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" }
                    }
                }
            };

            var result = await this.StorageProvider.OpenFilePickerAsync(options);
            if (result != null && result.Count > 0)
            {
                _selectedImagePath = result[0].Path.LocalPath;
                var previewBorder = this.FindControl<Border>("ImagePreviewBorder");
                var previewImage = this.FindControl<Image>("PreviewImage");
                
                try
                {
                    if (previewImage != null)
                    {
                        using (var stream = File.OpenRead(_selectedImagePath))
                        {
                            previewImage.Source = new Bitmap(stream);
                        }
                    }
                    if (previewBorder != null) previewBorder.IsVisible = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Image Load Error: " + ex.Message);
                }
            }
        }

        private void OnRemoveImageClick(object? sender, RoutedEventArgs e)
        {
            _selectedImagePath = null;
            var previewBorder = this.FindControl<Border>("ImagePreviewBorder");
            if (previewBorder != null) previewBorder.IsVisible = false;
        }

        private void OnInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers != Avalonia.Input.KeyModifiers.Shift)
            {
                e.Handled = true;
                OnSendClick(null, null!);
            }
        }

        private async void OnSendClick(object? sender, RoutedEventArgs e)
        {
            var inputBox = this.FindControl<TextBox>("InputTextBox");
            if (inputBox == null) return;

            string text = inputBox.Text?.Trim() ?? "";
            string? imgPath = _selectedImagePath;

            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(imgPath)) return;

            inputBox.Text = "";
            OnRemoveImageClick(null, null!);

            Bitmap? bmp = null;
            if (!string.IsNullOrEmpty(imgPath))
            {
                try
                {
                    using (var stream = File.OpenRead(imgPath))
                        bmp = new Bitmap(stream);
                }
                catch { }
            }

            AddMessageToChat("Sen", text, isUser: true, image: bmp);

            if (string.IsNullOrEmpty(_apiKey))
            {
                AddMessageToChat("Jarvis", "⚠️ API anahtarı (api_key.txt) bulunamadı.\nLütfen projenin ana dizinine api_key.txt dosyasını ekleyin.", isUser: false);
                return;
            }

            var sendBtn = this.FindControl<Button>("SendButton");
            if (sendBtn != null) sendBtn.IsEnabled = false;
            SetThinking(true, imgPath != null ? "Jarvis fotoğrafı inceliyor..." : "Jarvis düşünüyor...");

            try
            {
                await ProcessWithGemini(text, imgPath);
            }
            finally
            {
                if (sendBtn != null) sendBtn.IsEnabled = true;
                SetThinking(false);
            }
        }

        // ━━━━━━━━━━━━━ GEMINI API (Multi-Turn) ━━━━━━━━━━━━━

        private async Task ProcessWithGemini(string promptText, string? imagePath)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";

            // Build the user message parts
            var userParts = new List<object>();

            if (!string.IsNullOrEmpty(imagePath))
            {
                byte[] imgBytes = await File.ReadAllBytesAsync(imagePath);
                string base64 = Convert.ToBase64String(imgBytes);
                string mimeType = imagePath.EndsWith("png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                
                userParts.Add(new
                {
                    inline_data = new { mime_type = mimeType, data = base64 }
                });
            }

            if (!string.IsNullOrEmpty(promptText))
            {
                userParts.Add(new { text = promptText });
            }
            else if (!string.IsNullOrEmpty(imagePath))
            {
                // If only image, add default analysis prompt
                userParts.Add(new { text = "Bu fotoğrafı incele. Arızayı veya parçayı tanımla ve tamir için adım adım ne yapmam gerektiğini söyle." });
            }

            // Add user turn to history
            _conversationHistory.Add(new { role = "user", parts = userParts });

            // System instruction
            string safeName = string.IsNullOrEmpty(_userName) ? "Mühendis" : _userName;
            string sysInstruction = $"Sen RoKiSim Fabrikasının akıllı yapay zeka asistanı Jarvis'sin. Karşındaki mühendisin adı: {safeName}. Ona her zaman ismiyle hitap et ve profesyonel, yardımsever ancak 'Tony Stark'ın asistanı' gibi sofistike bir ton kullan. Her zaman Türkçe konuş. Kullanıcı Fanuc endüstriyel robot kollarını onarıyor, üretim hattındaki cihazlarla ilgileniyor veya senden hata logları/fotoğraflar ile ilgili analiz istiyor. Fotoğraf gönderilirse fotoğraftaki parçayı, PLC arızasını veya robot kolu kırığını detaylıca analiz et. Adım adım, net tamir talimatları ver. Gerekli teknik aletleri listele ve endüstriyel güvenlik uyarılarını kesinlikle belirt.";

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = sysInstruction } } },
                contents = _conversationHistory
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                string responseStr = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using (JsonDocument doc = JsonDocument.Parse(responseStr))
                    {
                        var root = doc.RootElement;
                        var textResponse = root.GetProperty("candidates")[0]
                            .GetProperty("content").GetProperty("parts")[0]
                            .GetProperty("text").GetString() ?? "Üzgünüm, cevap üretemedim.";
                        
                        AddMessageToChat("Jarvis", textResponse, isUser: false);
                        
                        // Add model response to conversation history
                        _conversationHistory.Add(new { role = "model", parts = new[] { new { text = textResponse } } });
                        
                        // Keep history manageable (last 20 turns)
                        if (_conversationHistory.Count > 20)
                            _conversationHistory.RemoveRange(0, _conversationHistory.Count - 20);
                        
                        if (!string.IsNullOrEmpty(textResponse))
                            Task.Run(() => SpeakText(textResponse));
                    }
                }
                else
                {
                    // Remove the failed user message from history
                    if (_conversationHistory.Count > 0)
                        _conversationHistory.RemoveAt(_conversationHistory.Count - 1);

                    string errMsg;
                    if ((int)response.StatusCode == 429)
                        errMsg = "⚠️ API kota limiti aşıldı. Biraz bekleyip tekrar deneyin.";
                    else if ((int)response.StatusCode == 400)
                        errMsg = "⚠️ İstek hatası. Çok büyük bir görsel olabilir, daha küçük bir fotoğraf deneyin.";
                    else
                        errMsg = $"⚠️ API Hatası ({response.StatusCode}). Lütfen tekrar deneyin.";
                    AddMessageToChat("Jarvis", errMsg, isUser: false);
                }
            }
            catch (Exception ex)
            {
                // Remove the failed user message from history
                if (_conversationHistory.Count > 0)
                    _conversationHistory.RemoveAt(_conversationHistory.Count - 1);

                AddMessageToChat("Jarvis", $"⚠️ Bağlantı hatası: {ex.Message}", isUser: false);
            }
        }

        // ━━━━━━━━━━━━━ TTS ━━━━━━━━━━━━━

        private void SpeakText(string text)
        {
            try
            {
                string safeText = text.Replace("\"", "").Replace("'", "").Replace("`", "");
                if (safeText.Length > 600) safeText = safeText.Substring(0, 600) + "...";
                
                string cmd = $"python3 -m edge_tts --voice tr-TR-AhmetNeural --text \"{safeText}\" --write-media /tmp/tamir_tts.mp3 && /usr/bin/ffplay -nodisp -autoexit -loglevel quiet /tmp/tamir_tts.mp3";
                
                var processInfo = new ProcessStartInfo("bash", $"-c '{cmd}'")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var process = Process.Start(processInfo);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("TTS Error: " + ex.Message);
            }
        }
    }
}
