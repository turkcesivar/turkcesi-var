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

        /// <summary>Bir rengin eşleşen piksellerinin en az bu kadarı çizgi kenarında olmalı (yazı ince; kar/gökyüzü geniş leke).</summary>
        public const double EnAzKenarOrani = 0.3;

        /// <summary>
        /// Öğrenilen renklerden herhangi birine yakın pikseller yazı sayılır. Her renk karede ayrı denetlenir:
        /// eşleşen pikselleri geniş lekeler oluşturuyorsa (ör. öğrenilmiş açık gri = karlı zemin) o renk bu karede atlanır.
        /// </summary>
        public static Bitmap TemizleRenkler(Bitmap kaynak, System.Collections.Generic.IReadOnlyList<Color> hedefler, out long imza, out bool tamamenSiyah)
        {
            int w = kaynak.Width, hgt = kaynak.Height, n = w * hgt;
            var px = new int[n];
            var a = kaynak.LockBits(new Rectangle(0, 0, w, hgt), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(a.Scan0, px, 0, n); }
            finally { kaynak.UnlockBits(a); }

            // Piksel başına eşleşen ilk renk (-1: hiçbiri).
            var renkNo = new sbyte[n];
            for (int i = 0; i < n; i++)
            {
                int p = px[i], r = (p >> 16) & 255, g = (p >> 8) & 255, bl = p & 255;
                renkNo[i] = -1;
                for (int k = 0; k < hedefler.Count; k++)
                {
                    var c = hedefler[k];
                    if (Math.Abs(r - c.R) <= OzelRenkPayi && Math.Abs(g - c.G) <= OzelRenkPayi && Math.Abs(bl - c.B) <= OzelRenkPayi)
                    { renkNo[i] = (sbyte)k; break; }
                }
            }
            var sayi = new int[hedefler.Count];
            var kenar = new int[hedefler.Count];
            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x, k = renkNo[i];
                    if (k < 0) continue;
                    sayi[k]++;
                    bool icte = x > 0 && x < w - 1 && y > 0 && y < hgt - 1
                        && renkNo[i - 1] == k && renkNo[i + 1] == k && renkNo[i - w] == k && renkNo[i + w] == k;
                    if (!icte) kenar[k]++;
                }
            var gecerli = new bool[hedefler.Count];
            for (int k = 0; k < hedefler.Count; k++) gecerli[k] = sayi[k] > 0 && (double)kenar[k] / sayi[k] >= EnAzKenarOrani;
            return Uygula(kaynak, px, i => renkNo[i] >= 0 && gecerli[renkNo[i]], out imza, out tamamenSiyah);
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

        /// <summary>Pikseller önceden okunmuşsa: yazı kararı piksel sırasına göre verilir.</summary>
        private static Bitmap Uygula(Bitmap kaynak, int[] okunan, Func<int, bool> yaziMi, out long imza, out bool tamamenSiyah)
        {
            int w = kaynak.Width, h = kaynak.Height, n = w * h;
            var cikis = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            try
            {
                var px = new int[n];
                long hs = 1469598103934665603L;
                bool siyah = true;
                for (int i = 0; i < n; i++)
                {
                    int p = okunan[i];
                    if (((p >> 16) & 255) > 8 || ((p >> 8) & 255) > 8 || (p & 255) > 8) siyah = false;
                    bool yazi = yaziMi(i);
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
