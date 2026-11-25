using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace Canli.Goruntu
{
    /// <summary>
    /// Altyazı rengini otomatik bulur (tasarım §7.2): süzgeçsiz OCR'ın verdiği yazı kutularında, kutu kenarının
    /// (zemin) renginden uzak, ince çizgilerde yoğunlaşan ve diğer renklerden belirgin biçimde baskın renk yazı rengidir.
    /// </summary>
    public static class RenkAlgilayici
    {
        private const int Kova = 16;                 // renk kovası genişliği (her kanal)
        private const double BaskinlikKati = 1.5;    // aday, ilk 12 kovanın ortancasının en az bu katı olmalı
        private const double EnAzKenarOrani = 0.4;   // pikselleri çizgi kenarında olmalı (düz zemin lekesi değil)
        private const int EnAzZeminUzakligi = 60;    // kutu kenarı (zemin) renginden uzaklık
        private const int KenarFarki = 60;           // komşu piksel bu kadar farklıysa "kenar"
        private const int EnAzParlaklik = 90;        // en parlak kanal bunun altındaysa gölge/çerçeve sayılır

        public static Color? YaziRengi(Bitmap b, IEnumerable<Rectangle> kutular)
        {
            int w = b.Width, h = b.Height;
            var px = new int[w * h];
            var kilit = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(kilit.Scan0, px, 0, px.Length); }
            finally { b.UnlockBits(kilit); }

            var sayi = new Dictionary<int, int>();
            var kenarSayi = new Dictionary<int, int>();
            var toplam = new Dictionary<int, long[]>();
            var zemin = new List<int>();
            int kutuPiksel = 0;
            foreach (var k0 in kutular)
            {
                var k = Rectangle.Intersect(k0, new Rectangle(0, 0, w, h));
                if (k.Width < 4 || k.Height < 4) continue;
                for (int y = k.Top; y < k.Bottom; y++)
                    for (int x = k.Left; x < k.Right; x++)
                    {
                        int p = px[y * w + x];
                        if (y == k.Top || y == k.Bottom - 1 || x == k.Left || x == k.Right - 1) zemin.Add(p);
                        int kv = KovaNo(p);
                        sayi.TryGetValue(kv, out int n); sayi[kv] = n + 1;
                        if (!toplam.TryGetValue(kv, out var t)) toplam[kv] = t = new long[3];
                        t[0] += (p >> 16) & 255; t[1] += (p >> 8) & 255; t[2] += p & 255;
                        if (KenardaMi(px, w, h, x, y, p)) { kenarSayi.TryGetValue(kv, out int e); kenarSayi[kv] = e + 1; }
                        kutuPiksel++;
                    }
            }
            if (kutuPiksel == 0 || zemin.Count == 0) return null;

            int zr = Ortanca(zemin.Select(p => (p >> 16) & 255)), zg = Ortanca(zemin.Select(p => (p >> 8) & 255)), zb = Ortanca(zemin.Select(p => p & 255));
            var sirali = sayi.OrderByDescending(k => k.Value).ToList();
            double ortanca = sirali.Take(12).Select(k => k.Value).OrderBy(v => v).ElementAt(Math.Min(12, sirali.Count) / 2);
            int enAz = Math.Max(60, kutuPiksel * 3 / 1000);

            foreach (var aday in sirali.Take(12))
            {
                if (aday.Value < enAz || aday.Value < BaskinlikKati * ortanca) continue;
                kenarSayi.TryGetValue(aday.Key, out int kenar);
                if ((double)kenar / aday.Value < EnAzKenarOrani) continue;
                var t = toplam[aday.Key];
                var renk = Color.FromArgb((int)(t[0] / aday.Value), (int)(t[1] / aday.Value), (int)(t[2] / aday.Value));
                if (Uzaklik(renk.R, renk.G, renk.B, zr, zg, zb) < EnAzZeminUzakligi) continue;
                // Yazının gölgesi/çerçevesi (koyu, ince, kenarlı) yazı rengi değildir: altyazı hiç bu kadar koyu olmaz.
                if (Math.Max(renk.R, Math.Max(renk.G, renk.B)) < EnAzParlaklik) continue;
                return renk;
            }
            return null;
        }

        private static int KovaNo(int p) => ((((p >> 16) & 255) / Kova) << 8) | ((((p >> 8) & 255) / Kova) << 4) | ((p & 255) / Kova);

        private static bool KenardaMi(int[] px, int w, int h, int x, int y, int p)
        {
            int r = (p >> 16) & 255, g = (p >> 8) & 255, b = p & 255;
            for (int d = 1; d <= 2; d++)
            {
                if (Fark(px, w, h, x + d, y, r, g, b) || Fark(px, w, h, x - d, y, r, g, b) ||
                    Fark(px, w, h, x, y + d, r, g, b) || Fark(px, w, h, x, y - d, r, g, b)) return true;
            }
            return false;
        }

        private static bool Fark(int[] px, int w, int h, int x, int y, int r, int g, int b)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            int q = px[y * w + x];
            return Uzaklik(r, g, b, (q >> 16) & 255, (q >> 8) & 255, q & 255) > KenarFarki;
        }

        internal static int Uzaklik(int r1, int g1, int b1, int r2, int g2, int b2) =>
            Math.Max(Math.Abs(r1 - r2), Math.Max(Math.Abs(g1 - g2), Math.Abs(b1 - b2)));

        private static int Ortanca(IEnumerable<int> s)
        {
            var d = s.OrderBy(v => v).ToList();
            return d[d.Count / 2];
        }
    }

    /// <summary>Algılanan renkler oylanır: aynı renk (±25) üç kez gelince karar verilir; tek seferlik farklı renk sayılmaz.</summary>
    public sealed class RenkOylamasi
    {
        private const int Pay = 25;
        private const int GerekenOy = 3;
        private readonly List<Color> _oylar = new List<Color>();

        public Color? Ekle(Color c)
        {
            _oylar.Add(c);
            if (_oylar.Count > 12) _oylar.RemoveAt(0);
            var yakin = _oylar.Where(o => RenkAlgilayici.Uzaklik(o.R, o.G, o.B, c.R, c.G, c.B) <= Pay).ToList();
            if (yakin.Count < GerekenOy) return null;
            return Color.FromArgb((int)yakin.Average(o => o.R), (int)yakin.Average(o => o.G), (int)yakin.Average(o => o.B));
        }
    }
}

namespace Canli.Goruntu
{
    /// <summary>
    /// Oturum boyunca altyazı rengini öğrenir: 2 sn'de bir süzgeçsiz okunan yazı kutularından renk algılanır,
    /// aynı renk 3 kez gelince öğrenilir. En çok 3 renk (ör. beyaz ipucu + turuncu konuşma); öğrenilen renk yeniden sayılmaz.
    /// </summary>
    public sealed class OtomatikRenk
    {
        public const double AralikMs = 2000;
        public const int EnCokRenk = 5;
        private const int AyniRenkPayi = 25;

        private readonly System.Func<double> _saat;
        private readonly System.Collections.Generic.List<System.Drawing.Color> _renkler;
        private RenkOylamasi _oylama = new RenkOylamasi();
        private double _son = double.NegativeInfinity;

        public OtomatikRenk(System.Func<double> saat, System.Collections.Generic.IEnumerable<System.Drawing.Color> bilinen = null)
        {
            _saat = saat;
            _renkler = bilinen == null ? new System.Collections.Generic.List<System.Drawing.Color>()
                : System.Linq.Enumerable.ToList(System.Linq.Enumerable.Take(bilinen, EnCokRenk));
        }

        public System.Collections.Generic.IReadOnlyList<System.Drawing.Color> Renkler { get { return _renkler; } }

        public bool OrneklemeZamani() { return _renkler.Count < EnCokRenk && _saat() - _son >= AralikMs; }

        /// <summary>Bir örnek işler; yeni bir renk öğrenildiyse true.</summary>
        public bool Ornekle(System.Drawing.Bitmap ham, System.Collections.Generic.IEnumerable<System.Drawing.Rectangle> kutular)
        {
            _son = _saat();
            if (_renkler.Count >= EnCokRenk) return false;
            var c = RenkAlgilayici.YaziRengi(ham, kutular);
            if (!c.HasValue) return false;
            foreach (var r in _renkler)
                if (RenkAlgilayici.Uzaklik(r.R, r.G, r.B, c.Value.R, c.Value.G, c.Value.B) <= AyniRenkPayi) return false;
            var karar = _oylama.Ekle(c.Value);
            if (!karar.HasValue) return false;
            _renkler.Add(karar.Value);
            _oylama = new RenkOylamasi();
            return true;
        }
    }
}
