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

        /// <summary>Yazı çizgisinin en büyük kalınlığı: en az 8 px, bölge yüksekliğinin %5'i.</summary>
        public static int CizgiKalinligi(int bolgeYuksekligi) => Math.Max(8, (int)Math.Round(bolgeYuksekligi * 0.05));

        /// <summary>
        /// Öğrenilen renklerden herhangi birine yakın pikseller yazı sayılır; ama yalnız ince çizgi üstündekiler:
        /// pikselin yatay ve dikey eşleşme dizilerinden kısası çizgi kalınlığını aşıyorsa (kar, gökyüzü gibi geniş leke)
        /// atılır. Böylece aynı karede açık gri konuşmacı adı tutulur, aynı renkteki kar atılır.
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
            // Eşleşen piksellerin yatay ve dikey dizi uzunlukları.
            var yatay = new int[n];
            var dikey = new int[n];
            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w;)
                {
                    if (renkNo[y * w + x] < 0) { x++; continue; }
                    int bas = x;
                    while (x < w && renkNo[y * w + x] >= 0) x++;
                    for (int j = bas; j < x; j++) yatay[y * w + j] = x - bas;
                }
            for (int x = 0; x < w; x++)
                for (int y = 0; y < hgt;)
                {
                    if (renkNo[y * w + x] < 0) { y++; continue; }
                    int bas = y;
                    while (y < hgt && renkNo[y * w + x] >= 0) y++;
                    for (int j = bas; j < y; j++) dikey[j * w + x] = y - bas;
                }
            int kalinlik = CizgiKalinligi(hgt);
            return Uygula(kaynak, px, i => renkNo[i] >= 0 && Math.Min(yatay[i], dikey[i]) <= kalinlik, out imza, out tamamenSiyah);
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
