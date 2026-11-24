using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Canli.Goruntu
{
    // Süreç DPI-aware olmalı (Program.Main SetProcessDPIAware çağırır); dikdörtgenler fiziksel pikseldir.
    public static class Yakalayici
    {
        public static Bitmap Al(Rectangle ekranPikseli)
        {
            var b = new Bitmap(ekranPikseli.Width, ekranPikseli.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
                g.CopyFromScreen(ekranPikseli.X, ekranPikseli.Y, 0, 0, ekranPikseli.Size);
            return b;
        }

        public static byte[] KucukImza(Rectangle r)
        {
            using (var tam = Al(r)) return KucukImza(tam);
        }

        public static byte[] KucukImza(Bitmap tam)
        {
            using (var k = new Bitmap(64, 36, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(k))
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.DrawImage(tam, 0, 0, 64, 36);
                }
                var d = k.LockBits(new Rectangle(0, 0, 64, 36), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var px = new int[64 * 36];
                Marshal.Copy(d.Scan0, px, 0, px.Length);
                k.UnlockBits(d);
                var o = new byte[px.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    int p = px[i];
                    o[i] = (byte)((((p >> 16) & 255) + ((p >> 8) & 255) + (p & 255)) / 3);
                }
                return o;
            }
        }

        // Piksel farkı >14 olanların oranı.
        public static double Degisim(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 1;
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i] - b[i]) > 14) n++;
            return (double)n / a.Length;
        }
    }
}
