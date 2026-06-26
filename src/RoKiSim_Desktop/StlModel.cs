using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace RoKiSim_Desktop
{
    public struct Triangle
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;
    }

    public class StlModel
    {
        public List<Triangle> Triangles { get; } = new List<Triangle>();

        public static StlModel Load(string path)
        {
            using var fs = File.OpenRead(path);
            // Try to detect ASCII vs binary
            byte[] header = new byte[80];
            int read = fs.Read(header, 0, header.Length);
            fs.Seek(0, SeekOrigin.Begin);

            var headerText = System.Text.Encoding.ASCII.GetString(header).Trim();
            if (headerText.StartsWith("solid", StringComparison.OrdinalIgnoreCase))
            {
                // Try ASCII parse first
                try
                {
                    var text = File.ReadAllText(path);
                    return LoadAscii(text);
                }
                catch
                {
                    // fallback to binary
                }
            }

            return LoadBinary(fs);
        }

        private static StlModel LoadAscii(string content)
        {
            var model = new StlModel();
            using var sr = new StringReader(content);
            string? line;
            var verts = new List<Vector3>();
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    {
                        verts.Add(new Vector3(x, y, z));
                        if (verts.Count == 3)
                        {
                            model.Triangles.Add(new Triangle { A = verts[0], B = verts[1], C = verts[2] });
                            verts.Clear();
                        }
                    }
                }
            }
            return model;
        }

        private static StlModel LoadBinary(Stream fs)
        {
            var model = new StlModel();
            using var br = new BinaryReader(fs);
            // skip 80-byte header
            br.ReadBytes(80);
            uint triCount = br.ReadUInt32();
            for (uint i = 0; i < triCount; i++)
            {
                try
                {
                    // normal
                    float nx = br.ReadSingle();
                    float ny = br.ReadSingle();
                    float nz = br.ReadSingle();
                    var a = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    var b = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    var c = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    ushort attr = br.ReadUInt16();
                    model.Triangles.Add(new Triangle { A = a, B = b, C = c });
                }
                catch
                {
                    break;
                }
            }
            return model;
        }
    }
}
