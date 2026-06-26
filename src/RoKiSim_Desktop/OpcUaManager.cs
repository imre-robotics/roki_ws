using System;
using System.Threading;
using System.Threading.Tasks;
using Opc.UaFx;
using Opc.UaFx.Client;

namespace RoKiSim_Desktop
{
    public class OpcUaManager
    {
        private OpcClient? _client;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        // Callbacks for updating UI
        public Action<bool>? OnConnectionStatusChanged { get; set; }
        public Action<double[]>? OnCommandReceived { get; set; } // J1, J2, J3, J4, J5, J6, J7, Pinch

        // Shared state for writing to PLC
        public double ActualJ1 { get; set; }
        public double ActualJ2 { get; set; }
        public double ActualJ3 { get; set; }
        public double ActualJ4 { get; set; }
        public double ActualJ5 { get; set; }
        public double ActualJ6 { get; set; }
        public double ActualJ7 { get; set; }
        public double ActualPinch { get; set; }
        public double ActualX { get; set; }
        public double ActualY { get; set; }
        public double ActualZ { get; set; }

        public OpcUaManager()
        {
        }

        public void Connect(string url)
        {
            try
            {
                _client = new OpcClient(url);
                _client.Connect();
                
                OnConnectionStatusChanged?.Invoke(true);

                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => OpcLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Console.WriteLine("OPC Connect Error: " + ex.Message);
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _loopTask?.Wait(1000);
                
                if (_client != null && _client.State == OpcClientState.Connected)
                {
                    _client.Disconnect();
                }
            }
            catch { }
            finally
            {
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        private void SafeWrite(string nodeId, float value)
        {
            if (_client == null || _client.State != OpcClientState.Connected) return;
            try { _client.WriteNode(nodeId, value); } catch { /* Ignore missing nodes during dev */ }
        }

        private float SafeRead(string nodeId, float fallback = 0f)
        {
            if (_client == null || _client.State != OpcClientState.Connected) return fallback;
            try { return _client.ReadNode(nodeId).As<float>(); } catch { return fallback; }
        }

        private void OpcLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_client == null || _client.State != OpcClientState.Connected)
                        break;

                    // Write to PLC (Telemetry)
                    SafeWrite("ns=2;s=Robot/Status/Actual_J1", (float)ActualJ1);
                    SafeWrite("ns=2;s=Robot/Status/Actual_J2", (float)ActualJ2);
                    SafeWrite("ns=2;s=Robot/Status/Actual_J3", (float)ActualJ3);
                    SafeWrite("ns=2;s=Robot/Status/Actual_J4", (float)ActualJ4);
                    SafeWrite("ns=2;s=Robot/Status/Actual_J5", (float)ActualJ5);
                    SafeWrite("ns=2;s=Robot/Status/Actual_J6", (float)ActualJ6);
                    SafeWrite("ns=2;s=Robot/Status/Actual_J7", (float)ActualJ7);
                    SafeWrite("ns=2;s=Robot/Status/Actual_Gripper", (float)ActualPinch);

                    SafeWrite("ns=2;s=Robot/Status/Actual_X", (float)ActualX);
                    SafeWrite("ns=2;s=Robot/Status/Actual_Y", (float)ActualY);
                    SafeWrite("ns=2;s=Robot/Status/Actual_Z", (float)ActualZ);

                    // Read from PLC (Commands)
                    var cmdJ1 = SafeRead("ns=2;s=Robot/Command/J1", (float)ActualJ1);
                    var cmdJ2 = SafeRead("ns=2;s=Robot/Command/J2", (float)ActualJ2);
                    var cmdJ3 = SafeRead("ns=2;s=Robot/Command/J3", (float)ActualJ3);
                    var cmdJ4 = SafeRead("ns=2;s=Robot/Command/J4", (float)ActualJ4);
                    var cmdJ5 = SafeRead("ns=2;s=Robot/Command/J5", (float)ActualJ5);
                    var cmdJ6 = SafeRead("ns=2;s=Robot/Command/J6", (float)ActualJ6);
                    var cmdJ7 = SafeRead("ns=2;s=Robot/Command/J7_Track", (float)ActualJ7);
                    var cmdGrip = SafeRead("ns=2;s=Robot/Command/Gripper", (float)ActualPinch);

                    double[] commands = new double[] { cmdJ1, cmdJ2, cmdJ3, cmdJ4, cmdJ5, cmdJ6, cmdJ7, cmdGrip };
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        OnCommandReceived?.Invoke(commands);
                    });

                    Thread.Sleep(50); // 20Hz Update Rate
                }
                catch (Exception ex)
                {
                    Console.WriteLine("OPC Loop Error: " + ex.Message);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        OnConnectionStatusChanged?.Invoke(false);
                    });
                    break;
                }
            }
        }
    }
}
