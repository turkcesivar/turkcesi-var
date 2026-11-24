using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Canli.Goruntu
{
    public enum RenkKipi { Beyaz, Sari, Ikisi }

    public static class RenkSuzgeci
    {
        // Altyazı rengindeki pikselleri siyah, gerisini beyaz yapar; yazı piksellerinin FNV imzasını verir.
        public static Bitmap Temizle(Bitmap kaynak, RenkKipi kip, int esik, out long imza, out bool tamamenSiyah)
        {
            int w = kaynak.Width, h = kaynak.Height;
            var cikis = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            try
            {
                int n = w * h;
                var px = new int[n];
                var a = kaynak.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try { Marshal.Copy(a.Scan0, px, 0, n); }
                finally { kaynak.UnlockBits(a); }
                long hs = 1469598103934665603L;
                bool siyah = true;
                for (int i = 0; i < n; i++)
                {
                    int p = px[i];
                    int r = (p >> 16) & 255, g = (p >> 8) & 255, bl = p & 255;
                    if (r > 8 || g > 8 || bl > 8) siyah = false;
                    int mn = Math.Min(r, Math.Min(g, bl)), mx = Math.Max(r, Math.Max(g, bl));
                    bool beyaz = mn >= esik && (mx - mn) < 60;
                    bool sari = r >= 210 && g >= 190 && bl <= 130 && (g - bl) >= 90;
                    bool yazi = kip == RenkKipi.Beyaz ? beyaz : kip == RenkKipi.Sari ? sari : (beyaz || sari);
                    px[i] = yazi ? unchecked((int)0xFF000000) : unchecked((int)0xFFFFFFFF);
                    if (yazi) hs = unchecked((hs ^ i) * 1099511628211L);
                }
                var b = cikis.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try { Marshal.Copy(px, 0, b.Scan0, n); }
                finally { cikis.UnlockBits(b); }
                imza = hs;
                tamamenSiyah = siyah;
                return cikis;
            }
            catch { cikis.Dispose(); throw; }
        }
    }
}
