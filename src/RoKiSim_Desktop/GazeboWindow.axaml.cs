using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RoKiSim_Desktop
{
    public partial class GazeboWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellation;
        private bool _isRunning = false;

        public GazeboWindow()
        {
            InitializeComponent();
            this.Opened += GazeboWindow_Opened;
            this.Closing += GazeboWindow_Closing;
        }

        private void GazeboWindow_Opened(object? sender, EventArgs e)
        {
            _cancellation = new CancellationTokenSource();
            _isRunning = true;
            Task.Run(() => ReceiveCameraFeed(_cancellation.Token));
        }

        private void GazeboWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _isRunning = false;
            _cancellation?.Cancel();
            _stream?.Close();
            _client?.Close();
        }

        private async Task ReceiveCameraFeed(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_client == null || !_client.Connected)
                    {
                        Dispatcher.UIThread.Post(() => txtStatus.Text = "🔴 Gazebo Kamerasına Bağlanılıyor...");
                        _client = new TcpClient();
                        await _client.ConnectAsync("127.0.0.1", 8003, token);
                        _stream = _client.GetStream();
                        Dispatcher.UIThread.Post(() => txtStatus.Text = "🟢 Gazebo Bağlantısı Aktif");
                    }

                    // Read 4-byte size header
                    byte[] sizeBytes = new byte[4];
                    int read = await ReadExactAsync(_stream, sizeBytes, 4, token);
                    if (read != 4) throw new Exception("Disconnected");

                    int size = BitConverter.ToInt32(sizeBytes, 0);
                    if (size > 0 && size < 10000000) // Sanity check (max 10MB frame)
                    {
                        byte[] frameBytes = new byte[size];
                        int frameRead = await ReadExactAsync(_stream, frameBytes, size, token);
                        if (frameRead != size) throw new Exception("Disconnected");

                        // Update Image
                        using (var ms = new MemoryStream(frameBytes))
                        {
                            var bitmap = new Bitmap(ms);
                            Dispatcher.UIThread.Post(() => {
                                imgCamera.Source = bitmap;
                            });
                        }
                    }
                }
                catch (Exception)
                {
                    _stream?.Close();
                    _client?.Close();
                    _client = null;
                    if (!token.IsCancellationRequested)
                        await Task.Delay(1000, token); // Wait before reconnecting
                }
            }
        }

        private async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int length, CancellationToken token)
        {
            int totalRead = 0;
            while (totalRead < length && !token.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalRead, length - totalRead, token);
                if (bytesRead == 0) return totalRead;
                totalRead += bytesRead;
            }
            return totalRead;
        }
    }
}
