using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Canli.Goruntu
{
    /// <summary>Altyazı rengi. Ikisi (varsayılan, protokolde "ikisi"): bilinen bütün renkler (beyaz, sarı, turuncu).</summary>
    public enum RenkKipi { Beyaz, Sari, Ikisi, Turuncu }

    public static class RenkSuzgeci
    {
        /// <summary>Algılanan renge yakınlık payı (kanal başına en büyük fark).</summary>
        public const int OzelRenkPayi = 45;

        // Altyazı rengindeki pikselleri siyah, gerisini beyaz yapar; yazı piksellerinin FNV imzasını verir.
        public static Bitmap Temizle(Bitmap kaynak, RenkKipi kip, int esik, out long imza, out bool tamamenSiyah)
        {
            return Uygula(kaynak, (r, g, bl) =>
            {
                int mn = Math.Min(r, Math.Min(g, bl)), mx = Math.Max(r, Math.Max(g, bl));
                bool beyaz = mn >= esik && (mx - mn) < 60;
                bool sari = r >= 210 && g >= 190 && bl <= 130 && (g - bl) >= 90;
                // Turuncu/kırmızımsı (ör. Metro Exodus ≈ 202,66,0): kırmızı belirgin, mavi düşük, yeşilden çok uzak.
                bool turuncu = r >= 150 && bl <= 70 && (r - g) >= 70 && (r - bl) >= 120;
                return kip == RenkKipi.Beyaz ? beyaz : kip == RenkKipi.Sari ? sari : kip == RenkKipi.Turuncu ? turuncu
                    : (beyaz || sari || turuncu);
            }, out imza, out tamamenSiyah);
        }

        /// <summary>Otomatik algılanan altyazı rengine (± OzelRenkPayi) yakın pikseller yazı sayılır.</summary>
        public static Bitmap TemizleRenk(Bitmap kaynak, Color hedef, out long imza, out bool tamamenSiyah)
        {
            return TemizleRenkler(kaynak, new[] { hedef }, out imza, out tamamenSiyah);
        }

        /// <summary>Öğrenilen renklerden herhangi birine yakın pikseller yazı sayılır.</summary>
        public static Bitmap TemizleRenkler(Bitmap kaynak, System.Collections.Generic.IReadOnlyList<Color> hedefler, out long imza, out bool tamamenSiyah)
        {
            var h = new int[hedefler.Count * 3];
            for (int i = 0; i < hedefler.Count; i++) { h[i * 3] = hedefler[i].R; h[i * 3 + 1] = hedefler[i].G; h[i * 3 + 2] = hedefler[i].B; }
            return Uygula(kaynak, (r, g, bl) =>
            {
                for (int i = 0; i < h.Length; i += 3)
                    if (Math.Abs(r - h[i]) <= OzelRenkPayi && Math.Abs(g - h[i + 1]) <= OzelRenkPayi && Math.Abs(bl - h[i + 2]) <= OzelRenkPayi)
                        return true;
                return false;
            }, out imza, out tamamenSiyah);
        }

        private static Bitmap Uygula(Bitmap kaynak, Func<int, int, int, bool> yaziMi, out long imza, out bool tamamenSiyah)
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
                    bool yazi = yaziMi(r, g, bl);
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
