using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;

namespace RoKiSim_Desktop
{
    public partial class MainWindow : Window
    {
        private RobotRenderer? robotRenderer;
        private OpcUaManager _opcUaManager;
        private McuManager _mcuManager;

        // Telemetry UI Caches
        private TextBlock? _posXText, _posYText, _posZText, _rollText, _pitchText, _yawText;
        private TextBlock? _dhTheta1, _dhTheta2, _dhTheta3, _dhTheta4, _dhTheta5, _dhTheta6, _dhTheta7;

        // Control Caches
        private Slider? _j1_s, _j2_s, _j3_s, _j4_s, _j5_s, _j6_s, _j7_s;
        private NumericUpDown? _trgX, _trgY, _trgZ;

        private System.Net.Sockets.TcpClient? _gazeboJointClient;
        private System.Net.Sockets.NetworkStream? _gazeboJointStream;

        public MainWindow()
        {
            InitializeComponent();

            robotRenderer = this.FindControl<RobotRenderer>("ModelCanvas");
            TryLoadModels();
            SetupSliders();

            // Init Telemetry Caches
            _posXText = this.FindControl<TextBlock>("posXText");
            _posYText = this.FindControl<TextBlock>("posYText");
            _posZText = this.FindControl<TextBlock>("posZText");
            _rollText = this.FindControl<TextBlock>("rollText");
            _pitchText = this.FindControl<TextBlock>("pitchText");
            _yawText = this.FindControl<TextBlock>("yawText");
            
            _dhTheta1 = this.FindControl<TextBlock>("dhTheta1");
            _dhTheta2 = this.FindControl<TextBlock>("dhTheta2");
            _dhTheta3 = this.FindControl<TextBlock>("dhTheta3");
            _dhTheta4 = this.FindControl<TextBlock>("dhTheta4");
            _dhTheta5 = this.FindControl<TextBlock>("dhTheta5");
            _dhTheta6 = this.FindControl<TextBlock>("dhTheta6");
            _dhTheta7 = this.FindControl<TextBlock>("dhTheta7");

            _j1_s = this.FindControl<Slider>("j1_s");
            _j2_s = this.FindControl<Slider>("j2_s");
            _j3_s = this.FindControl<Slider>("j3_s");
            _j4_s = this.FindControl<Slider>("j4_s");
            _j5_s = this.FindControl<Slider>("j5_s");
            _j6_s = this.FindControl<Slider>("j6_s");
            _j7_s = this.FindControl<Slider>("j7_s");

            _trgX = this.FindControl<NumericUpDown>("trgX");
            _trgY = this.FindControl<NumericUpDown>("trgY");
            _trgZ = this.FindControl<NumericUpDown>("trgZ");

            System.Threading.Tasks.Task.Run(GazeboJointSyncLoop);
            System.Threading.Tasks.Task.Run(UdpTelemetryServer);
        }

        private async System.Threading.Tasks.Task UdpTelemetryServer()
        {
            try {
                using var udp = new System.Net.Sockets.UdpClient(8006);
                while (true) {
                    var res = await udp.ReceiveAsync();
                    string data = System.Text.Encoding.UTF8.GetString(res.Buffer);
                    var vals = data.Split(',');
                    if (vals.Length >= 8) {
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            if (_j1_s != null) _j1_s.Value = double.Parse(vals[1], System.Globalization.CultureInfo.InvariantCulture) * 180.0 / Math.PI;
                            if (_j2_s != null) _j2_s.Value = double.Parse(vals[2], System.Globalization.CultureInfo.InvariantCulture) * 180.0 / Math.PI;
                            if (_j3_s != null) _j3_s.Value = double.Parse(vals[3], System.Globalization.CultureInfo.InvariantCulture) * 180.0 / Math.PI;
                            if (_j4_s != null) _j4_s.Value = double.Parse(vals[4], System.Globalization.CultureInfo.InvariantCulture) * 180.0 / Math.PI;
                            if (_j5_s != null) _j5_s.Value = double.Parse(vals[5], System.Globalization.CultureInfo.InvariantCulture) * 180.0 / Math.PI;
                            if (_j6_s != null) _j6_s.Value = double.Parse(vals[6], System.Globalization.CultureInfo.InvariantCulture) * 180.0 / Math.PI;
                            if (_j7_s != null) _j7_s.Value = double.Parse(vals[0], System.Globalization.CultureInfo.InvariantCulture) * 1000.0;
                            
                            var grip_s = this.FindControl<Slider>("grip_s");
                            if (grip_s != null) {
                                double grip_m = double.Parse(vals[7], System.Globalization.CultureInfo.InvariantCulture);
                                grip_s.Value = (1.0 - (grip_m / 0.06)) * 125.0;
                            }
                        });
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Telemetry Error: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task GazeboJointSyncLoop()
        {
            while (true)
            {
                try
                {
                    if (_gazeboJointClient == null || !_gazeboJointClient.Connected)
                    {
                        _gazeboJointClient = new System.Net.Sockets.TcpClient();
                        await _gazeboJointClient.ConnectAsync("127.0.0.1", 8004);
                        _gazeboJointStream = _gazeboJointClient.GetStream();
                    }

                    double j1=0, j2=0, j3=0, j4=0, j5=0, j6=0, j7=0, grip=0;
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        if (robotRenderer != null) {
                            j1 = robotRenderer.J1 * Math.PI / 180.0;
                            j2 = robotRenderer.J2 * Math.PI / 180.0;
                            j3 = robotRenderer.J3 * Math.PI / 180.0;
                            j4 = robotRenderer.J4 * Math.PI / 180.0;
                            j5 = robotRenderer.J5 * Math.PI / 180.0;
                            j6 = robotRenderer.J6 * Math.PI / 180.0;
                            j7 = robotRenderer.J7 / 1000.0; // mm to meters
                            // Map 0-125 UI range to 0.06-0m for each finger (inverted)
                            grip = (1.0 - (robotRenderer.JawGap / 125.0)) * 0.06;
                        }
                    });

                    if (_gazeboJointStream != null)
                    {
                        string data = string.Format(System.Globalization.CultureInfo.InvariantCulture, 
                            "{0},{1},{2},{3},{4},{5},{6},{7}\n", j1, j2, j3, j4, j5, j6, j7, grip);
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
                        await _gazeboJointStream.WriteAsync(bytes, 0, bytes.Length);
                    }
                }
                catch
                {
                    _gazeboJointStream?.Close();
                    _gazeboJointClient?.Close();
                    _gazeboJointClient = null;
                }
                await System.Threading.Tasks.Task.Delay(33); // ~30 FPS
            }
        }

        private double SmoothStep(double current, double target, double dt, double speed)
        {
            double diff = target - current;
            double step = speed * dt;
            if (Math.Abs(diff) <= step) return target;
            return current + Math.Sign(diff) * step;
        }

        private void SetupSliders()
        {
            var j7 = this.FindControl<Slider>("j7_s");
            var j1 = this.FindControl<Slider>("j1_s");
            var j2 = this.FindControl<Slider>("j2_s");
            var j3 = this.FindControl<Slider>("j3_s");
            var j4 = this.FindControl<Slider>("j4_s");
            var j5 = this.FindControl<Slider>("j5_s");
            var j6 = this.FindControl<Slider>("j6_s");
            var grip = this.FindControl<Slider>("grip_s");
            var cam = this.FindControl<Slider>("cam_slider");

            if (j7 != null) j7.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { UpdateTelemetry(); } };
            if (j1 != null) j1.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { UpdateTelemetry(); } };
            if (j2 != null) j2.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { UpdateTelemetry(); } };
            if (j3 != null) j3.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { UpdateTelemetry(); } };
            if (j4 != null) j4.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { UpdateTelemetry(); } };
            if (j5 != null) j5.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { UpdateTelemetry(); } };
            if (j6 != null) j6.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { UpdateTelemetry(); } };
            if (grip != null) grip.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { } };
            if (cam != null) cam.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty) { } };
            
            var uiSmoothTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            uiSmoothTimer.Tick += (s, e) => {
                if (robotRenderer != null) {
                    double dt = 0.02; // 20ms
                    double speed_j7 = 200.0;
                    double speed_rad = 34.0;
                    
                    if (j7 != null) robotRenderer.J7 = SmoothStep(robotRenderer.J7, j7.Value, dt, speed_j7);
                    if (j1 != null) robotRenderer.J1 = SmoothStep(robotRenderer.J1, j1.Value, dt, speed_rad);
                    if (j2 != null) robotRenderer.J2 = SmoothStep(robotRenderer.J2, j2.Value, dt, speed_rad);
                    if (j3 != null) robotRenderer.J3 = SmoothStep(robotRenderer.J3, j3.Value, dt, speed_rad);
                    if (j4 != null) robotRenderer.J4 = SmoothStep(robotRenderer.J4, j4.Value, dt, speed_rad);
                    if (j5 != null) robotRenderer.J5 = SmoothStep(robotRenderer.J5, j5.Value, dt, speed_rad);
                    if (j6 != null) robotRenderer.J6 = SmoothStep(robotRenderer.J6, j6.Value, dt, speed_rad);
                    if (grip != null) robotRenderer.JawGap = SmoothStep(robotRenderer.JawGap, grip.Value, dt, 10.0);
                    if (cam != null) robotRenderer.Angle = SmoothStep(robotRenderer.Angle, cam.Value, dt, 100.0);
                    
                    robotRenderer.InvalidateVisual();
                    UpdateTelemetry();
                }
            };
            uiSmoothTimer.Start();
            
            // Set initial values
            if (robotRenderer != null)
            {
                if (j7 != null) robotRenderer.J7 = j7.Value;
                if (j1 != null) robotRenderer.J1 = j1.Value;
                if (j2 != null) robotRenderer.J2 = j2.Value;
                if (j3 != null) robotRenderer.J3 = j3.Value;
                if (j4 != null) robotRenderer.J4 = j4.Value;
                if (j5 != null) robotRenderer.J5 = j5.Value;
                if (j6 != null) robotRenderer.J6 = j6.Value;
                if (grip != null) robotRenderer.JawGap = grip.Value;
                if (cam != null) robotRenderer.Angle = cam.Value;
                UpdateTelemetry();
            }

            _opcUaManager = new OpcUaManager();
            _opcUaManager.OnConnectionStatusChanged = (connected) =>
            {
                if (btnOpcConnect != null && opcStatus != null)
                {
                    if (connected)
                    {
                        btnOpcConnect.Content = "🔗 BAĞLANTIYI KES";
                        btnOpcConnect.Background = Avalonia.Media.Brushes.DarkRed;
                        opcStatus.Text = "🟢 ONLINE";
                        opcStatus.Foreground = Avalonia.Media.Brushes.LimeGreen;
                    }
                    else
                    {
                        btnOpcConnect.Content = "🔗 BAĞLAN";
                        btnOpcConnect.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2980b9"));
                        opcStatus.Text = "🔴 OFFLINE";
                        opcStatus.Foreground = Avalonia.Media.Brushes.Gray;
                    }
                }
            };

            _opcUaManager.OnCommandReceived = (commands) =>
            {
                // J1, J2, J3, J4, J5, J6, J7, Pinch
                if (j1 != null) j1.Value = commands[0];
                if (j2 != null) j2.Value = commands[1];
                if (j3 != null) j3.Value = commands[2];
                if (j4 != null) j4.Value = commands[3];
                if (j5 != null) j5.Value = commands[4];
                if (j6 != null) j6.Value = commands[5];
                if (j7 != null) j7.Value = commands[6];
                if (robotRenderer != null) robotRenderer.JawGap = commands[7];
                ExecuteJogIK(j1, j2, j3, j4, j5, j6);
            };

            if (btnOpcConnect != null)
            {
                btnOpcConnect.Click += (s, e) => {
                    var txtUrl = this.FindControl<TextBox>("txtOpcUrl");
                    if (btnOpcConnect.Content?.ToString() == "🔗 BAĞLAN")
                    {
                        _opcUaManager.Connect(txtUrl?.Text ?? "opc.tcp://127.0.0.1:4840");
                    }
                    else
                    {
                        _opcUaManager.Disconnect();
                    }
                };
            }

            _mcuManager = new McuManager();
            var btnMcuConnect = this.FindControl<Button>("btnMcuConnect");
            var mcuStatus = this.FindControl<TextBlock>("mcuStatus");
            var cmbMcuType = this.FindControl<ComboBox>("cmbMcuType");
            var txtMcuAddress = this.FindControl<TextBox>("txtMcuAddress");

            _mcuManager.OnConnectionStatusChanged = (connected) =>
            {
                if (btnMcuConnect != null && mcuStatus != null)
                {
                    if (connected)
                    {
                        btnMcuConnect.Content = "🔗 BAĞLANTIYI KES";
                        btnMcuConnect.Background = Avalonia.Media.Brushes.DarkRed;
                        mcuStatus.Text = "🟢 ONLINE";
                        mcuStatus.Foreground = Avalonia.Media.Brushes.LimeGreen;
                    }
                    else
                    {
                        btnMcuConnect.Content = "🔗 BAĞLAN";
                        btnMcuConnect.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8e44ad"));
                        mcuStatus.Text = "🔴 OFFLINE";
                        mcuStatus.Foreground = Avalonia.Media.Brushes.Gray;
                    }
                }
            };

            if (btnMcuConnect != null)
            {
                btnMcuConnect.Click += (s, e) => {
                    if (btnMcuConnect.Content?.ToString() == "🔗 BAĞLAN")
                    {
                        if (cmbMcuType != null && cmbMcuType.SelectedIndex == 0)
                            _mcuManager.ConnectionType = McuConnectionType.TcpWifi;
                        else
                            _mcuManager.ConnectionType = McuConnectionType.SerialUsb;

                        _mcuManager.Connect(txtMcuAddress?.Text ?? "");
                    }
                    else
                    {
                        _mcuManager.Disconnect();
                    }
                };
            }

            var btnConfigGamepad = this.FindControl<Button>("btnConfigGamepad");
            if (btnConfigGamepad != null)
            {
                btnConfigGamepad.Click += (s, e) => {
                    var win = new GamepadConfigWindow();
                    win.ShowDialog(this);
                };
            }

            void WireJogSafe(Button btn, Slider slider, double amount)
            {
                if (btn != null && slider != null)
                    btn.Click += (s, e) => { slider.Value = Math.Clamp(slider.Value + amount, slider.Minimum, slider.Maximum); };
            }

            if (j1 != null) { WireJogSafe(btnJogJ1Minus, j1, -5); WireJogSafe(btnJogJ1Plus, j1, 5); }
            if (j2 != null) { WireJogSafe(btnJogJ2Minus, j2, -5); WireJogSafe(btnJogJ2Plus, j2, 5); }
            if (j3 != null) { WireJogSafe(btnJogJ3Minus, j3, -5); WireJogSafe(btnJogJ3Plus, j3, 5); }
            if (j4 != null) { WireJogSafe(btnJogJ4Minus, j4, -5); WireJogSafe(btnJogJ4Plus, j4, 5); }
            if (j5 != null) { WireJogSafe(btnJogJ5Minus, j5, -5); WireJogSafe(btnJogJ5Plus, j5, 5); }
            if (j6 != null) { WireJogSafe(btnJogJ6Minus, j6, -5); WireJogSafe(btnJogJ6Plus, j6, 5); }

            if (btnExecuteJog != null)
            {
                btnExecuteJog.Click += (s, e) => {
                    ExecuteJogIK(j1, j2, j3, j4, j5, j6);
                };
            }

            if (btnHome != null)
            {
                btnHome.Click += (s, e) => {
                    AnimateToHome(j7, j1, j2, j3, j4, j5, j6);
                };
            }
            if (btnJogXMinus != null) btnJogXMinus.Click += (s, e) => JogCartesian(-10, 0, 0);
            if (btnJogXPlus != null) btnJogXPlus.Click += (s, e) => JogCartesian(10, 0, 0);
            if (btnJogYMinus != null) btnJogYMinus.Click += (s, e) => JogCartesian(0, -10, 0);
            if (btnJogYPlus != null) btnJogYPlus.Click += (s, e) => JogCartesian(0, 10, 0);
            if (btnJogZMinus != null) btnJogZMinus.Click += (s, e) => JogCartesian(0, 0, -10);
            if (btnJogZPlus != null) btnJogZPlus.Click += (s, e) => JogCartesian(0, 0, 10);

            if (btnRec != null)
            {
                btnRec.Click += (s, e) => {
                    ToggleRecording(btnRec, j1, j2, j3, j4, j5, j6);
                };
            }

            if (btnPlay != null)
            {
                btnPlay.Click += (s, e) => {
                    PlayRecordedWaypoints(j1, j2, j3, j4, j5, j6);
                };
            }

            SetupJoystick(joy1Base, joy1Thumb, j1, j2);
            SetupJoystick(joy2Base, joy2Thumb, j3, j4);
            SetupJoystick(joy3Base, joy3Thumb, j5, j6);

            if (btnExportCsv != null)
            {
                btnExportCsv.Click += async (s, e) => {
                    try {
                        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
                        if (topLevel == null) return;
                        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions {
                            Title = "Save Telemetry CSV",
                            DefaultExtension = "csv",
                            SuggestedFileName = "telemetry_full.csv"
                        });
                        if (file != null) {
                            var lines = _continuousTelemetry.Select(w => string.Join(",", w.Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                            await System.IO.File.WriteAllLinesAsync(file.Path.LocalPath, lines);
                            if (txtRecordCount != null) txtRecordCount.Text = "EXPORTED!";
                        }
                    } catch (Exception ex) {
                        Console.WriteLine(ex.Message);
                    }
                };
            }

            bool showAxes = false;
            if (btnShowAxes != null) btnShowAxes.Click += (s, e) => {
                showAxes = !showAxes;
                btnShowAxes.Opacity = showAxes ? 0.5 : 1.0;
                if (robotRenderer != null) {
                    robotRenderer.ShowAxes = showAxes;
                    robotRenderer.InvalidateVisual();
                }
            };

            bool showWp = false;
            if (btnShowWorkspace != null) btnShowWorkspace.Click += (s, e) => {
                showWp = !showWp;
                btnShowWorkspace.Opacity = showWp ? 0.5 : 1.0;
                if (robotRenderer != null) {
                    robotRenderer.ShowWorkspace = showWp;
                    robotRenderer.InvalidateVisual();
                }
            };

            var pnlTorque = this.FindControl<Border>("pnlTorque");
            var pnlVelocity = this.FindControl<Border>("pnlVelocity");
            var pnlPosition = this.FindControl<Border>("pnlPosition");

            var btnTelTork = this.FindControl<Button>("btnTelTork");
            var btnTelTcp = this.FindControl<Button>("btnTelTcp");
            var btnTelKonum = this.FindControl<Button>("btnTelKonum");

            if (btnTelTork != null) btnTelTork.Click += (s, e) => { if (pnlTorque != null) pnlTorque.IsVisible = !pnlTorque.IsVisible; };
            if (btnTelTcp != null) btnTelTcp.Click += (s, e) => { if (pnlVelocity != null) pnlVelocity.IsVisible = !pnlVelocity.IsVisible; };
            if (btnTelKonum != null) btnTelKonum.Click += (s, e) => { if (pnlPosition != null) pnlPosition.IsVisible = !pnlPosition.IsVisible; };

            var btnCloseTorque = this.FindControl<Button>("btnCloseTorque");
            var btnCloseVelocity = this.FindControl<Button>("btnCloseVelocity");
            var btnClosePosition = this.FindControl<Button>("btnClosePosition");

            if (btnCloseTorque != null) btnCloseTorque.Click += (s, e) => { if (pnlTorque != null) pnlTorque.IsVisible = false; };
            if (btnCloseVelocity != null) btnCloseVelocity.Click += (s, e) => { if (pnlVelocity != null) pnlVelocity.IsVisible = false; };
            if (btnClosePosition != null) btnClosePosition.Click += (s, e) => { if (pnlPosition != null) pnlPosition.IsVisible = false; };

            var pnlTorqueHeader = this.FindControl<Grid>("pnlTorqueHeader");
            var pnlVelocityHeader = this.FindControl<Grid>("pnlVelocityHeader");
            var pnlPositionHeader = this.FindControl<Grid>("pnlPositionHeader");

            if (pnlTorque != null && pnlTorqueHeader != null) MakeDraggable(pnlTorque, pnlTorqueHeader);
            if (pnlVelocity != null && pnlVelocityHeader != null) MakeDraggable(pnlVelocity, pnlVelocityHeader);
            if (pnlPosition != null && pnlPositionHeader != null) MakeDraggable(pnlPosition, pnlPositionHeader);

            var toggleAiOverride = this.FindControl<ToggleSwitch>("toggleAiOverride");
            if (toggleAiOverride != null) {
                toggleAiOverride.IsCheckedChanged += (s, e) => {
                    if (toggleAiOverride.IsChecked == true) {
                        StartCV(j1, j2, j3, j4, j5, j6);
                    } else {
                        StopCV();
                    }
                };
            }

            var toggleAIPartner = this.FindControl<ToggleSwitch>("toggleAIPartner");
            if (toggleAIPartner != null) {
                toggleAIPartner.IsCheckedChanged += (s, e) => {
                    if (toggleAIPartner.IsChecked == true) StartAIPartner();
                    else StopAIPartner();
                };
            }

            var btnPartnerSend = this.FindControl<Button>("btnPartnerSend");
            var txtPartnerInput = this.FindControl<TextBox>("txtPartnerInput");
            if (btnPartnerSend != null && txtPartnerInput != null) {
                btnPartnerSend.Click += (s, e) => {
                    string msg = txtPartnerInput.Text ?? "";
                    if (!string.IsNullOrWhiteSpace(msg)) {
                        SendAIMessage(msg);
                        txtPartnerInput.Text = "";
                    }
                };

                txtPartnerInput.KeyDown += (s, e) => {
                    if (e.Key == Avalonia.Input.Key.Enter) {
                        string msg = txtPartnerInput.Text ?? "";
                        if (!string.IsNullOrWhiteSpace(msg)) {
                            SendAIMessage(msg);
                            txtPartnerInput.Text = "";
                        }
                    }
                };
            }

            var btnPartnerMic = this.FindControl<Button>("btnPartnerMic");
            if (btnPartnerMic != null) {
                btnPartnerMic.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (s, e) => {
                    btnPartnerMic.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#c0392b"));
                    btnPartnerMic.Content = "REC";
                    StartRecordingMic();
                }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

                btnPartnerMic.AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, (s, e) => {
                    btnPartnerMic.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e74c3c"));
                    btnPartnerMic.Content = "🎤";
                    StopRecordingMicAndSend();
                }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }

            SetupTour();
            SetupBackgroundColor();
            SetupScripting();
            SetupGamepad();

            SetupGraphs();
        }

        private void MakeDraggable(Border panel, Grid header)
        {
            bool isDragging = false;
            Point startPoint = new Point();
            var transform = new Avalonia.Media.TranslateTransform();
            panel.RenderTransform = transform;

            header.PointerPressed += (s, e) => {
                isDragging = true;
                startPoint = e.GetPosition(this);
                e.Pointer.Capture(header);
            };

            header.PointerReleased += (s, e) => {
                isDragging = false;
                e.Pointer.Capture(null);
            };

            header.PointerMoved += (s, e) => {
                if (!isDragging) return;
                var currentPoint = e.GetPosition(this);
                var dx = currentPoint.X - startPoint.X;
                var dy = currentPoint.Y - startPoint.Y;
                transform.X += dx;
                transform.Y += dy;
                startPoint = currentPoint;
            };
        }

        private void SetupGraphs()
        {
            var polyVelocity = this.FindControl<Polyline>("polyVelocity");
            var polyJ1 = this.FindControl<Polyline>("polyJ1");
            var polyJ2 = this.FindControl<Polyline>("polyJ2");
            var polyJ3 = this.FindControl<Polyline>("polyJ3");
            var polyJ4 = this.FindControl<Polyline>("polyJ4");
            var polyJ5 = this.FindControl<Polyline>("polyJ5");
            var polyJ6 = this.FindControl<Polyline>("polyJ6");

            var barJ1 = this.FindControl<Border>("barJ1");
            var barJ2 = this.FindControl<Border>("barJ2");
            var barJ3 = this.FindControl<Border>("barJ3");
            var barJ4 = this.FindControl<Border>("barJ4");
            var barJ5 = this.FindControl<Border>("barJ5");
            var barJ6 = this.FindControl<Border>("barJ6");

            if (polyVelocity != null) polyVelocity.Points = new List<Point>();
            if (polyJ1 != null) polyJ1.Points = new List<Point>();
            if (polyJ2 != null) polyJ2.Points = new List<Point>();
            if (polyJ3 != null) polyJ3.Points = new List<Point>();
            if (polyJ4 != null) polyJ4.Points = new List<Point>();
            if (polyJ5 != null) polyJ5.Points = new List<Point>();
            if (polyJ6 != null) polyJ6.Points = new List<Point>();

            DispatcherTimer graphTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            double graphTime = 0;
            double lastX = 0, lastY = 0, lastZ = 0;

            graphTimer.Tick += (s, e) => {
                if (robotRenderer == null) return;
                
                if (_continuousTelemetry.Count < 500000)
                {
                    _continuousTelemetry.Add(new double[] { graphTime, robotRenderer.J1, robotRenderer.J2, robotRenderer.J3, robotRenderer.J4, robotRenderer.J5, robotRenderer.J6 });
                    if (_continuousTelemetry.Count % 10 == 0)
                    {
                        var txtRecordCount = this.FindControl<TextBlock>("txtRecordCount");
                        if (txtRecordCount != null) txtRecordCount.Text = $"{_continuousTelemetry.Count} KAYIT";
                    }
                }

                if (barJ1 != null) barJ1.Width = Math.Clamp(Math.Abs(robotRenderer.J1), 0, 200);
                if (barJ2 != null) barJ2.Width = Math.Clamp(Math.Abs(robotRenderer.J2), 0, 200);
                if (barJ3 != null) barJ3.Width = Math.Clamp(Math.Abs(robotRenderer.J3), 0, 200);
                if (barJ4 != null) barJ4.Width = Math.Clamp(Math.Abs(robotRenderer.J4), 0, 200);
                if (barJ5 != null) barJ5.Width = Math.Clamp(Math.Abs(robotRenderer.J5), 0, 200);
                if (barJ6 != null) barJ6.Width = Math.Clamp(Math.Abs(robotRenderer.J6), 0, 200);

                var matrices = robotRenderer.GetTransforms();
                if (matrices != null && matrices.Length >= 7) {
                    var flange = matrices[6];
                    double x = flange.M41;
                    double y = flange.M42;
                    double z = flange.M43;
                    
                    double dx = x - lastX; double dy = y - lastY; double dz = z - lastZ;
                    lastX = x; lastY = y; lastZ = z;
                    double vel = Math.Sqrt(dx*dx + dy*dy + dz*dz) / 10.0;
                    
                    AddPoint(polyVelocity, graphTime, vel * 150, 250, 200);
                }

                AddPoint(polyJ1, graphTime, (robotRenderer.J1 + 180) / 360.0 * 40, 260, 40);
                AddPoint(polyJ2, graphTime, (robotRenderer.J2 + 180) / 360.0 * 40, 260, 40);
                AddPoint(polyJ3, graphTime, (robotRenderer.J3 + 180) / 360.0 * 40, 260, 40);
                AddPoint(polyJ4, graphTime, (robotRenderer.J4 + 180) / 360.0 * 40, 260, 40);
                AddPoint(polyJ5, graphTime, (robotRenderer.J5 + 180) / 360.0 * 40, 260, 40);
                AddPoint(polyJ6, graphTime, (robotRenderer.J6 + 180) / 360.0 * 40, 260, 40);

                graphTime += 2;
            };
            graphTimer.Start();
        }

        private void AddPoint(Polyline? poly, double timeX, double valueY, double maxChartWidth, double maxChartHeight)
        {
            if (poly == null) return;
            var pointsList = poly.Points;
            if (pointsList == null) return;
            
            pointsList.Add(new Point(timeX, maxChartHeight - Math.Clamp(valueY, 0, maxChartHeight)));
            
            if (timeX > maxChartWidth) {
                double timeShift = timeX - maxChartWidth;
                while (pointsList.Count > 0 && pointsList[0].X < timeShift) {
                    pointsList.RemoveAt(0);
                }
                if (poly.RenderTransform is Avalonia.Media.TranslateTransform translate) {
                    translate.X = -timeShift;
                } else {
                    poly.RenderTransform = new Avalonia.Media.TranslateTransform(-timeShift, 0);
                }
            }
            
            // Force redraw since modifying the collection directly may not trigger UI update
            poly.InvalidateVisual();
        }

        private void SetupJoystick(Border? baseBorder, Border? thumbBorder, Slider? sx, Slider? sy)
        {
            if (baseBorder == null || thumbBorder == null || sx == null || sy == null) return;

            bool isDragging = false;
            double cx = 32; 
            double cy = 32;

            baseBorder.PointerPressed += (s, e) => { isDragging = true; };
            baseBorder.PointerReleased += (s, e) => {
                isDragging = false;
                thumbBorder.RenderTransform = new Avalonia.Media.TranslateTransform(0, 0);
            };
            baseBorder.PointerMoved += (s, e) => {
                if (!isDragging) return;
                var pt = e.GetPosition(baseBorder);
                double dx = pt.X - cx;
                double dy = pt.Y - cy;

                double mag = Math.Sqrt(dx*dx + dy*dy);
                if (mag > 20) { dx = dx / mag * 20; dy = dy / mag * 20; }

                thumbBorder.RenderTransform = new Avalonia.Media.TranslateTransform(dx, dy);
                
                sx.Value = Math.Clamp(sx.Value + (dx / 20.0 * 2.0), sx.Minimum, sx.Maximum);
                sy.Value = Math.Clamp(sy.Value + (-dy / 20.0 * 2.0), sy.Minimum, sy.Maximum);
            };
        }


        private List<double[]> _recordedWaypoints = new List<double[]>();
        private List<double[]> _continuousTelemetry = new List<double[]>();
        private DispatcherTimer? _playTimer;
        private int _playIndex = 0;
        
        private DispatcherTimer? _recordTimer;
        private bool _isRecording = false;

        private void ToggleRecording(Button btnRec, Slider? j1, Slider? j2, Slider? j3, Slider? j4, Slider? j5, Slider? j6)
        {
            if (!_isRecording)
            {
                _isRecording = true;
                _recordedWaypoints.Clear();
                btnRec.Content = "⏹ STOP REC";
                btnRec.Background = Avalonia.Media.Brushes.DarkRed;
                
                if (_recordTimer != null) { _recordTimer.Stop(); _recordTimer = null; }
                
                _recordTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; 
                _recordTimer.Tick += (s, e) => {
                    _recordedWaypoints.Add(new double[] { 
                        j1?.Value??0, j2?.Value??0, j3?.Value??0, 
                        j4?.Value??0, j5?.Value??0, j6?.Value??0 
                    });
                };
                _recordTimer.Start();
            }
            else
            {
                _isRecording = false;
                if (_recordTimer != null) { _recordTimer.Stop(); _recordTimer = null; }
                btnRec.Content = "🔴 REC";
                btnRec.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#c0392b"));
            }
        }

        private void PlayRecordedWaypoints(Slider? j1, Slider? j2, Slider? j3, Slider? j4, Slider? j5, Slider? j6)
        {
            if (_recordedWaypoints.Count == 0 || j1 == null || j2 == null || j3 == null || j4 == null || j5 == null || j6 == null) return;
            
            _playIndex = 0;
            if (_playTimer != null) { _playTimer.Stop(); _playTimer = null; }
            
            _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _playTimer.Tick += (s, e) => {
                if (_playIndex >= _recordedWaypoints.Count) {
                    _playTimer.Stop(); 
                    _playTimer = null; 
                    return;
                }
                var wp = _recordedWaypoints[_playIndex++];
                j1.Value = wp[0];
                j2.Value = wp[1];
                j3.Value = wp[2];
                j4.Value = wp[3];
                j5.Value = wp[4];
                j6.Value = wp[5];
            };
            _playTimer.Start();
        }

        private void JogCartesian(double dx, double dy, double dz)
        {
            if (robotRenderer == null) return;
            var matrices = robotRenderer.GetTransforms();
            if (matrices == null || matrices.Length < 7) return;

            var flangeMatrix = matrices[6];
            
            double x = flangeMatrix.M41; // mm
            double y = flangeMatrix.M42;
            double z = flangeMatrix.M43;

            if (trgX != null) trgX.Value = (decimal)((x + dx) / 1000.0);
            if (trgY != null) trgY.Value = (decimal)((y + dy) / 1000.0);
            if (trgZ != null) trgZ.Value = (decimal)((z + dz) / 1000.0);

            var j1 = this.FindControl<Slider>("j1_s");
            var j2 = this.FindControl<Slider>("j2_s");
            var j3 = this.FindControl<Slider>("j3_s");
            var j4 = this.FindControl<Slider>("j4_s");
            var j5 = this.FindControl<Slider>("j5_s");
            var j6 = this.FindControl<Slider>("j6_s");
            
            ExecuteJogIK(j1, j2, j3, j4, j5, j6);
        }

        private DispatcherTimer? _homeTimer;

        private void AnimateToHome(Slider? j7, Slider? j1, Slider? j2, Slider? j3, Slider? j4, Slider? j5, Slider? j6)
        {
            if (_homeTimer != null)
            {
                _homeTimer.Stop();
                _homeTimer = null;
            }

            double startJ7 = j7?.Value ?? 0;
            double startJ1 = j1?.Value ?? 0;
            double startJ2 = j2?.Value ?? 0;
            double startJ3 = j3?.Value ?? 0;
            double startJ4 = j4?.Value ?? 0;
            double startJ5 = j5?.Value ?? 0;
            double startJ6 = j6?.Value ?? 0;

            double durationMs = 1500.0;
            int fps = 60;
            double tickMs = 1000.0 / fps;
            int totalFrames = (int)(durationMs / tickMs);
            int currentFrame = 0;

            _homeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(tickMs)
            };

            _homeTimer.Tick += (s, e) =>
            {
                currentFrame++;
                double progress = (double)currentFrame / totalFrames;
                double ease = progress * progress * (3.0 - 2.0 * progress); // Smoothstep

                if (j7 != null) j7.Value = startJ7 * (1.0 - ease);
                if (j1 != null) j1.Value = startJ1 * (1.0 - ease);
                if (j2 != null) j2.Value = startJ2 * (1.0 - ease);
                if (j3 != null) j3.Value = startJ3 * (1.0 - ease);
                if (j4 != null) j4.Value = startJ4 * (1.0 - ease);
                if (j5 != null) j5.Value = startJ5 * (1.0 - ease);
                if (j6 != null) j6.Value = startJ6 * (1.0 - ease);

                if (currentFrame >= totalFrames)
                {
                    _homeTimer.Stop();
                    _homeTimer = null;
                    
                    if (j7 != null) j7.Value = 0;
                    if (j1 != null) j1.Value = 0;
                    if (j2 != null) j2.Value = 0;
                    if (j3 != null) j3.Value = 0;
                    if (j4 != null) j4.Value = 0;
                    if (j5 != null) j5.Value = 0;
                    if (j6 != null) j6.Value = 0;
                }
            };

            _homeTimer.Start();
        }

        private void ExecuteJogIK(Slider? j1, Slider? j2, Slider? j3, Slider? j4, Slider? j5, Slider? j6)
        {
            if (robotRenderer == null || j1 == null || j2 == null || j3 == null || j4 == null || j5 == null || j6 == null) return;
            
            var trgX = _trgX;
            var trgY = _trgY;
            var trgZ = _trgZ;

            if (trgX == null || trgY == null || trgZ == null) return;

            double targetX = Convert.ToDouble(trgX.Value ?? 0) * 1000.0;
            double targetY = Convert.ToDouble(trgY.Value ?? 0) * 1000.0;
            double targetZ = Convert.ToDouble(trgZ.Value ?? 0) * 1000.0;

            double v1 = j1.Value;
            double v2 = j2.Value;
            double v3 = j3.Value;
            double v4 = j4.Value;
            double v5 = j5.Value;
            double v6 = j6.Value;
            double v7 = robotRenderer.J7;

            for (int iter = 0; iter < 15; iter++) 
            {
                double cx, cy, cz;
                GetFlangePos(v1, v2, v3, v4, v5, v6, v7, out cx, out cy, out cz);
                
                double distSq = (targetX - cx)*(targetX - cx) + (targetY - cy)*(targetY - cy) + (targetZ - cz)*(targetZ - cz);
                double dist = Math.Sqrt(distSq);
                if (dist < 1.0) break;

                double step = 0.1;
                double gx, gy, gz;
                
                GetFlangePos(v1 + step, v2, v3, v4, v5, v6, v7, out gx, out gy, out gz);
                double d1 = Math.Sqrt((targetX - gx)*(targetX - gx) + (targetY - gy)*(targetY - gy) + (targetZ - gz)*(targetZ - gz));
                double gradJ1 = (d1 - dist) / step;

                GetFlangePos(v1, v2 + step, v3, v4, v5, v6, v7, out gx, out gy, out gz);
                double d2 = Math.Sqrt((targetX - gx)*(targetX - gx) + (targetY - gy)*(targetY - gy) + (targetZ - gz)*(targetZ - gz));
                double gradJ2 = (d2 - dist) / step;

                GetFlangePos(v1, v2, v3 + step, v4, v5, v6, v7, out gx, out gy, out gz);
                double d3 = Math.Sqrt((targetX - gx)*(targetX - gx) + (targetY - gy)*(targetY - gy) + (targetZ - gz)*(targetZ - gz));
                double gradJ3 = (d3 - dist) / step;

                // Normalize gradient vector to prevent explosive steps
                double gradMag = Math.Sqrt(gradJ1*gradJ1 + gradJ2*gradJ2 + gradJ3*gradJ3);
                if (gradMag > 0.0001)
                {
                    gradJ1 /= gradMag;
                    gradJ2 /= gradMag;
                    gradJ3 /= gradMag;
                }

                double stepSize = Math.Min(dist * 0.1, 1.5); // max 1.5 degree per iter

                v1 = Math.Clamp(v1 - gradJ1 * stepSize, j1.Minimum, j1.Maximum);
                v2 = Math.Clamp(v2 - gradJ2 * stepSize, j2.Minimum, j2.Maximum);
                v3 = Math.Clamp(v3 - gradJ3 * stepSize, j3.Minimum, j3.Maximum);
            }

            // Write to UI exactly once
            if (Math.Abs(j1.Value - v1) > 0.01) j1.Value = v1;
            if (Math.Abs(j2.Value - v2) > 0.01) j2.Value = v2;
            if (Math.Abs(j3.Value - v3) > 0.01) j3.Value = v3;
        }

        private void GetFlangePos(double j1, double j2, double j3, double j4, double j5, double j6, double j7, out double x, out double y, out double z)
        {
            var m0 = System.Numerics.Matrix4x4.CreateTranslation(0, (float)j7, 0);
            var m1 = System.Numerics.Matrix4x4.CreateRotationZ((float)(-j1 * Math.PI / 180.0)) * System.Numerics.Matrix4x4.CreateTranslation(0, 0, 330) * m0;
            var m2 = System.Numerics.Matrix4x4.CreateRotationY((float)(j2 * Math.PI / 180.0)) * System.Numerics.Matrix4x4.CreateTranslation(50, 0, 0) * m1;
            var m3 = System.Numerics.Matrix4x4.CreateRotationY((float)(j3 * Math.PI / 180.0)) * System.Numerics.Matrix4x4.CreateTranslation(0, 0, 330) * m2;
            var m4 = System.Numerics.Matrix4x4.CreateRotationX((float)(-j4 * Math.PI / 180.0)) * System.Numerics.Matrix4x4.CreateTranslation(0, 0, 35) * m3;
            var m5 = System.Numerics.Matrix4x4.CreateRotationY((float)(-j5 * Math.PI / 180.0)) * System.Numerics.Matrix4x4.CreateTranslation(335, 0, 0) * m4;
            var m6 = System.Numerics.Matrix4x4.CreateRotationX((float)(-j6 * Math.PI / 180.0)) * System.Numerics.Matrix4x4.CreateTranslation(80, 0, 0) * m5;

            x = m6.M41;
            y = m6.M42;
            z = m6.M43;
        }

        private void UpdateTelemetry()
        {
            if (robotRenderer == null) return;
            var matrices = robotRenderer.GetTransforms();
            if (matrices == null || matrices.Length < 7) return;

            var flangeMatrix = matrices[6];
            
            // Convert mm to meters for display (since DH params are in mm)
            double x = flangeMatrix.M41 / 1000.0;
            double y = flangeMatrix.M42 / 1000.0;
            double z = flangeMatrix.M43 / 1000.0;

            // Extract Yaw, Pitch, Roll from rotation matrix
            float pitch = (float)Math.Asin(-flangeMatrix.M32);
            float yaw, roll;
            if (Math.Cos(pitch) > 0.0001)
            {
                yaw = (float)Math.Atan2(flangeMatrix.M12, flangeMatrix.M22);
                roll = (float)Math.Atan2(flangeMatrix.M31, flangeMatrix.M33);
            }
            else
            {
                yaw = (float)Math.Atan2(-flangeMatrix.M21, flangeMatrix.M11);
                roll = 0;
            }

            // Convert rad to deg
            yaw = yaw * 180f / (float)Math.PI;
            pitch = pitch * 180f / (float)Math.PI;
            roll = roll * 180f / (float)Math.PI;

            if (_posXText != null) _posXText.Text = $"POS_X: {x:F3}";
            if (_posYText != null) _posYText.Text = $"POS_Y: {y:F3}";
            if (_posZText != null) _posZText.Text = $"POS_Z: {z:F3}";
            if (_rollText != null) _rollText.Text = $"ROLL : {roll,6:F1}°";
            if (_pitchText != null) _pitchText.Text = $"PITCH: {pitch,6:F1}°";
            if (_yawText != null) _yawText.Text = $"YAW  : {yaw,6:F1}°";

            if (_dhTheta1 != null) _dhTheta1.Text = $"{robotRenderer.J1:F1}°";
            if (_dhTheta2 != null) _dhTheta2.Text = $"{-115.8 + robotRenderer.J2:F1}°";
            if (_dhTheta3 != null) _dhTheta3.Text = $"{68.8 + robotRenderer.J3:F1}°";
            if (_dhTheta4 != null) _dhTheta4.Text = $"{11.5 + robotRenderer.J4:F1}°";
            if (_dhTheta5 != null) _dhTheta5.Text = $"{20.1 + robotRenderer.J5:F1}°";
            if (_dhTheta6 != null) _dhTheta6.Text = $"{robotRenderer.J6:F1}°";
            if (_dhTheta7 != null) _dhTheta7.Text = $"{robotRenderer.J7:F1} mm";

            if (_opcUaManager != null)
            {
                _opcUaManager.ActualJ1 = robotRenderer.J1;
                _opcUaManager.ActualJ2 = robotRenderer.J2;
                _opcUaManager.ActualJ3 = robotRenderer.J3;
                _opcUaManager.ActualJ4 = robotRenderer.J4;
                _opcUaManager.ActualJ5 = robotRenderer.J5;
                _opcUaManager.ActualJ6 = robotRenderer.J6;
                _opcUaManager.ActualJ7 = robotRenderer.J7;
                _opcUaManager.ActualPinch = robotRenderer.JawGap;
                _opcUaManager.ActualX = x;
                _opcUaManager.ActualY = y;
                _opcUaManager.ActualZ = z;
            }

            if (_mcuManager != null)
            {
                _mcuManager.ActualJ1 = robotRenderer.J1;
                _mcuManager.ActualJ2 = robotRenderer.J2;
                _mcuManager.ActualJ3 = robotRenderer.J3;
                _mcuManager.ActualJ4 = robotRenderer.J4;
                _mcuManager.ActualJ5 = robotRenderer.J5;
                _mcuManager.ActualJ6 = robotRenderer.J6;
                _mcuManager.ActualJ7 = robotRenderer.J7;
                _mcuManager.ActualPinch = robotRenderer.JawGap;
            }
        }

        private void TryLoadModels()
        {
            try
            {
                var searchRoots = new List<string>();
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var currentDir = Directory.GetCurrentDirectory();
                searchRoots.Add(baseDir);
                if (!searchRoots.Contains(currentDir, StringComparer.OrdinalIgnoreCase))
                    searchRoots.Add(currentDir);

                foreach (var root in new[] { baseDir, currentDir })
                {
                    var dir = root;
                    for (int i = 0; i < 5; i++)
                    {
                        if (string.IsNullOrWhiteSpace(dir))
                            break;
                        searchRoots.Add(dir);
                        var parent = Directory.GetParent(dir);
                        if (parent == null) break;
                        dir = parent.FullName;
                    }
                }

                var candidates = new List<string>();
                foreach (var root in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var candidateDir = System.IO.Path.Combine(root, "Models");
                    if (Directory.Exists(candidateDir))
                        candidates.AddRange(Directory.GetFiles(candidateDir, "*.stl", SearchOption.TopDirectoryOnly));
                }

                // Ensure sorted so base_link is first, link_1, etc.
                var sortedFiles = candidates.OrderBy(f => f).ToList();
                if (robotRenderer != null)
                {
                    foreach (var file in sortedFiles)
                    {
                        robotRenderer.Models.Add(StlModel.Load(file));
                    }
                    robotRenderer.InvalidateVisual();
                }
            }
            catch
            {
                // ignore
            }
        }

        private int _tourStep = 0;
        private readonly (string Title, string Desc, string TargetName, Avalonia.Controls.PlacementMode Placement)[] _tourData = {
            ("1/6 - DİJİTAL İKİZ BÖLÜMÜ", "Bu bölüm robotun 3D simülasyonunu gösterir. Farenizle açıyı değiştirebilirsiniz.", "border3DEnv", Avalonia.Controls.PlacementMode.Bottom),
            ("2/6 - [MOD-AI] COMPUTER VISION", "Kamera ile el takibi modülü. Sağ elinizle konumu, sol elinizle Gripper'ı (çimdik yaparak) kontrol edebilirsiniz.", "pnlAI", Avalonia.Controls.PlacementMode.Right),
            ("3/6 - [DATA] TELEMETRİ MERKEZİ", "Gerçek zamanlı sensör verileri. Tork, Konum ve Hız değerlerini canlı izleyip CSV olarak bilgisayarınıza aktarabilirsiniz.", "pnlTelemetry", Avalonia.Controls.PlacementMode.Left),
            ("4/6 - [SYS] TEACH PENDANT", "Robotun hareketlerini anlık kaydedebilir (REC) ve daha sonra otomatik olarak yeniden oynatabilirsiniz (PLAY).", "pnlTeach", Avalonia.Controls.PlacementMode.Right),
            ("5/6 - [JOG] JOINT DIAGNOSTICS", "Robotun tüm eklemlerini manuel olarak bağımsız bir şekilde hareket ettirip test edebileceğiniz kaydırıcılar (slider).", "pnlJog", Avalonia.Controls.PlacementMode.Left),
            ("6/6 - [CMD] INVERSE KINEMATICS", "Uzaydaki hedef X, Y, Z koordinatını girin ve EXECUTE JOG butonuna basın, robot en uygun pozisyonu hesaplayarak oraya gidecektir.", "pnlIK", Avalonia.Controls.PlacementMode.Right)
        };

        private void SetupTour()
        {
            var btnTourHelp = this.FindControl<Button>("btnTourHelp");
            var tourPopup = this.FindControl<Avalonia.Controls.Primitives.Popup>("tourPopup");
            var btnTourClose = this.FindControl<Button>("btnTourClose");
            var btnTourNext = this.FindControl<Button>("btnTourNext");
            var btnTourPrev = this.FindControl<Button>("btnTourPrev");

            if (btnTourHelp != null && tourPopup != null)
            {
                btnTourHelp.Click += (s, e) => {
                    _tourStep = 0;
                    UpdateTourUI();
                    tourPopup.IsOpen = true;
                };

                btnTourClose!.Click += (s, e) => tourPopup.IsOpen = false;
                
                btnTourNext!.Click += async (s, e) => {
                    if (_tourStep < _tourData.Length - 1) {
                        _tourStep++;
                        tourPopup.IsOpen = false;
                        await System.Threading.Tasks.Task.Delay(50);
                        UpdateTourUI();
                        tourPopup.IsOpen = true;
                    } else {
                        tourPopup.IsOpen = false;
                    }
                };
                
                btnTourPrev!.Click += async (s, e) => {
                    if (_tourStep > 0) {
                        _tourStep--;
                        tourPopup.IsOpen = false;
                        await System.Threading.Tasks.Task.Delay(50);
                        UpdateTourUI();
                        tourPopup.IsOpen = true;
                    }
                };
            }
        }

        private void UpdateTourUI()
        {
            var tourTitle = this.FindControl<TextBlock>("tourTitle");
            var tourDesc = this.FindControl<TextBlock>("tourDesc");
            var btnTourNext = this.FindControl<Button>("btnTourNext");
            var tourPopup = this.FindControl<Avalonia.Controls.Primitives.Popup>("tourPopup");
            
            var stepData = _tourData[_tourStep];
            if (tourTitle != null) tourTitle.Text = stepData.Title;
            if (tourDesc != null) tourDesc.Text = stepData.Desc;
            
            if (btnTourNext != null) {
                btnTourNext.Content = (_tourStep == _tourData.Length - 1) ? "BİTİR" : "SONRAKİ >";
            }

            if (tourPopup != null) {
                var targetControl = this.FindControl<Avalonia.Controls.Control>(stepData.TargetName);
                if (targetControl != null) {
                    tourPopup.PlacementTarget = targetControl;
                    tourPopup.Placement = stepData.Placement;
                }
            }
        }

        private void SetupBackgroundColor()
        {
            var cbBgColor = this.FindControl<ComboBox>("cbBgColor");
            var border3DEnv = this.FindControl<Border>("border3DEnv");

            if (cbBgColor != null && border3DEnv != null)
            {
                cbBgColor.SelectionChanged += (s, e) => {
                    var idx = cbBgColor.SelectedIndex;
                    // Siyah, Koyu Gri, Gece Mavisi, Endüstriyel Yeşil
                    string colorHex = idx switch {
                        1 => "#222222",
                        2 => "#0a192f",
                        3 => "#1b3320",
                        _ => "#050505"
                    };
                    border3DEnv.Background = Avalonia.Media.SolidColorBrush.Parse(colorHex);
                };
            }
        }

        private void SetupScripting()
        {
            var btnOpenScripting = this.FindControl<Button>("btnOpenScripting");
            var pnlScripting = this.FindControl<Border>("pnlScripting");
            var btnCloseScripting = this.FindControl<Button>("btnCloseScripting");
            var btnRunScript = this.FindControl<Button>("btnRunScript");
            var pnlScriptingHeader = this.FindControl<Grid>("pnlScriptingHeader");

            if (pnlScripting != null && pnlScriptingHeader != null) {
                MakeDraggable(pnlScripting, pnlScriptingHeader);
            }

            if (btnOpenScripting != null && pnlScripting != null)
            {
                btnOpenScripting.Click += (s, e) => {
                    pnlScripting.IsVisible = true;
                    LogToScripting("Scripting Modülü Açıldı.");
                };
                
                if (btnCloseScripting != null) {
                    btnCloseScripting.Click += (s, e) => pnlScripting.IsVisible = false;
                }

                if (btnRunScript != null) {
                    btnRunScript.Click += async (s, e) => {
                        var txtScript = this.FindControl<TextBox>("txtScript");
                        if (txtScript != null && !string.IsNullOrWhiteSpace(txtScript.Text)) {
                            btnRunScript.IsEnabled = false;
                            LogToScripting("--- YENİ SENARYO BAŞLATILDI ---");
                            await ExecuteScriptAsync(txtScript.Text);
                            LogToScripting("--- SENARYO TAMAMLANDI ---");
                            btnRunScript.IsEnabled = true;
                        }
                    };
                }
            }
        }

        private void LogToScripting(string msg)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                var lstLogs = this.FindControl<ListBox>("lstLogs");
                if (lstLogs != null) {
                    var items = lstLogs.Items.Cast<string>().ToList();
                    items.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
                    if (items.Count > 100) items.RemoveAt(0);
                    lstLogs.ItemsSource = items;
                    lstLogs.ScrollIntoView(items.Last());
                }
            });
        }

        private async System.Threading.Tasks.Task ExecuteScriptAsync(string scriptText)
        {
            var lines = scriptText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var j1 = this.FindControl<Slider>("j1_s");
            var j2 = this.FindControl<Slider>("j2_s");
            var j3 = this.FindControl<Slider>("j3_s");
            var j4 = this.FindControl<Slider>("j4_s");
            var j5 = this.FindControl<Slider>("j5_s");
            var j6 = this.FindControl<Slider>("j6_s");
            
            var trgX = this.FindControl<NumericUpDown>("trgX");
            var trgY = this.FindControl<NumericUpDown>("trgY");
            var trgZ = this.FindControl<NumericUpDown>("trgZ");

            foreach (var rawLine in lines) {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

                LogToScripting($"> {line}");
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToUpper();

                try {
                    if (cmd == "GOTO" && parts.Length >= 4) {
                        double x = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        double y = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                        double z = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);

                        if (trgX != null) trgX.Value = (decimal)x;
                        if (trgY != null) trgY.Value = (decimal)y;
                        if (trgZ != null) trgZ.Value = (decimal)z;

                        ExecuteJogIK(j1, j2, j3, j4, j5, j6);
                    }
                    else if (cmd == "WAIT" && parts.Length >= 2) {
                        int ms = int.Parse(parts[1]);
                        await System.Threading.Tasks.Task.Delay(ms);
                    }
                    else if (cmd == "GRIP" && parts.Length >= 2) {
                        double val = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        if (robotRenderer != null) robotRenderer.JawGap = val * 80.0;
                    }
                    else if (cmd == "HOME") {
                        if (trgX != null) trgX.Value = (decimal)0.4;
                        if (trgY != null) trgY.Value = (decimal)0.0;
                        if (trgZ != null) trgZ.Value = (decimal)0.4;
                        ExecuteJogIK(j1, j2, j3, j4, j5, j6);
                    }
                    else {
                        LogToScripting($"Hata: Bilinmeyen veya eksik komut '{line}'");
                    }
                } catch (Exception ex) {
                    LogToScripting($"Hata: {ex.Message}");
                }
            }
        }

        private System.Threading.CancellationTokenSource? _gamepadCts;
        private Avalonia.Threading.DispatcherTimer? _gpTimer;
        
        // Smoothing variables
        private double _gpSmoothX = 0.4;
        private double _gpSmoothY = 0.0;
        private double _gpSmoothZ = 0.4;
        private double _gpSmoothJ1 = 0.0;
        private double _gpSmoothJ2 = 0.0;
        private double _gpSmoothJ3 = 0.0;
        private double _gpSmoothJ4 = 0.0;
        private double _gpSmoothJ5 = 0.0;
        private double _gpSmoothJ6 = 0.0;
        private double _gpSmoothJ7 = 0.0;
        private double _gpSmoothPinch = 80.0;

        // Target variables updated by Joystick thread
        private double _gpTargetX = 0.4;
        private double _gpTargetY = 0.0;
        private double _gpTargetZ = 0.4;
        private double _gpTargetJ1 = 0.0;
        private double _gpTargetJ2 = 0.0;
        private double _gpTargetJ3 = 0.0;
        private double _gpTargetJ4 = 0.0;
        private double _gpTargetJ5 = 0.0;
        private double _gpTargetJ6 = 0.0;
        private double _gpTargetJ7 = 0.0;
        private double _gpTargetPinch = 80.0;
        private bool _gpDirectJointsActive = false;
        private void SetupGamepad()
        {
            var toggleGamepad = this.FindControl<ToggleSwitch>("toggleGamepad");
            if (toggleGamepad != null)
            {
                toggleGamepad.IsCheckedChanged += (s, e) =>
                {
                    if (toggleGamepad.IsChecked == true)
                        StartGamepad();
                    else
                        StopGamepad();
                };
            }
        }

        private void StartGamepad()
        {
            var txtGamepadStatus = this.FindControl<TextBlock>("txtGamepadStatus");
            if (txtGamepadStatus != null)
            {
                txtGamepadStatus.Text = "SYS: CONNECTING...";
                txtGamepadStatus.Foreground = Avalonia.Media.Brushes.Yellow;
            }

            _gamepadCts = new System.Threading.CancellationTokenSource();
            var ct = _gamepadCts.Token;

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    using var fs = new System.IO.FileStream("/dev/input/js0", System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite, 4096, System.IO.FileOptions.Asynchronous);
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        if (txtGamepadStatus != null) {
                            txtGamepadStatus.Text = "SYS: JOYSTICK ACTIVE";
                            txtGamepadStatus.Foreground = Avalonia.Media.Brushes.LimeGreen;
                        }
                    });

                    // Prepare target variables
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        var trgX_c = _trgX;
                        if (trgX_c != null) _gpSmoothX = (double)trgX_c.Value;
                        var trgY_c = _trgY;
                        if (trgY_c != null) _gpSmoothY = (double)trgY_c.Value;
                        var trgZ_c = _trgZ;
                        if (trgZ_c != null) _gpSmoothZ = (double)trgZ_c.Value;

                        var j1_s_c = _j1_s;
                        if (j1_s_c != null) _gpSmoothJ1 = j1_s_c.Value;
                        var j2_s_c = _j2_s;
                        if (j2_s_c != null) _gpSmoothJ2 = j2_s_c.Value;
                        var j3_s_c = _j3_s;
                        if (j3_s_c != null) _gpSmoothJ3 = j3_s_c.Value;
                        var j4_s_c = _j4_s;
                        if (j4_s_c != null) _gpSmoothJ4 = j4_s_c.Value;
                        var j5_s_c = _j5_s;
                        if (j5_s_c != null) _gpSmoothJ5 = j5_s_c.Value;
                        var j6_s_c = _j6_s;
                        if (j6_s_c != null) _gpSmoothJ6 = j6_s_c.Value;
                        var j7_s_c = _j7_s;
                        if (j7_s_c != null) _gpSmoothJ7 = j7_s_c.Value;
                        
                        _gpTargetX = _gpSmoothX;
                        _gpTargetY = _gpSmoothY;
                        _gpTargetZ = _gpSmoothZ;
                        _gpTargetJ1 = _gpSmoothJ1;
                        _gpTargetJ2 = _gpSmoothJ2;
                        _gpTargetJ3 = _gpSmoothJ3;
                        _gpTargetJ4 = _gpSmoothJ4;
                        _gpTargetJ5 = _gpSmoothJ5;
                        _gpTargetJ6 = _gpSmoothJ6;
                        _gpTargetJ7 = _gpSmoothJ7;

                        // Start Timer for independent smoothing (60 FPS)
                        _gpTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                        _gpTimer.Tick += (s, e) => {
                            _gpSmoothX += 0.02 * (_gpTargetX - _gpSmoothX);
                            _gpSmoothY += 0.02 * (_gpTargetY - _gpSmoothY);
                            _gpSmoothZ += 0.02 * (_gpTargetZ - _gpSmoothZ);
                            
                            _gpSmoothJ1 += 0.05 * (_gpTargetJ1 - _gpSmoothJ1);
                            _gpSmoothJ2 += 0.05 * (_gpTargetJ2 - _gpSmoothJ2);
                            _gpSmoothJ3 += 0.05 * (_gpTargetJ3 - _gpSmoothJ3);
                            _gpSmoothJ4 += 0.05 * (_gpTargetJ4 - _gpSmoothJ4);
                            _gpSmoothJ5 += 0.05 * (_gpTargetJ5 - _gpSmoothJ5);
                            _gpSmoothJ6 += 0.05 * (_gpTargetJ6 - _gpSmoothJ6);
                            _gpSmoothJ7 += 0.02 * (_gpTargetJ7 - _gpSmoothJ7);
                            _gpSmoothPinch += 0.05 * (_gpTargetPinch - _gpSmoothPinch);

                            var j1 = _j1_s;
                            var j2 = _j2_s;
                            var j3 = _j3_s;
                            var j4 = _j4_s;
                            var j5 = _j5_s;
                            var j6 = _j6_s;
                            var j7 = _j7_s;
                            
                            if (j7 != null) j7.Value = Math.Clamp(_gpSmoothJ7, -1000, 1000);

                            var trgX = _trgX;
                            var trgY = _trgY;
                            var trgZ = _trgZ;
                            
                            if (!_gpDirectJointsActive)
                            {
                                if (trgX != null) trgX.Value = (decimal)Math.Clamp(_gpSmoothX, -0.6, 0.6);
                                if (trgY != null) trgY.Value = (decimal)Math.Clamp(_gpSmoothY, -0.6, 0.6);
                                if (trgZ != null) trgZ.Value = (decimal)Math.Clamp(_gpSmoothZ, 0.0, 0.8);
                                ExecuteJogIK(j1, j2, j3, j4, j5, j6);
                            }
                            else
                            {
                                if (j1 != null) j1.Value = Math.Clamp(_gpSmoothJ1, -180, 180);
                                if (j2 != null) j2.Value = Math.Clamp(_gpSmoothJ2, -180, 180);
                                if (j3 != null) j3.Value = Math.Clamp(_gpSmoothJ3, -180, 180);
                                if (j4 != null) j4.Value = Math.Clamp(_gpSmoothJ4, -180, 180);
                                if (j5 != null) j5.Value = Math.Clamp(_gpSmoothJ5, -180, 180);
                                if (j6 != null) j6.Value = Math.Clamp(_gpSmoothJ6, -180, 180);
                                
                                // Sync targets so switching back doesn't snap
                                if (trgX != null) _gpTargetX = _gpSmoothX = (double)trgX.Value;
                                if (trgY != null) _gpTargetY = _gpSmoothY = (double)trgY.Value;
                                if (trgZ != null) _gpTargetZ = _gpSmoothZ = (double)trgZ.Value;
                            }

                            var robotRenderer = this.FindControl<RobotRenderer>("robotRenderer");
                            if (robotRenderer != null) robotRenderer.JawGap = _gpSmoothPinch;
                        };
                        _gpTimer.Start();
                    });

                    byte[] buffer = new byte[8];
                    while (!ct.IsCancellationRequested)
                    {
                        int read = await fs.ReadAsync(buffer, 0, 8, ct);
                        if (read != 8) continue;

                        short value = BitConverter.ToInt16(buffer, 4);
                        byte type = buffer[6];
                        byte number = buffer[7];
                        type &= 0x7F;

                        if (type == 0x01 && value == 1) // Button Pressed
                        {
                            var action = GamepadSettings.Instance.GetActionForButton(number);
                            switch (action)
                            {
                                case GamepadAction.HomePosition:
                                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                        var trgX = _trgX;
                                        var trgY = _trgY;
                                        var trgZ = _trgZ;
                                        if (trgX != null) trgX.Value = (decimal)0.4;
                                        if (trgY != null) trgY.Value = (decimal)0.0;
                                        if (trgZ != null) trgZ.Value = (decimal)0.4;
                                        _gpSmoothY = 0; _gpSmoothZ = 0.4;
                                        _gpTargetY = 0; _gpTargetZ = 0.4;
                                        
                                        var j1 = _j1_s;
                                        var j2 = _j2_s;
                                        var j3 = _j3_s;
                                        var j4 = _j4_s;
                                        var j5 = _j5_s;
                                        var j6 = _j6_s;
                                        ExecuteJogIK(j1, j2, j3, j4, j5, j6);
                                    });
                                    break;
                                case GamepadAction.GripperClose:
                                    _gpTargetPinch = 0.0; // close
                                    break;
                                case GamepadAction.GripperOpen:
                                    _gpTargetPinch = 80.0; // open
                                    break;
                            }
                        }
                        else if (type == 0x02) // Axis
                        {
                            double norm = value / 32767.0;
                            if (Math.Abs(norm) < 0.1) norm = 0; // deadzone

                            var action = GamepadSettings.Instance.GetActionForAxis(number);
                            
                            bool isIkAction = action == GamepadAction.MoveX || action == GamepadAction.MoveY || action == GamepadAction.MoveZ;
                            bool isJointAction = action == GamepadAction.MoveJ1 || action == GamepadAction.MoveJ2 || action == GamepadAction.MoveJ3 || 
                                                 action == GamepadAction.MoveJ4 || action == GamepadAction.MoveJ5 || action == GamepadAction.MoveJ6;

                            if (isIkAction && Math.Abs(norm) > 0.05) _gpDirectJointsActive = false;
                            if (isJointAction && Math.Abs(norm) > 0.05) _gpDirectJointsActive = true;

                            switch (action)
                            {
                                case GamepadAction.MoveJ7:
                                    _gpTargetJ7 = norm * 1000.0;
                                    break;
                                case GamepadAction.MoveX:
                                    _gpTargetX = 0.4 + (norm * 0.4);
                                    break;
                                case GamepadAction.MoveY:
                                    _gpTargetY = norm * 0.6;
                                    break;
                                case GamepadAction.MoveZ:
                                    _gpTargetZ = 0.4 - (norm * 0.4);
                                    break;
                                case GamepadAction.MoveJ1:
                                    _gpTargetJ1 = norm * 180.0;
                                    break;
                                case GamepadAction.MoveJ2:
                                    _gpTargetJ2 = norm * 180.0;
                                    break;
                                case GamepadAction.MoveJ3:
                                    _gpTargetJ3 = norm * 180.0;
                                    break;
                                case GamepadAction.MoveJ4:
                                    _gpTargetJ4 = norm * 180.0;
                                    break;
                                case GamepadAction.MoveJ5:
                                    _gpTargetJ5 = norm * 180.0;
                                    break;
                                case GamepadAction.MoveJ6:
                                    _gpTargetJ6 = norm * 180.0;
                                    break;
                            }
                        }
                    }
                }
                catch
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        if (txtGamepadStatus != null) {
                            txtGamepadStatus.Text = "SYS: OFFLINE / NO USB";
                            txtGamepadStatus.Foreground = Avalonia.Media.Brushes.Red;
                        }
                        var toggleGamepad = this.FindControl<ToggleSwitch>("toggleGamepad");
                        if (toggleGamepad != null) toggleGamepad.IsChecked = false;
                    });
                }
            }, ct);
        }

        private void StopGamepad()
        {
            _gpTimer?.Stop();
            _gamepadCts?.Cancel();
            var txtGamepadStatus = this.FindControl<TextBlock>("txtGamepadStatus");
            if (txtGamepadStatus != null)
            {
                txtGamepadStatus.Text = "SYS: JOYSTICK OFFLINE";
                txtGamepadStatus.Foreground = Avalonia.Media.Brushes.Gray;
            }
        }

        private System.Diagnostics.Process? _cvProcess;

        // --- COMPUTER VISION LOGIC ---
        private System.Net.Sockets.TcpClient? _cvClient;
        private System.Threading.CancellationTokenSource? _cvCts;

        // Smoothing variables for CV tracking
        private double _cvSmoothX = 0.4;
        private double _cvSmoothY = 0.0;
        private double _cvSmoothZ = 0.4;
        private double _cvSmoothJ5 = 0.0;
        private double _cvSmoothJ6 = 0.0;
        private double _cvSmoothJ7 = 0.0;
        private double _cvSmoothPinch = 80.0;

        private void StartCV(Slider? j1, Slider? j2, Slider? j3, Slider? j4, Slider? j5, Slider? j6)
        {
            var txtWebcamOffline = this.FindControl<TextBlock>("txtWebcamOffline");
            var imgWebcam = this.FindControl<Avalonia.Controls.Image>("imgWebcam");
            var txtAiStatus = this.FindControl<TextBlock>("txtAiStatus");

            if (txtWebcamOffline != null) txtWebcamOffline.IsVisible = false;
            if (imgWebcam != null) imgWebcam.IsVisible = true;
            if (txtAiStatus != null) {
                txtAiStatus.Text = "SYS: STARTING CV SERVER...";
                txtAiStatus.Foreground = Avalonia.Media.Brushes.Yellow;
            }

            _cvProcess = new System.Diagnostics.Process {
                StartInfo = new System.Diagnostics.ProcessStartInfo {
                    FileName = "python3",
                    Arguments = "cv_server.py",
                    WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            try { _cvProcess.Start(); } catch {}

            _cvCts = new System.Threading.CancellationTokenSource();
            System.Threading.Tasks.Task.Run(() => ConnectAndReadCV(_cvCts.Token, j1, j2, j3, j4, j5, j6));
        }

        private void StopCV()
        {
            _cvCts?.Cancel();
            try { _cvClient?.Close(); } catch {}
            try { _cvProcess?.Kill(); } catch {}
            _cvProcess = null;
            _cvClient = null;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                var txtWebcamOffline = this.FindControl<TextBlock>("txtWebcamOffline");
                var imgWebcam = this.FindControl<Avalonia.Controls.Image>("imgWebcam");
                var txtAiStatus = this.FindControl<TextBlock>("txtAiStatus");
                
                if (txtWebcamOffline != null) txtWebcamOffline.IsVisible = true;
                if (imgWebcam != null) imgWebcam.IsVisible = false;
                if (txtAiStatus != null) {
                    txtAiStatus.Text = "SYS: AI INACTIVE";
                    txtAiStatus.Foreground = Avalonia.Media.Brushes.Gray;
                }
            });
        }

        private async System.Threading.Tasks.Task ConnectAndReadCV(System.Threading.CancellationToken ct, Slider? j1, Slider? j2, Slider? j3, Slider? j4, Slider? j5, Slider? j6)
        {
            int retries = 0;
            while (!ct.IsCancellationRequested && retries < 10)
            {
                try {
                    _cvClient = new System.Net.Sockets.TcpClient();
                    await _cvClient.ConnectAsync("127.0.0.1", 5050, ct);
                    break;
                } catch {
                    retries++;
                    await System.Threading.Tasks.Task.Delay(500, ct);
                }
            }

            if (_cvClient == null || !_cvClient.Connected) {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    var txtAiStatus = this.FindControl<TextBlock>("txtAiStatus");
                    if (txtAiStatus != null) {
                        txtAiStatus.Text = "SYS: CV SERVER FAILED";
                        txtAiStatus.Foreground = Avalonia.Media.Brushes.Red;
                    }
                });
                return;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                var txtAiStatus = this.FindControl<TextBlock>("txtAiStatus");
                if (txtAiStatus != null) {
                    txtAiStatus.Text = "SYS: TRACKING ACTIVE";
                    txtAiStatus.Foreground = Avalonia.Media.Brushes.Lime;
                }
            });

            var stream = _cvClient.GetStream();
            byte[] lenBuf = new byte[4];
            
            try {
                while (!ct.IsCancellationRequested)
                {
                    int totalRead = 0;
                    while (totalRead < 4) {
                        int r = await stream.ReadAsync(lenBuf.AsMemory(totalRead, 4 - totalRead), ct);
                        if (r == 0) throw new Exception("Stream closed");
                        totalRead += r;
                    }
                    int jsonLen = BitConverter.ToInt32(lenBuf, 0);
                    
                    byte[] jsonBytes = new byte[jsonLen];
                    totalRead = 0;
                    while (totalRead < jsonLen) {
                        int r = await stream.ReadAsync(jsonBytes.AsMemory(totalRead, jsonLen - totalRead), ct);
                        if (r == 0) throw new Exception("Stream closed");
                        totalRead += r;
                    }
                    string jsonStr = System.Text.Encoding.UTF8.GetString(jsonBytes);
                    
                    totalRead = 0;
                    while (totalRead < 4) {
                        int r = await stream.ReadAsync(lenBuf.AsMemory(totalRead, 4 - totalRead), ct);
                        if (r == 0) throw new Exception("Stream closed");
                        totalRead += r;
                    }
                    int jpegLen = BitConverter.ToInt32(lenBuf, 0);
                    
                    byte[] jpegBytes = new byte[jpegLen];
                    totalRead = 0;
                    while (totalRead < jpegLen) {
                        int r = await stream.ReadAsync(jpegBytes.AsMemory(totalRead, jpegLen - totalRead), ct);
                        if (r == 0) throw new Exception("Stream closed");
                        totalRead += r;
                    }
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        try {
                            using var ms = new System.IO.MemoryStream(jpegBytes);
                            var bitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                            var imgWebcam = this.FindControl<Avalonia.Controls.Image>("imgWebcam");
                            if (imgWebcam != null) imgWebcam.Source = bitmap;

                            using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                            var root = doc.RootElement;
                            double rx = root.GetProperty("rx").GetDouble();
                            double ry = root.GetProperty("ry").GetDouble();
                            double r_size = root.GetProperty("r_size").GetDouble();
                            double r_angle = root.GetProperty("r_angle").GetDouble();
                            double lx = root.GetProperty("lx").GetDouble();
                            double ly = root.GetProperty("ly").GetDouble();
                            double pinch = root.GetProperty("pinch").GetDouble();

                            if (rx >= 0) {
                                // Right Hand: X, Y, Z (Position IK) + J6 (Roll)
                                double rawY = (rx - 0.5) * -1.0; 
                                double rawZ = (1.0 - ry) * 0.8;
                                // r_size (depth): 0.05 is far, 0.30 is close
                                double rawX = 0.2 + (r_size * 2.0);
                                
                                // r_angle (roll): usually around -PI/2 when hand is upright
                                double rawJ6 = (r_angle + Math.PI/2) * (180.0 / Math.PI) * -1.0;
                                
                                _cvSmoothX += 0.20 * (rawX - _cvSmoothX);
                                _cvSmoothY += 0.20 * (rawY - _cvSmoothY);
                                _cvSmoothZ += 0.20 * (rawZ - _cvSmoothZ);
                                _cvSmoothJ6 += 0.20 * (rawJ6 - _cvSmoothJ6);

                                var trgX = _trgX;
                                var trgY = _trgY;
                                var trgZ = _trgZ;
                                
                                if (trgX != null) trgX.Value = (decimal)Math.Clamp(_cvSmoothX, 0.2, 0.6);
                                if (trgY != null) trgY.Value = (decimal)Math.Clamp(_cvSmoothY, -0.6, 0.6);
                                if (trgZ != null) trgZ.Value = (decimal)Math.Clamp(_cvSmoothZ, 0.0, 0.8);
                                
                                if (_j6_s != null) _j6_s.Value = Math.Clamp(_cvSmoothJ6, -180, 180);

                                ExecuteJogIK(_j1_s, _j2_s, _j3_s, _j4_s, _j5_s, _j6_s);
                            }

                            if (lx >= 0) {
                                // Left Hand: Y controls Pitch (J5). Rail (J7) is disabled as requested.
                                double rawJ5 = (ly - 0.5) * -180.0;
                                _cvSmoothJ5 += 0.20 * (rawJ5 - _cvSmoothJ5);

                                if (_j5_s != null) _j5_s.Value = Math.Clamp(_cvSmoothJ5, -120, 120);
                            }

                            if (pinch >= 0) {
                                double rawGap = Math.Clamp((pinch - 0.05) / 0.15 * 80.0, 0.0, 80.0);
                                _cvSmoothPinch += 0.20 * (rawGap - _cvSmoothPinch);
                                if (robotRenderer != null) robotRenderer.JawGap = _cvSmoothPinch;
                            }
                        } catch {}
                    });
                }
            } catch {}
        }

        // --- AI ENGLISH PARTNER LOGIC ---
        private System.Diagnostics.Process? _aiProcess;
        private System.Diagnostics.Process? _micProcess;
        private System.Net.Sockets.TcpClient? _aiClient;
        private System.Threading.CancellationTokenSource? _aiCts;

        private void StartRecordingMic()
        {
            try {
                if (_micProcess != null) {
                    try { _micProcess.Kill(); } catch {}
                }
                if (System.IO.File.Exists("/tmp/roki_mic.wav")) System.IO.File.Delete("/tmp/roki_mic.wav");
                _micProcess = new System.Diagnostics.Process {
                    StartInfo = new System.Diagnostics.ProcessStartInfo {
                        FileName = "arecord",
                        Arguments = "-f S16_LE -c 1 -r 16000 /tmp/roki_mic.wav",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                _micProcess.Start();
            } catch (Exception ex) {
                AddSystemMessage($"[MIC ERROR] {ex.Message}");
            }
        }

        private void StopRecordingMicAndSend()
        {
            try {
                if (_micProcess != null) {
                    try { _micProcess.Kill(); } catch {}
                    _micProcess = null;
                }
                if (System.IO.File.Exists("/tmp/roki_mic.wav")) {
                    AddSystemMessage("[SYSTEM] Transcribing and sending audio...");
                    SendAIAudio("/tmp/roki_mic.wav");
                }
            } catch (Exception ex) {
                AddSystemMessage($"[MIC ERROR] {ex.Message}");
            }
        }

        private void SendAIAudio(string path)
        {
            if (_aiClient == null || !_aiClient.Connected) {
                AddSystemMessage("[SYSTEM ERROR] AI Partner not active.");
                return;
            }
            try {
                var stream = _aiClient.GetStream();
                string level = "Beginner";
                var cmb = this.FindControl<ComboBox>("cmbEnglishLevel");
                if (cmb != null && cmb.SelectedItem is ComboBoxItem cbi) {
                    level = cbi.Content?.ToString() ?? "Beginner";
                }
                
                string payload = System.Text.Json.JsonSerializer.Serialize(new { msg = "", audio = path, level = level });
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload + "\n");
                stream.Write(bytes, 0, bytes.Length);
            } catch (Exception e) {
                AddSystemMessage($"[ERROR] SendAIAudio: {e.Message}");
            }
        }

        private async System.Threading.Tasks.Task<string> TranslateTextAsync(string text)
        {
            try {
                using var client = new System.Net.Http.HttpClient();
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=tr&dt=t&q={System.Uri.EscapeDataString(text)}";
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                string result = await client.GetStringAsync(url);
                using var doc = System.Text.Json.JsonDocument.Parse(result);
                var translated = doc.RootElement[0][0][0].GetString();
                return translated ?? text;
            } catch {
                return "Translation failed.";
            }
        }

        private void BtnOpenGazebo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var gazeboWindow = new GazeboWindow();
            gazeboWindow.Show(this);
        }


        private void AddChatMessage(string sender, string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                var pnl = this.FindControl<StackPanel>("pnlPartnerChatContainer");
                if (pnl == null) return;

                var txtStatus = this.FindControl<TextBlock>("txtPartnerChatStatus");
                if (txtStatus != null) txtStatus.IsVisible = false;

                var sp = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Avalonia.Thickness(0, 0, 0, 4) };
                
                var cb = new CheckBox { 
                    Margin = new Avalonia.Thickness(0, 0, 5, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                };

                var tb = new TextBox { 
                    Text = $"{sender}: {message}", 
                    Foreground = sender == "YOU" ? Avalonia.Media.Brushes.LightBlue : Avalonia.Media.Brushes.LightGray,
                    Background = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = new Avalonia.Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Width = 220
                };

                cb.IsCheckedChanged += async (s, e) => {
                    if (cb.IsChecked == true) {
                        string trText = await TranslateTextAsync(message);
                        tb.Text = $"{sender}: {trText} [TR]";
                    } else {
                        tb.Text = $"{sender}: {message}";
                    }
                };

                sp.Children.Add(cb);
                sp.Children.Add(tb);
                pnl.Children.Add(sp);

                var scroll = this.FindControl<ScrollViewer>("scrollPartnerChat");
                scroll?.ScrollToEnd();
            });
        }

        private void AddSystemMessage(string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                var pnl = this.FindControl<StackPanel>("pnlPartnerChatContainer");
                if (pnl == null) return;
                var txtStatus = this.FindControl<TextBlock>("txtPartnerChatStatus");
                if (txtStatus != null) txtStatus.IsVisible = false;

                var tb = new TextBlock { 
                    Text = message, 
                    Foreground = Avalonia.Media.Brushes.Yellow,
                    FontFamily = Avalonia.Media.FontFamily.Parse("Consolas"),
                    Margin = new Avalonia.Thickness(0, 2)
                };
                pnl.Children.Add(tb);
                var scroll = this.FindControl<ScrollViewer>("scrollPartnerChat");
                scroll?.ScrollToEnd();
            });
        }

        private void StartAIPartner()
        {
            var pnl = this.FindControl<StackPanel>("pnlPartnerChatContainer");
            if (pnl != null) {
                pnl.Children.Clear();
            }
            AddSystemMessage("[SYSTEM] Starting AI Partner...");
            var imgBg = this.FindControl<Avalonia.Controls.Image>("imgJarvisBg");
            if (imgBg != null) {
                imgBg.IsVisible = true;
                imgBg.Classes.Clear();
                imgBg.Classes.Add("hudIdle");
            }

            _aiProcess = new System.Diagnostics.Process {
                StartInfo = new System.Diagnostics.ProcessStartInfo {
                    FileName = "python3",
                    Arguments = "ai_voice_server.py",
                    WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            try { _aiProcess.Start(); } catch {}

            _aiCts = new System.Threading.CancellationTokenSource();
            System.Threading.Tasks.Task.Run(() => ConnectAndReadAI(_aiCts.Token));
        }

        private void StopAIPartner()
        {
            _aiCts?.Cancel();
            try { _aiClient?.Close(); } catch {}
            try { _aiProcess?.Kill(); } catch {}
            _aiProcess = null;
            _aiClient = null;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                var imgBg = this.FindControl<Image>("imgJarvisBg");
                if (imgBg != null) imgBg.IsVisible = false;
                var pnl = this.FindControl<StackPanel>("pnlPartnerChatContainer");
                if (pnl != null) pnl.Children.Clear();

                var txtStatus = this.FindControl<TextBlock>("txtPartnerChatStatus");
                if (txtStatus == null) {
                    txtStatus = new TextBlock { 
                        Name = "txtPartnerChatStatus", 
                        Text = "[AI PARTNER OFFLINE]", 
                        Foreground = Avalonia.Media.Brushes.DarkGray, 
                        FontFamily = Avalonia.Media.FontFamily.Parse("Consolas") 
                    };
                    pnl?.Children.Add(txtStatus);
                } else {
                    txtStatus.Text = "[AI PARTNER OFFLINE]";
                    txtStatus.IsVisible = true;
                    pnl?.Children.Add(txtStatus);
                }
            });
        }

        private async System.Threading.Tasks.Task ConnectAndReadAI(System.Threading.CancellationToken ct)
        {
            int retries = 0;
            while (!ct.IsCancellationRequested && retries < 10)
            {
                try {
                    _aiClient = new System.Net.Sockets.TcpClient();
                    await _aiClient.ConnectAsync("127.0.0.1", 8002, ct);
                    break;
                } catch {
                    retries++;
                    await System.Threading.Tasks.Task.Delay(500, ct);
                }
            }

            if (_aiClient == null || !_aiClient.Connected) {
                AddSystemMessage("[SYSTEM] Failed to connect to AI Server.");
                return;
            }

            AddSystemMessage("[SYSTEM] AI Partner connected and listening.");

            try {
                using var stream = _aiClient.GetStream();
                using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);

                while (!ct.IsCancellationRequested) {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    try {
                        using var doc = System.Text.Json.JsonDocument.Parse(line);
                        
                        if (doc.RootElement.TryGetProperty("status", out var statusProp)) {
                            string statusStr = statusProp.GetString() ?? "";
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                var imgBg = this.FindControl<Avalonia.Controls.Image>("imgJarvisBg");
                                if (imgBg != null) {
                                    imgBg.Classes.Clear();
                                    if (statusStr == "speaking") {
                                        imgBg.Classes.Add("hudSpeaking");
                                    } else {
                                        imgBg.Classes.Add("hudIdle");
                                    }
                                }
                            });
                        }
                        
                        if (doc.RootElement.TryGetProperty("response", out var resp)) {
                            AddChatMessage("AI", resp.GetString() ?? "");
                        }
                        if (doc.RootElement.TryGetProperty("command", out var cmd)) {
                            string commandStr = cmd.GetString() ?? "";
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                ExecuteAICommand(commandStr);
                            });
                        }
                    } catch {}
                }
            } catch {}
        }

        private void ExecuteAICommand(string cmdStr)
        {
            try {
                using var doc = System.Text.Json.JsonDocument.Parse(cmdStr);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp)) return;
                string cmdType = typeProp.GetString() ?? "";

                if (cmdType == "GRIPPER_OPEN") {
                    if (robotRenderer != null) robotRenderer.JawGap = 80;
                    var grip_s = this.FindControl<Slider>("grip_s");
                    if (grip_s != null) grip_s.Value = 80;
                }
                else if (cmdType == "GRIPPER_CLOSE") {
                    if (robotRenderer != null) robotRenderer.JawGap = 0;
                    var grip_s = this.FindControl<Slider>("grip_s");
                    if (grip_s != null) grip_s.Value = 0;
                }
                else if (cmdType == "MOVE_HOME") {
                    var j7 = this.FindControl<Slider>("j7_s");
                    AnimateToHome(j7, _j1_s, _j2_s, _j3_s, _j4_s, _j5_s, _j6_s);
                }
                else if (cmdType == "JOG_JOINT") {
                    if (root.TryGetProperty("joint", out var jProp) && root.TryGetProperty("value", out var vProp)) {
                        int joint = jProp.GetInt32();
                        double val = vProp.GetDouble();
                        Slider? targetSlider = joint switch {
                            1 => _j1_s, 2 => _j2_s, 3 => _j3_s, 4 => _j4_s, 5 => _j5_s, 6 => _j6_s,
                            7 => this.FindControl<Slider>("j7_s"),
                            _ => null
                        };
                        if (targetSlider != null) targetSlider.Value = val;
                    }
                }
                else if (cmdType == "JOG_CART") {
                    if (root.TryGetProperty("axis", out var aProp) && root.TryGetProperty("value", out var vProp)) {
                        string axis = aProp.GetString() ?? "";
                        double val = vProp.GetDouble();
                        double dx=0, dy=0, dz=0;
                        if (axis == "X") dx = val;
                        else if (axis == "Y") dy = val;
                        else if (axis == "Z") dz = val;
                        JogCartesian(dx, dy, dz);
                    }
                }
                else if (cmdType == "START_RECORDING" || cmdType == "STOP_RECORDING") {
                    var btnRec = this.FindControl<Button>("btnRec");
                    if (btnRec != null) ToggleRecording(btnRec, _j1_s, _j2_s, _j3_s, _j4_s, _j5_s, _j6_s);
                }
                else if (cmdType == "PLAY_RECORDING") {
                    PlayRecordedWaypoints(_j1_s, _j2_s, _j3_s, _j4_s, _j5_s, _j6_s);
                }
                else if (cmdType == "CONNECT_MCU" || cmdType == "DISCONNECT_MCU") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnConnectMCU");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "CONNECT_OPC" || cmdType == "DISCONNECT_OPC") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnConnectOPC");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "TOGGLE_GAMEPAD") {
                    var tgl = this.FindControl<ToggleSwitch>("toggleGamepad");
                    if (tgl != null) tgl.IsChecked = !(tgl.IsChecked ?? false);
                }
                else if (cmdType == "TOGGLE_CV") {
                    var tgl = this.FindControl<ToggleSwitch>("toggleAiOverride");
                    if (tgl != null) tgl.IsChecked = !(tgl.IsChecked ?? false);
                }
                else if (cmdType == "TOGGLE_WORKSPACE") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnShowWorkspace");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "SHOW_AXES") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnShowAxes");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "OPEN_SCRIPTING") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnOpenScripting");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "HELP_TOUR") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnTourHelp");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "VISION_PICK") {
                    string objName = "hammer";
                    if (root.TryGetProperty("object", out var prop)) objName = prop.GetString() ?? "hammer";
                    try {
                        using var client = new System.Net.Sockets.TcpClient();
                        client.Connect("127.0.0.1", 8005);
                        byte[] data = System.Text.Encoding.UTF8.GetBytes("jarvis bana " + objName + " uzat\n");
                        client.GetStream().Write(data, 0, data.Length);
                    } catch {
                        AddChatMessage("SYSTEM", "Failed to connect to vision node on port 8005.");
                    }
                }
                else if (cmdType == "CONFIG_GAMEPAD") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnConfigGamepad");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "SHOW_TELEMETRY_POS") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnTelKonum");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "SHOW_TELEMETRY_TORQUE") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnTelTork");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "SHOW_TELEMETRY_TCP") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnTelTcp");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "EXPORT_CSV") {
                    var btn = this.FindControl<Avalonia.Controls.Button>("btnExportCsv");
                    if (btn != null) btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                }
                else if (cmdType == "START_REPAIR_MODE") {
                    var repairWindow = new RepairModeWindow();
                    repairWindow.Show(this);
                }
            } catch {
                if (cmdStr == "GRIPPER_OPEN") {
                    if (robotRenderer != null) robotRenderer.JawGap = 80;
                    var grip_s = this.FindControl<Slider>("grip_s");
                    if (grip_s != null) grip_s.Value = 80;
                }
                else if (cmdStr == "GRIPPER_CLOSE") {
                    if (robotRenderer != null) robotRenderer.JawGap = 0;
                    var grip_s = this.FindControl<Slider>("grip_s");
                    if (grip_s != null) grip_s.Value = 0;
                }
                else if (cmdStr == "MOVE_HOME") {
                    var j7 = this.FindControl<Slider>("j7_s");
                    AnimateToHome(j7, _j1_s, _j2_s, _j3_s, _j4_s, _j5_s, _j6_s);
                }
            }
        }

        private void SendAIMessage(string msg)
        {
            if (_aiClient == null || !_aiClient.Connected) return;

            var cmbLevel = this.FindControl<ComboBox>("cmbEnglishLevel");
            string level = "Beginner";
            if (cmbLevel != null && cmbLevel.SelectedItem is ComboBoxItem cbi) {
                level = cbi.Content?.ToString() ?? "Beginner";
            }

            AddChatMessage("YOU", msg);

            try {
                var payload = new { msg = msg, level = level };
                string jsonStr = System.Text.Json.JsonSerializer.Serialize(payload);
                byte[] data = System.Text.Encoding.UTF8.GetBytes(jsonStr + "\n");
                _aiClient.GetStream().Write(data, 0, data.Length);
            } catch {}
        }

        private void OnRepairModeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                var repairWindow = new RepairModeWindow();
                repairWindow.Show(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine("REPAIR MODE CRASH: " + ex.ToString());
                System.IO.File.WriteAllText("/tmp/repair_mode_crash.txt", ex.ToString());
            }
        }
    }
}