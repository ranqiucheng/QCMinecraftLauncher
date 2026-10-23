using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace MinecraftLauncher.Views
{
    public partial class SkinEditorPage : UserControl
    {
        private const int SkinW = 64, SkinH = 64;
        private byte[] _skinPixels = new byte[SkinW * SkinH * 4];
        private int _texId;
        private OpenTK.GLControl _gl;
        private bool _drawing, _rotating;
        private System.Drawing.Point _lastMouse;
        private float _yaw = 0, _pitch = 0;
        private float _distance = 70f;
        private Color _currentColor = Color.White;
        private readonly Stack<byte[]> _history = new Stack<byte[]>();
        private const int MaxHistory = 30;

        private class FaceInfo
        {
            public Box Box;
            public int Face; // 0=top 1=bottom 2=front 3=back 4=right 5=left
            public PointF[] Screen; // 4 屏幕点
            public float Depth; // 视空间深度（越小越近）
        }
        private class Box
        {
            public Vector3 Center; public float HX, HY, HZ;
            public Rectangle Top, Bottom, Front, Back, Right, Left;
        }
        private readonly List<Box> _boxes = new List<Box>();
        private List<FaceInfo> _faces = new List<FaceInfo>();
        private bool[] _partVisible = { true, true, true, true, true, true };

        public SkinEditorPage()
        {
            InitializeComponent();
            for (int i = 0; i < _skinPixels.Length; i += 4)
            {
                _skinPixels[i] = 40; _skinPixels[i + 1] = 40; _skinPixels[i + 2] = 40; _skinPixels[i + 3] = 255;
            }
            BuildBoxes();
            InitPalette();
            CreateGlControl();
        }

        private void BuildBoxes()
        {
            _boxes.Add(new Box { Center = new Vector3(0, 16, 0), HX = 4, HY = 4, HZ = 4,
                Top = new Rectangle(8, 0, 8, 8), Bottom = new Rectangle(16, 0, 8, 8),
                Right = new Rectangle(0, 8, 8, 8), Front = new Rectangle(8, 8, 8, 8),
                Left = new Rectangle(16, 8, 8, 8), Back = new Rectangle(24, 8, 8, 8) });
            _boxes.Add(new Box { Center = new Vector3(0, 6, 0), HX = 4, HY = 6, HZ = 2,
                Top = new Rectangle(20, 16, 8, 4), Bottom = new Rectangle(28, 16, 8, 4),
                Right = new Rectangle(16, 20, 4, 12), Front = new Rectangle(20, 20, 8, 12),
                Left = new Rectangle(28, 20, 4, 12), Back = new Rectangle(32, 20, 8, 12) });
            _boxes.Add(new Box { Center = new Vector3(6, 6, 0), HX = 2, HY = 6, HZ = 2,
                Top = new Rectangle(44, 16, 4, 4), Bottom = new Rectangle(48, 16, 4, 4),
                Right = new Rectangle(40, 20, 4, 12), Front = new Rectangle(44, 20, 4, 12),
                Left = new Rectangle(48, 20, 4, 12), Back = new Rectangle(52, 20, 4, 12) });
            _boxes.Add(new Box { Center = new Vector3(-6, 6, 0), HX = 2, HY = 6, HZ = 2,
                Top = new Rectangle(36, 48, 4, 4), Bottom = new Rectangle(40, 48, 4, 4),
                Right = new Rectangle(32, 52, 4, 12), Front = new Rectangle(36, 52, 4, 12),
                Left = new Rectangle(40, 52, 4, 12), Back = new Rectangle(44, 52, 4, 12) });
            _boxes.Add(new Box { Center = new Vector3(2, -6, 0), HX = 2, HY = 6, HZ = 2,
                Top = new Rectangle(4, 16, 4, 4), Bottom = new Rectangle(8, 16, 4, 4),
                Right = new Rectangle(0, 20, 4, 12), Front = new Rectangle(4, 20, 4, 12),
                Left = new Rectangle(8, 20, 4, 12), Back = new Rectangle(12, 20, 4, 12) });
            _boxes.Add(new Box { Center = new Vector3(-2, -6, 0), HX = 2, HY = 6, HZ = 2,
                Top = new Rectangle(20, 48, 4, 4), Bottom = new Rectangle(24, 48, 4, 4),
                Right = new Rectangle(16, 52, 4, 12), Front = new Rectangle(20, 52, 4, 12),
                Left = new Rectangle(24, 52, 4, 12), Back = new Rectangle(28, 52, 4, 12) });
        }

        private void InitPalette()
        {
            string[] colors = {
                "#FFFFFF", "#000000", "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF",
                "#FFA500", "#800080", "#008000", "#008080", "#000080", "#808080", "#C0C0C0", "#F5DEB3",
                "#FFC0CB", "#A52A2A", "#8B4513", "#2F4F4F", "#4B0082", "#DDA0DD", "#F0E68C", "#98FB98"
            };
            foreach (var hex in colors)
            {
                var btn = new Button
                {
                    Width = 26, Height = 26, Margin = new Thickness(1.5),
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                    BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(1), Tag = hex
                };
                btn.Click += (s, e) =>
                {
                    string h = (string)((Button)s).Tag;
                    _currentColor = System.Drawing.ColorTranslator.FromHtml(h);
                    ColorHexBox.Text = h;
                };
                ColorGrid.Children.Add(btn);
            }
        }

        private void Part_Changed(object sender, RoutedEventArgs e)
        {
            if (HeadCheck == null || BodyCheck == null || RArmCheck == null ||
                LArmCheck == null || RLegCheck == null || LLegCheck == null) return;
            _partVisible[0] = HeadCheck.IsChecked == true;
            _partVisible[1] = BodyCheck.IsChecked == true;
            _partVisible[2] = RArmCheck.IsChecked == true;
            _partVisible[3] = LArmCheck.IsChecked == true;
            _partVisible[4] = RLegCheck.IsChecked == true;
            _partVisible[5] = LLegCheck.IsChecked == true;
            if (_gl != null && _gl.IsHandleCreated) _gl.Invalidate();
        }

        private void CreateGlControl()
        {
            _gl = new OpenTK.GLControl();
            _gl.Paint += Gl_Paint;
            _gl.Resize += (s, e) => { if (_gl.IsHandleCreated) { _gl.MakeCurrent(); _gl.Invalidate(); } };
            _gl.MouseDown += Gl_MouseDown;
            _gl.MouseUp += Gl_MouseUp;
            _gl.MouseMove += Gl_MouseMove;
            _gl.MouseWheel += Gl_MouseWheel;
            GlHost.Child = _gl;
            _gl.HandleCreated += (s, e) =>
            {
                _gl.MakeCurrent();
                GL.ClearColor(0.1f, 0.1f, 0.12f, 1f);
                GL.Enable(EnableCap.Texture2D);
                GL.Enable(EnableCap.DepthTest);
                UploadTexture();
                _gl.Invalidate();
            };
        }

        private void UploadTexture()
        {
            if (_texId == 0) _texId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _texId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, SkinW, SkinH, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, _skinPixels);
        }

        private void SetupViewport()
        {
            int w = _gl.Width, h = _gl.Height;
            if (h == 0) h = 1;
            GL.Viewport(0, 0, w, h);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45f), (float)w / h, 0.1f, 1000f);
            GL.LoadMatrix(ref proj);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        }

        private void Gl_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            if (!_gl.IsHandleCreated) return;
            _gl.MakeCurrent();
            SetupViewport();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.LoadIdentity();
            GL.Translate(0f, -10f, -_distance);
            GL.Rotate(_pitch, 1f, 0f, 0f);
            GL.Rotate(_yaw, 0f, 1f, 0f);

            // 保存矩阵用于屏幕投影
            float[] mv = new float[16], pr = new float[16];
            GL.GetFloat(GetPName.ModelviewMatrix, mv);
            GL.GetFloat(GetPName.ProjectionMatrix, pr);
            var modelview = new Matrix4(mv[0], mv[1], mv[2], mv[3], mv[4], mv[5], mv[6], mv[7], mv[8], mv[9], mv[10], mv[11], mv[12], mv[13], mv[14], mv[15]);
            var projection = new Matrix4(pr[0], pr[1], pr[2], pr[3], pr[4], pr[5], pr[6], pr[7], pr[8], pr[9], pr[10], pr[11], pr[12], pr[13], pr[14], pr[15]);
            var mvp = modelview * projection;
            int vw = _gl.Width, vh = _gl.Height;

            _faces = new List<FaceInfo>();
            for (int bi = 0; bi < _boxes.Count; bi++)
            {
                if (!_partVisible[bi]) continue;
                var b = _boxes[bi];
                // 每个面（4 顶点）投影到屏幕
                AddFaceScreen(b, 0, b.Top, mvp, vw, vh);
                AddFaceScreen(b, 1, b.Bottom, mvp, vw, vh);
                AddFaceScreen(b, 2, b.Front, mvp, vw, vh);
                AddFaceScreen(b, 3, b.Back, mvp, vw, vh);
                AddFaceScreen(b, 4, b.Right, mvp, vw, vh);
                AddFaceScreen(b, 5, b.Left, mvp, vw, vh);
                DrawBox(b);
            }

            // 网格线（淡）
            GL.Disable(EnableCap.Texture2D);
            GL.LineWidth(1f);
            GL.Color4(0.5f, 0.9f, 0.6f, 0.25f);
            for (int bi = 0; bi < _boxes.Count; bi++) { if (_partVisible[bi]) DrawGrid(_boxes[bi]); }
            GL.Enable(EnableCap.Texture2D);
            _gl.SwapBuffers();
        }

        private Vector3[] GetFaceVerts(Box b, int face)
        {
            float x = b.Center.X, y = b.Center.Y, z = b.Center.Z;
            float hx = b.HX, hy = b.HY, hz = b.HZ;
            switch (face)
            {
                case 0: // top +Y
                    return new[] { new Vector3(x - hx, y + hy, z - hz), new Vector3(x + hx, y + hy, z - hz),
                                   new Vector3(x + hx, y + hy, z + hz), new Vector3(x - hx, y + hy, z + hz) };
                case 1: // bottom -Y
                    return new[] { new Vector3(x - hx, y - hy, z + hz), new Vector3(x + hx, y - hy, z + hz),
                                   new Vector3(x + hx, y - hy, z - hz), new Vector3(x - hx, y - hy, z - hz) };
                case 2: // front -Z
                    return new[] { new Vector3(x - hx, y - hy, z - hz), new Vector3(x + hx, y - hy, z - hz),
                                   new Vector3(x + hx, y + hy, z - hz), new Vector3(x - hx, y + hy, z - hz) };
                case 3: // back +Z
                    return new[] { new Vector3(x + hx, y - hy, z + hz), new Vector3(x - hx, y - hy, z + hz),
                                   new Vector3(x - hx, y + hy, z + hz), new Vector3(x + hx, y + hy, z + hz) };
                case 4: // right +X
                    return new[] { new Vector3(x + hx, y - hy, z - hz), new Vector3(x + hx, y - hy, z + hz),
                                   new Vector3(x + hx, y + hy, z + hz), new Vector3(x + hx, y + hy, z - hz) };
                default: // left -X
                    return new[] { new Vector3(x - hx, y - hy, z + hz), new Vector3(x - hx, y - hy, z - hz),
                                   new Vector3(x - hx, y + hy, z - hz), new Vector3(x - hx, y + hy, z + hz) };
            }
        }

        private void AddFaceScreen(Box b, int face, Rectangle uv, Matrix4 mvp, int vw, int vh)
        {
            var v = GetFaceVerts(b, face);
            var screen = new PointF[4];
            float depth = 0f;
            for (int i = 0; i < 4; i++)
            {
                var vec = new Vector4(v[i].X, v[i].Y, v[i].Z, 1f);
                var clip = Vector4.Transform(vec, mvp);
                float nx = clip.X / clip.W, ny = clip.Y / clip.W;
                screen[i] = new PointF((nx + 1f) * 0.5f * vw, (1f - (ny + 1f) * 0.5f) * vh);
                depth += clip.W;
            }
            _faces.Add(new FaceInfo { Box = b, Face = face, Screen = screen, Depth = depth / 4f });
        }

        private void DrawBox(Box b)
        {
            for (int f = 0; f < 6; f++)
            {
                var v = GetFaceVerts(b, f);
                Rectangle uv = GetFaceRect2(b, f);
                GL.BindTexture(TextureTarget.Texture2D, _texId);
                GL.Color3(1f, 1f, 1f);
                GL.Begin(PrimitiveType.Quads);
                GL.TexCoord2(uv.Left / 64f, uv.Bottom / 64f); GL.Vertex3(v[0].X, v[0].Y, v[0].Z);
                GL.TexCoord2(uv.Right / 64f, uv.Bottom / 64f); GL.Vertex3(v[1].X, v[1].Y, v[1].Z);
                GL.TexCoord2(uv.Right / 64f, uv.Top / 64f); GL.Vertex3(v[2].X, v[2].Y, v[2].Z);
                GL.TexCoord2(uv.Left / 64f, uv.Top / 64f); GL.Vertex3(v[3].X, v[3].Y, v[3].Z);
                GL.End();
            }
        }

        private Rectangle GetFaceRect2(Box b, int face)
        {
            switch (face)
            {
                case 0: return b.Top; case 1: return b.Bottom; case 2: return b.Front;
                case 3: return b.Back; case 4: return b.Right; default: return b.Left;
            }
        }

        private void DrawGrid(Box b)
        {
            for (int f = 0; f < 6; f++)
            {
                var v = GetFaceVerts(b, f);
                Rectangle uv = GetFaceRect2(b, f);
                Vector3 normal = FaceNormal(f);
                Vector3 off = normal * 0.02f;
                GL.Begin(PrimitiveType.Lines);
                // 横线
                for (int i = 1; i < uv.Height; i++)
                {
                    float t = (float)i / uv.Height;
                    var p0 = Lerp(v[0], v[3], t) + off;
                    var p1 = Lerp(v[1], v[2], t) + off;
                    GL.Vertex3(p0.X, p0.Y, p0.Z); GL.Vertex3(p1.X, p1.Y, p1.Z);
                }
                // 竖线
                for (int i = 1; i < uv.Width; i++)
                {
                    float t = (float)i / uv.Width;
                    var p0 = Lerp(v[0], v[1], t) + off;
                    var p1 = Lerp(v[3], v[2], t) + off;
                    GL.Vertex3(p0.X, p0.Y, p0.Z); GL.Vertex3(p1.X, p1.Y, p1.Z);
                }
                GL.End();
            }
        }

        private Vector3 FaceNormal(int face)
        {
            switch (face)
            {
                case 0: return new Vector3(0, 1, 0);
                case 1: return new Vector3(0, -1, 0);
                case 2: return new Vector3(0, 0, -1);
                case 3: return new Vector3(0, 0, 1);
                case 4: return new Vector3(1, 0, 0);
                default: return new Vector3(-1, 0, 0);
            }
        }

        private static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            return new Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        // ===== 鼠标 =====
        private void Gl_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            _lastMouse = e.Location;
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                PushHistory();
                _drawing = true;
                PaintAt(e.X, e.Y);
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right) _rotating = true;
        }

        private void Gl_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            _distance -= e.Delta * 0.03f;
            if (_distance < 25f) _distance = 25f;
            if (_distance > 150f) _distance = 150f;
            _gl.Invalidate();
        }

        private void Gl_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            _drawing = false; _rotating = false;
        }

        private void Gl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_rotating)
            {
                _yaw += (e.X - _lastMouse.X) * 0.5f;
                _pitch += (e.Y - _lastMouse.Y) * 0.5f;
                _lastMouse = e.Location;
                _gl.Invalidate();
            }
            else if (_drawing) PaintAt(e.X, e.Y);
        }

        private void PaintAt(int mx, int my)
        {
            PointF pt = new PointF(mx, my);
            FaceInfo best = null;
            float u = 0, v = 0;
            float bestDepth = float.MaxValue;
            foreach (var f in _faces)
            {
                if (!PointInQuad(pt, f.Screen)) continue;
                float fu, fv;
                if (!QuadUV(f.Screen, pt, out fu, out fv)) continue;
                if (f.Depth < bestDepth)
                {
                    bestDepth = f.Depth;
                    best = f;
                    u = fu; v = fv;
                }
            }
            if (best == null) { StatusText.Text = "未命中模型"; return; }
            Rectangle uvRect = GetFaceRect2(best.Box, best.Face);
            int px = (int)(uvRect.Left + u * uvRect.Width);
            int py = (int)(uvRect.Top + (1 - v) * uvRect.Height);
            if (px < 0 || py < 0 || px >= SkinW || py >= SkinH) return;
            ApplyPixel(px, py);
        }

        private bool PointInQuad(PointF p, PointF[] q)
        {
            return PointInTri(p, q[0], q[1], q[2]) || PointInTri(p, q[0], q[2], q[3]);
        }

        private bool PointInTri(PointF p, PointF a, PointF b, PointF c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        private float Sign(PointF p1, PointF p2, PointF p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        private bool QuadUV(PointF[] q, PointF p, out float u, out float v)
        {
            // 用三角形 (0,1,2) 和 (0,2,3) 求重心坐标
            float b0, b1, b2;
            if (Barycentric(p, q[0], q[1], q[2], out b0, out b1, out b2))
            {
                // 顶点 0=(0,0) 1=(1,0) 2=(1,1) 3=(0,1)
                u = b1 + b2; v = b2;
                return true;
            }
            if (Barycentric(p, q[0], q[2], q[3], out b0, out b1, out b2))
            {
                u = b1; v = b1 + b2;
                return true;
            }
            u = 0; v = 0;
            return false;
        }

        private bool Barycentric(PointF p, PointF a, PointF b, PointF c, out float w0, out float w1, out float w2)
        {
            float d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(d) < 1e-9f) { w0 = w1 = w2 = 0; return false; }
            w0 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / d;
            w1 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / d;
            w2 = 1 - w0 - w1;
            return true;
        }

        private void ApplyPixel(int px, int py)
        {
            if (PickerRadio.IsChecked == true)
            {
                int idx = (py * SkinW + px) * 4;
                _currentColor = Color.FromArgb(_skinPixels[idx + 3], _skinPixels[idx + 2], _skinPixels[idx + 1], _skinPixels[idx]);
                ColorHexBox.Text = "#" + _currentColor.R.ToString("X2") + _currentColor.G.ToString("X2") + _currentColor.B.ToString("X2");
                return;
            }
            Color color = EraserRadio.IsChecked == true ? Color.Transparent : _currentColor;
            int i2 = (py * SkinW + px) * 4;
            _skinPixels[i2] = color.B; _skinPixels[i2 + 1] = color.G;
            _skinPixels[i2 + 2] = color.R; _skinPixels[i2 + 3] = color.A;
            UploadTexture();
            _gl.Invalidate();
            StatusText.Text = "画了 (" + px + "," + py + ")";
        }

        private void PushHistory()
        {
            var snap = new byte[SkinW * SkinH * 4];
            Array.Copy(_skinPixels, snap, snap.Length);
            _history.Push(snap);
            while (_history.Count > MaxHistory) { var arr = _history.ToArray(); _history.Clear(); for (int i = 0; i < MaxHistory; i++) if (i < arr.Length - 1) _history.Push(arr[i]); }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_history.Count == 0) { StatusText.Text = "没有可撤销的操作"; return; }
            Array.Copy(_history.Pop(), _skinPixels, _skinPixels.Length);
            UploadTexture(); _gl.Invalidate();
            StatusText.Text = "已撤销";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PNG 皮肤 (*.png)|*.png", FileName = "skin.png" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var bmp = new Bitmap(SkinW, SkinH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bd = bmp.LockBits(new Rectangle(0, 0, SkinW, SkinH), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(_skinPixels, 0, bd.Scan0, _skinPixels.Length);
                bmp.UnlockBits(bd);
                bmp.Save(dlg.FileName, ImageFormat.Png);
                StatusText.Text = "已保存：" + Path.GetFileName(dlg.FileName);
            }
            catch (Exception ex) { StatusText.Text = "保存失败：" + ex.Message; }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            PushHistory();
            for (int i = 0; i < _skinPixels.Length; i++) _skinPixels[i] = 0;
            UploadTexture(); _gl.Invalidate();
        }
    }
}