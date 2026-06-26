using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace RoKiSim_Desktop
{
    public class RobotRenderer : Control
    {
        public List<StlModel> Models { get; set; } = new List<StlModel>();
        public double Angle { get; set; } = -135; // Azimuth (Ön Çapraz Açı)
        public double Elevation { get; set; } = 25;
        public double ZoomScale { get; set; } = 0.8;
        public double J1 { get; set; } = 0;
        public double J2 { get; set; } = -25.78;
        public double J3 { get; set; } = 68.75;
        public double J4 { get; set; } = 11.46;
        public double J5 { get; set; } = 20.05;
        public double J6 { get; set; } = 0;
        public double J7 { get; set; } = 0; // Linear Track
        public double JawGap { get; set; } = 30; // Gripper Jaw Gap
        public bool ShowAxes { get; set; } = false;
        public bool ShowWorkspace { get; set; } = false;

        private Point _lastMousePos;
        private bool _isDragging = false;

        public RobotRenderer()
        {
            ClipToBounds = true;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (_isDragging)
            {
                var pos = e.GetPosition(this);
                var dx = pos.X - _lastMousePos.X;
                var dy = pos.Y - _lastMousePos.Y;
                Angle -= dx * 0.5;
                Elevation -= dy * 0.5;
                if (Elevation > 89) Elevation = 89;
                if (Elevation < -89) Elevation = -89;
                
                _lastMousePos = pos;
                InvalidateVisual();
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            _isDragging = false;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            ZoomScale += e.Delta.Y * 0.1;
            if (ZoomScale < 0.1) ZoomScale = 0.1;
            if (ZoomScale > 5.0) ZoomScale = 5.0;
            InvalidateVisual();
        }

        public Matrix4x4[] GetTransforms()
        {
            var matrices = new Matrix4x4[7];

            // base_link (index 0) - moving along Y axis on the track
            Matrix4x4 m0 = Matrix4x4.CreateTranslation(0, (float)J7, 0);
            matrices[0] = m0;

            // link_1 (index 1) - Joint 1 (Z-axis)
            Matrix4x4 local1 = Matrix4x4.CreateRotationZ((float)(-J1 * Math.PI / 180.0)) * Matrix4x4.CreateTranslation(0, 0, 330);
            Matrix4x4 m1 = local1 * m0;
            matrices[1] = m1;

            // link_2 (index 2) - Joint 2 (Y-axis)
            Matrix4x4 local2 = Matrix4x4.CreateRotationY((float)(J2 * Math.PI / 180.0)) * Matrix4x4.CreateTranslation(50, 0, 0);
            Matrix4x4 m2 = local2 * m1;
            matrices[2] = m2;

            // link_3 (index 3) - Joint 3 (Y-axis)
            Matrix4x4 local3 = Matrix4x4.CreateRotationY((float)(J3 * Math.PI / 180.0)) * Matrix4x4.CreateTranslation(0, 0, 330);
            Matrix4x4 m3 = local3 * m2;
            matrices[3] = m3;

            // link_4 (index 4) - Joint 4 (X-axis)
            Matrix4x4 local4 = Matrix4x4.CreateRotationX((float)(-J4 * Math.PI / 180.0)) * Matrix4x4.CreateTranslation(0, 0, 35);
            Matrix4x4 m4 = local4 * m3;
            matrices[4] = m4;

            // link_5 (index 5) - Joint 5 (Y-axis)
            Matrix4x4 local5 = Matrix4x4.CreateRotationY((float)(-J5 * Math.PI / 180.0)) * Matrix4x4.CreateTranslation(335, 0, 0);
            Matrix4x4 m5 = local5 * m4;
            matrices[5] = m5;

            // link_6 (index 6) - Joint 6 (X-axis)
            Matrix4x4 local6 = Matrix4x4.CreateRotationX((float)(-J6 * Math.PI / 180.0)) * Matrix4x4.CreateTranslation(80, 0, 0);
            Matrix4x4 m6 = local6 * m5;
            matrices[6] = m6;

            return matrices;
        }

        private static IBrush[] _goldBrushes;
        private static Pen[] _goldPens;

        static RobotRenderer()
        {
            _goldBrushes = new IBrush[64];
            _goldPens = new Pen[64];
            for (int i = 0; i < 64; i++)
            {
                float intensity = 0.2f + 0.8f * (i / 63f);
                var brush = new SolidColorBrush(Color.FromRgb((byte)(255 * intensity), (byte)(215 * intensity), 0));
                _goldBrushes[i] = brush;
                _goldPens[i] = new Pen(brush, 0.5);
            }
        }

        private void AddBox(List<(Point A, Point B, Point C, double depth, IBrush brush, Pen pen)> tris, 
            Matrix4x4 transform, float sx, float sy, float sz, IBrush baseBrush, Matrix4x4 viewMatrix, double cx, double cy, Vector3 lightDir)
        {
            var v = new Vector3[8];
            float hx = sx/2, hy = sy/2, hz = sz/2;
            v[0] = new Vector3(-hx, -hy, -hz);
            v[1] = new Vector3(hx, -hy, -hz);
            v[2] = new Vector3(hx, hy, -hz);
            v[3] = new Vector3(-hx, hy, -hz);
            v[4] = new Vector3(-hx, -hy, hz);
            v[5] = new Vector3(hx, -hy, hz);
            v[6] = new Vector3(hx, hy, hz);
            v[7] = new Vector3(-hx, hy, hz);

            var finalMatrix = transform * viewMatrix;

            void AddFace(int i1, int i2, int i3, int i4)
            {
                ProcessTriangle(v[i1], v[i2], v[i3]);
                ProcessTriangle(v[i1], v[i3], v[i4]);
            }

            void ProcessTriangle(Vector3 A, Vector3 B, Vector3 C)
            {
                var a_rot = Vector3.Transform(A, finalMatrix);
                var b_rot = Vector3.Transform(B, finalMatrix);
                var c_rot = Vector3.Transform(C, finalMatrix);

                var u = b_rot - a_rot;
                var v_vec = c_rot - a_rot;
                var normal = Vector3.Normalize(Vector3.Cross(u, v_vec));

                if (normal.Y > 0) return;

                float intensity = Math.Max(0.2f, Vector3.Dot(normal, lightDir));
                var color = ((SolidColorBrush)baseBrush).Color;
                byte r = (byte)(color.R * intensity);
                byte g = (byte)(color.G * intensity);
                byte b_col = (byte)(color.B * intensity);
                var brush = new SolidColorBrush(Color.FromRgb(r, g, b_col));
                var pen = new Pen(brush, 0.5);

                var pa = Project(a_rot, cx, cy, ZoomScale);
                var pb = Project(b_rot, cx, cy, ZoomScale);
                var pc = Project(c_rot, cx, cy, ZoomScale);

                double avgDepth = (a_rot.Y + b_rot.Y + c_rot.Y) / 3.0;
                tris.Add((pa, pb, pc, avgDepth, brush, pen));
            }

            AddFace(3, 2, 1, 0); // Bottom
            AddFace(4, 5, 6, 7); // Top
            AddFace(0, 1, 5, 4); // Front
            AddFace(1, 2, 6, 5); // Right
            AddFace(2, 3, 7, 6); // Back
            AddFace(3, 0, 4, 7); // Left
        }

        public override void Render(DrawingContext context)
        {
            // Almost transparent background for Hit Testing! (Avalonia requires Alpha > 0)
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), Bounds);

            double cx = Bounds.Width / 2.0;
            double cy = Bounds.Height / 2.0;
            double zoom = ZoomScale;
            
            var viewMatrix = Matrix4x4.CreateRotationZ((float)(-Angle * Math.PI / 180.0)) * 
                             Matrix4x4.CreateRotationX((float)(-Elevation * Math.PI / 180.0));

            // Push the camera far enough away to avoid negative Y causing division by zero in perspective!
            viewMatrix *= Matrix4x4.CreateTranslation(0, 3000, 0);

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 1);
            for (int i = -1000; i <= 1000; i += 200)
            {
                var p1 = Project(Vector3.Transform(new Vector3(i, -1000, 0), viewMatrix), cx, cy, zoom);
                var p2 = Project(Vector3.Transform(new Vector3(i, 1000, 0), viewMatrix), cx, cy, zoom);
                var p3 = Project(Vector3.Transform(new Vector3(-1000, i, 0), viewMatrix), cx, cy, zoom);
                var p4 = Project(Vector3.Transform(new Vector3(1000, i, 0), viewMatrix), cx, cy, zoom);
                context.DrawLine(gridPen, p1, p2);
                context.DrawLine(gridPen, p3, p4);
            }

            var lightDir = Vector3.Normalize(new Vector3(0.5f, -0.8f, 1.0f));
            
            var railTris = new List<(Point A, Point B, Point C, double depth, IBrush brush, Pen pen)>(20);
            var projectedTris = new List<(Point A, Point B, Point C, double depth, IBrush brush, Pen pen)>(50000);
            
            // Draw Linear Rail (7th Axis)
            var railBrush = new SolidColorBrush(Color.Parse("#1A1A1A")); // Darker track
            AddBox(railTris, Matrix4x4.CreateTranslation(0, 0, -25), 300, 2200, 50, railBrush, viewMatrix, cx, cy, lightDir);

            if (Models.Count > 0)
            {
                var matrices = GetTransforms();
                var baseScale = Matrix4x4.CreateScale(1000f);

                var threadLists = new List<(Point A, Point B, Point C, double depth, IBrush brush, Pen pen)>[Models.Count];

                for (int i = 0; i < Models.Count; i++)
                {
                    var transform = i < matrices.Length ? matrices[i] : matrices[matrices.Length - 1];
                    var finalMatrix = baseScale * transform * viewMatrix;
                    
                    var localList = new List<(Point A, Point B, Point C, double depth, IBrush brush, Pen pen)>(Models[i].Triangles.Count);

                    foreach (var tri in Models[i].Triangles)
                    {
                        var a_rot = Vector3.Transform(tri.A, finalMatrix);
                        var b_rot = Vector3.Transform(tri.B, finalMatrix);
                        var c_rot = Vector3.Transform(tri.C, finalMatrix);

                        var u = b_rot - a_rot;
                        var v_vec = c_rot - a_rot;
                        var normal = Vector3.Normalize(Vector3.Cross(u, v_vec));

                        if (normal.Y > 0) continue;

                        float intensity = Math.Max(0.2f, Vector3.Dot(normal, lightDir));
                        int cacheIndex = (int)((intensity - 0.2f) / 0.8f * 63f);
                        if (cacheIndex < 0) cacheIndex = 0;
                        if (cacheIndex > 63) cacheIndex = 63;

                        var brush = _goldBrushes[cacheIndex];
                        var pen = _goldPens[cacheIndex];

                        var pa = Project(a_rot, cx, cy, zoom);
                        var pb = Project(b_rot, cx, cy, zoom);
                        var pc = Project(c_rot, cx, cy, zoom);

                        double avgDepth = (a_rot.Y + b_rot.Y + c_rot.Y) / 3.0;
                        localList.Add((pa, pb, pc, avgDepth, brush, pen));
                    }
                    threadLists[i] = localList;
                }

                int totalTris = 0;
                for (int i = 0; i < Models.Count; i++) totalTris += threadLists[i].Count;

                if (projectedTris.Capacity < totalTris + 50)
                    projectedTris.Capacity = totalTris + 50;

                for (int i = 0; i < Models.Count; i++)
                {
                    projectedTris.AddRange(threadLists[i]);
                }

                // Draw Gripper attached to Flange (m6)
                var flangeMatrix = matrices[6];
                
                // End effector colors: Yellow and Black
                var gripperBaseBrush = new SolidColorBrush(Color.Parse("#FFD700")); // Yellow base
                var fingerBrush = new SolidColorBrush(Color.Parse("#111111")); // Black fingers
                
                // Gripper base (centered at X=40, width=80, height=40, depth=40)
                AddBox(projectedTris, Matrix4x4.CreateTranslation(40, 0, 0) * flangeMatrix, 80, 80, 80, gripperBaseBrush, viewMatrix, cx, cy, lightDir);
                
                // Finger 1 (JawGap)
                AddBox(projectedTris, Matrix4x4.CreateTranslation(120, (float)(JawGap/2 + 10), 0) * flangeMatrix, 80, 20, 20, fingerBrush, viewMatrix, cx, cy, lightDir);
                // Finger 2 (-JawGap)
                AddBox(projectedTris, Matrix4x4.CreateTranslation(120, (float)(-JawGap/2 - 10), 0) * flangeMatrix, 80, 20, 20, fingerBrush, viewMatrix, cx, cy, lightDir);
            }

            // Painter's algorithm for rail
            railTris.Sort((t1, t2) => t2.depth.CompareTo(t1.depth));
            foreach (var t in railTris)
            {
                var geometry = new StreamGeometry();
                using (var gc = geometry.Open())
                {
                    gc.BeginFigure(t.A, true);
                    gc.LineTo(t.B);
                    gc.LineTo(t.C);
                    gc.EndFigure(true);
                }
                context.DrawGeometry(t.brush, t.pen, geometry);
            }

            // Painter's algorithm for robot
            projectedTris.Sort((t1, t2) => t2.depth.CompareTo(t1.depth));
            
            StreamGeometry? currentGeo = null;
            StreamGeometryContext? currentGc = null;
            IBrush? currentBrush = null;

            foreach (var t in projectedTris)
            {
                if (t.brush != currentBrush || currentGeo == null || currentGc == null)
                {
                    if (currentGc != null) {
                        currentGc.Dispose();
                        context.DrawGeometry(currentBrush, null, currentGeo); // Removed t.pen
                    }
                    currentBrush = t.brush;
                    currentGeo = new StreamGeometry();
                    currentGc = currentGeo.Open();
                }
                currentGc.BeginFigure(t.A, true);
                currentGc.LineTo(t.B);
                currentGc.LineTo(t.C);
                currentGc.EndFigure(true);
            }
            if (currentGc != null && currentGeo != null) {
                currentGc.Dispose();
                context.DrawGeometry(currentBrush, null, currentGeo);
            }

            // Draw Workspace Hemisphere
            if (ShowWorkspace)
            {
                var wsPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 255, 255)), 1.5);
                float radius = 1192;
                for (int i = 0; i < 360; i += 30)
                {
                    var prevP = new Point();
                    bool first = true;
                    for (int j = 0; j <= 90; j += 10)
                    {
                        double lat = j * Math.PI / 180.0;
                        double lon = i * Math.PI / 180.0;
                        float x = (float)(radius * Math.Cos(lat) * Math.Cos(lon));
                        float y = (float)(radius * Math.Cos(lat) * Math.Sin(lon));
                        float z = (float)(radius * Math.Sin(lat));
                        var p = Project(Vector3.Transform(new Vector3(x, y, z), viewMatrix), cx, cy, zoom);
                        if (!first) context.DrawLine(wsPen, prevP, p);
                        prevP = p;
                        first = false;
                    }
                }
                for (int j = 0; j <= 90; j += 15)
                {
                    var prevP = new Point();
                    var firstP = new Point();
                    bool first = true;
                    for (int i = 0; i < 360; i += 10)
                    {
                        double lat = j * Math.PI / 180.0;
                        double lon = i * Math.PI / 180.0;
                        float x = (float)(radius * Math.Cos(lat) * Math.Cos(lon));
                        float y = (float)(radius * Math.Cos(lat) * Math.Sin(lon));
                        float z = (float)(radius * Math.Sin(lat));
                        var p = Project(Vector3.Transform(new Vector3(x, y, z), viewMatrix), cx, cy, zoom);
                        if (first) firstP = p;
                        else context.DrawLine(wsPen, prevP, p);
                        prevP = p;
                        first = false;
                    }
                    context.DrawLine(wsPen, prevP, firstP);
                }
            }

            // Draw Axes
            if (ShowAxes)
            {
                var matrices = GetTransforms();
                var penX = new Pen(new SolidColorBrush(Colors.Red), 3);
                var penY = new Pen(new SolidColorBrush(Colors.Lime), 3);
                var penZ = new Pen(new SolidColorBrush(Colors.DodgerBlue), 3);
                
                for (int i = 0; i < matrices.Length; i++)
                {
                    var mat = matrices[i] * viewMatrix;
                    var p0 = Project(Vector3.Transform(new Vector3(0, 0, 0), mat), cx, cy, zoom);
                    var px = Project(Vector3.Transform(new Vector3(150, 0, 0), mat), cx, cy, zoom);
                    var py = Project(Vector3.Transform(new Vector3(0, 150, 0), mat), cx, cy, zoom);
                    var pz = Project(Vector3.Transform(new Vector3(0, 0, 150), mat), cx, cy, zoom);
                    
                    context.DrawLine(penX, p0, px);
                    context.DrawLine(penY, p0, py);
                    context.DrawLine(penZ, p0, pz);
                }
            }
        }

        private Point Project(Vector3 v, double cx, double cy, double scale)
        {
            // v.X is right, v.Y is depth, v.Z is UP
            double perspective = 1.0 / (1.0 + (v.Y * 0.0005));
            double x = cx + v.X * scale * perspective;
            double y = cy + 100 - v.Z * scale * perspective; // Z is vertical (UP)
            return new Point(x, y);
        }
    }
}
