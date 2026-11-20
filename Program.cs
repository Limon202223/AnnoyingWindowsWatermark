using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ActivateWindows
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WatermarkForm());
        }
    }

    public class WatermarkForm : Form
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        private string titleText = "Windows'u Etkinleştir";
        private string subText = "Windows'u etkinleştirmek için Ayarlar'a gidin.";
        private System.Windows.Forms.Timer topMostTimer;

        public WatermarkForm()
        {
            LoadOriginalSystem32Strings();
            SetupWindow();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }

        private void LoadOriginalSystem32Strings()
        {
            string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string[] candidateDlls = { "slc.dll.mui", "sppc.dll.mui", "shell32.dll.mui" };

            bool found = false;
            foreach (string dll in candidateDlls)
            {
                string muiPath = Path.Combine(sys32, "tr-TR", dll);
                if (!File.Exists(muiPath)) continue;

                IntPtr hMod = LoadLibraryEx(muiPath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
                if (hMod != IntPtr.Zero)
                {
                    for (uint id = 1; id < 40000; id++)
                    {
                        StringBuilder sb = new StringBuilder(256);
                        if (LoadString(hMod, id, sb, sb.Capacity) > 0)
                        {
                            string val = sb.ToString();
                            if (val == "Windows'u Etkinleştir")
                            {
                                titleText = val;
                                StringBuilder sbSub = new StringBuilder(256);
                                if (LoadString(hMod, id + 1, sbSub, sbSub.Capacity) > 0 && sbSub.ToString().Contains("Ayarlar"))
                                    subText = sbSub.ToString();
                                found = true;
                                break;
                            }
                        }
                    }
                    FreeLibrary(hMod);
                }
                if (found) break;
            }
        }

        private void SetupWindow()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Width = 410;
            this.Height = 75;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(wa.Right - this.Width - 12, wa.Bottom - this.Height - 25);

            RenderCrispWatermark();
            ForceOverlayTop();

            topMostTimer = new System.Windows.Forms.Timer();
            topMostTimer.Interval = 250;
            topMostTimer.Tick += (s, e) => ForceOverlayTop();
            topMostTimer.Start();
        }

        private void ForceOverlayTop()
        {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private void RenderCrispWatermark()
        {
            using (Bitmap bitmap = new Bitmap(this.Width, this.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.Clear(Color.Transparent);

                using (Font fontTitle = new Font("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Point))
                using (Font fontSub = new Font("Segoe UI", 11.25f, FontStyle.Regular, GraphicsUnit.Point))
                using (Brush watermarkBrush = new SolidBrush(Color.FromArgb(145, 225, 225, 225)))
                {
                    g.DrawString(titleText, fontTitle, watermarkBrush, new PointF(8, 6));
                    g.DrawString(subText, fontSub, watermarkBrush, new PointF(9, 34));
                }

                SetBitmap(bitmap);
            }
        }

        private void SetBitmap(Bitmap bitmap)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                Size size = new Size(bitmap.Width, bitmap.Height);
                Point pointSource = new Point(0, 0);
                Point topPos = new Point(this.Left, this.Top);
                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
            }
        }
    }
}