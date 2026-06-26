using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoKiSim_Desktop
{
    public enum McuConnectionType
    {
        TcpWifi,
        SerialUsb
    }

    public class McuManager
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _tcpStream;
        private SerialPort? _serialPort;

        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        public Action<bool>? OnConnectionStatusChanged { get; set; }

        public McuConnectionType ConnectionType { get; set; } = McuConnectionType.TcpWifi;

        // Shared state for writing to MCU
        public double ActualJ1 { get; set; }
        public double ActualJ2 { get; set; }
        public double ActualJ3 { get; set; }
        public double ActualJ4 { get; set; }
        public double ActualJ5 { get; set; }
        public double ActualJ6 { get; set; }
        public double ActualJ7 { get; set; }
        public double ActualPinch { get; set; }

        public void Connect(string address)
        {
            try
            {
                if (ConnectionType == McuConnectionType.TcpWifi)
                {
                    string[] parts = address.Split(':');
                    string ip = parts[0];
                    int port = parts.Length > 1 ? int.Parse(parts[1]) : 8080;

                    _tcpClient = new TcpClient();
                    _tcpClient.Connect(ip, port);
                    _tcpStream = _tcpClient.GetStream();
                }
                else
                {
                    _serialPort = new SerialPort(address, 115200);
                    _serialPort.Open();
                }

                OnConnectionStatusChanged?.Invoke(true);

                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => McuLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Console.WriteLine("MCU Connect Error: " + ex.Message);
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _loopTask?.Wait(1000);

                _tcpStream?.Close();
                _tcpClient?.Close();

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch { }
            finally
            {
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        private void McuLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Format: J1,J2,J3,J4,J5,J6,J7,Pinch
                    string payload = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0:F1},{1:F1},{2:F1},{3:F1},{4:F1},{5:F1},{6:F1},{7:F1}\n",
                        ActualJ1, ActualJ2, ActualJ3, ActualJ4, ActualJ5, ActualJ6, ActualJ7, ActualPinch);

                    byte[] data = Encoding.ASCII.GetBytes(payload);

                    if (ConnectionType == McuConnectionType.TcpWifi && _tcpStream != null)
                    {
                        _tcpStream.Write(data, 0, data.Length);
                    }
                    else if (ConnectionType == McuConnectionType.SerialUsb && _serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Write(data, 0, data.Length);
                    }

                    Thread.Sleep(50); // 20 Hz transmission rate
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MCU Loop Error: " + ex.Message);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        OnConnectionStatusChanged?.Invoke(false);
                    });
                    break;
                }
            }
        }
    }
}
