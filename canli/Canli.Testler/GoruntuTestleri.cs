using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using Canli.Goruntu;
using Xunit;
using Xunit.Abstractions;

namespace Canli.Testler
{
    public class GoruntuTestleri
    {
        private const string Yazi = "TORRISI MAFIOSO: Heard Spadaro's brought in men.";
        private readonly ITestOutputHelper _cikti;
        public GoruntuTestleri(ITestOutputHelper cikti) { _cikti = cikti; }

        // Test görüntüleri koddan üretilir (depoya oyun karesi girmez).
        private static Bitmap Gurultulu(int w, int h, Func<Random, Color> renk, bool beyazYamalar)
        {
            var rnd = new Random(7);
            var b = new Bitmap(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) b.SetPixel(x, y, renk(rnd));
            if (beyazYamalar)
                using (var g = Graphics.FromImage(b))
                    for (int i = 0; i < 5; i++)
                        g.FillEllipse(Brushes.White, rnd.Next(w - 60), rnd.Next(h - 20), 40, 12);
            return b;
        }

        private static Bitmap KumZemin(int w, int h) =>
            Gurultulu(w, h, r => Color.FromArgb(r.Next(200, 241), r.Next(170, 221), r.Next(120, 201)), true);

        private static void YaziCiz(Bitmap b, Color renk, string metin)
        {
            using (var g = Graphics.FromImage(b))
            using (var f = new Font("Arial", 30, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var br = new SolidBrush(renk))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(metin, f, br, 20, 25);
            }
        }

        private static Bitmap SariOrnek() { var b = KumZemin(1000, 90); YaziCiz(b, Color.FromArgb(240, 232, 70), Yazi); return b; }
        private static Bitmap BeyazOrnek()
        {
            var b = Gurultulu(1000, 90, r => Color.FromArgb(r.Next(10, 50), r.Next(10, 50), r.Next(10, 50)), false);
            YaziCiz(b, Color.White, Yazi); return b;
        }
        private static Bitmap BosOrnek() => KumZemin(1000, 90);

        private static double SiyahOran(Bitmap b)
        {
            long n = 0;
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++) if (b.GetPixel(x, y).R == 0) n++;
            return (double)n / (b.Width * b.Height);
        }

        [Fact]
        public void sari_suzgec_yaziyi_tutar_zemini_atar()
        {
            using (var k = SariOrnek())
            using (var t = RenkSuzgeci.Temizle(k, RenkKipi.Sari, 185, out _, out _))
                Assert.InRange(SiyahOran(t), 0.005, 0.15);
        }

        [Fact]
        public void beyaz_kip_sariyi_atar()
        {
            using (var k = SariOrnek())
            using (var t = RenkSuzgeci.Temizle(k, RenkKipi.Beyaz, 185, out _, out _))
            using (var bos = BosOrnek())
            using (var tb = RenkSuzgeci.Temizle(bos, RenkKipi.Beyaz, 185, out _, out _))
            {
                // Sarı yazı beyaz kipte tutulmaz: oran boş zeminin (yalnız beyaz yamalar) oranına yakın kalır.
                Assert.True(SiyahOran(t) < SiyahOran(tb) + 0.005);
            }
            using (var k = BeyazOrnek())
            using (var t = RenkSuzgeci.Temizle(k, RenkKipi.Beyaz, 185, out _, out _))
                Assert.InRange(SiyahOran(t), 0.005, 0.15);
        }

        [Fact]
        public void bos_zeminde_sari_kip_yazi_bulmaz()
        {
            using (var k = BosOrnek())
            using (var t = RenkSuzgeci.Temizle(k, RenkKipi.Sari, 185, out _, out _))
                Assert.True(SiyahOran(t) < 0.02); // gürültülü zeminde seyrek sahte sarı piksel olabilir
        }

        [Fact]
        public void imza_ayni_goruntude_esit()
        {
            using (var k = SariOrnek())
            {
                RenkSuzgeci.Temizle(k, RenkKipi.Ikisi, 185, out long a, out _).Dispose();
                RenkSuzgeci.Temizle(k, RenkKipi.Ikisi, 185, out long b, out _).Dispose();
                Assert.Equal(a, b);
                using (var bos = BosOrnek())
                {
                    RenkSuzgeci.Temizle(bos, RenkKipi.Ikisi, 185, out long c, out _).Dispose();
                    Assert.NotEqual(a, c);
                }
            }
        }

        [Fact]
        public void degisim_ayni_0_farkli_pozitif()
        {
            using (var a = SariOrnek())
            using (var b = BeyazOrnek())
            {
                var ia = Yakalayici.KucukImza(a);
                var ib = Yakalayici.KucukImza(b);
                Assert.Equal(64 * 36, ia.Length);
                Assert.Equal(0.0, Yakalayici.Degisim(ia, Yakalayici.KucukImza(a)));
                Assert.True(Yakalayici.Degisim(ia, ib) > 0);
            }
        }

        [Fact]
        public void tamamen_siyah_algilanir()
        {
            using (var s = new Bitmap(100, 40))
            {
                using (var g = Graphics.FromImage(s)) g.Clear(Color.FromArgb(5, 5, 5));
                RenkSuzgeci.Temizle(s, RenkKipi.Ikisi, 185, out _, out bool siyah).Dispose();
                Assert.True(siyah);
            }
            using (var k = SariOrnek())
            {
                RenkSuzgeci.Temizle(k, RenkKipi.Ikisi, 185, out _, out bool siyah).Dispose();
                Assert.False(siyah);
            }
        }

        [Fact]
        public void ocr_sari_satiri_okur()
        {
            var ocr = Okuyucu.Olustur(out _);
            if (ocr == null) { _cikti.WriteLine("en-US OCR yok, atlandi."); return; }
            using (var k = SariOrnek())
            using (var t = RenkSuzgeci.Temizle(k, RenkKipi.Sari, 185, out _, out _))
            {
                var satirlar = ocr.Oku(t);
                string hepsi = string.Join(" ", satirlar.Select(s => s.Metin)).ToUpperInvariant();
                _cikti.WriteLine(hepsi);
                Assert.True(hepsi.Contains("TORRISI") || hepsi.Contains("MAFIOSO"), hepsi);
                Assert.All(satirlar, s => Assert.True(s.Kutu.Width > 0));
            }
        }

        [Fact]
        public void ocr_dil_yoksa_hata_olayi()
        {
            var o = Okuyucu.Olustur(out string kod, () => new[] { "tr-TR", "de-DE" });
            Assert.Null(o);
            Assert.Equal("ocr_dil_yok", kod);
        }

        [Fact]
        public void ornek_klasoru_satirlari_okunur()
        {
            string klasor = Environment.GetEnvironmentVariable("CANLI_ORNEK_KLASORU");
            if (string.IsNullOrEmpty(klasor) || !Directory.Exists(klasor)) { _cikti.WriteLine("CANLI_ORNEK_KLASORU yok, atlandi."); return; }
            var ocr = Okuyucu.Olustur(out _);
            if (ocr == null) { _cikti.WriteLine("en-US OCR yok, atlandi."); return; }
            var dosyalar = Directory.GetFiles(klasor, "satir-*-ham.png");
            if (dosyalar.Length == 0) { _cikti.WriteLine("satir-*-ham.png yok, atlandi."); return; }
            int dolu = 0;
            foreach (var d in dosyalar)
                using (var b = new Bitmap(d))
                    if (ocr.Oku(b).Any(s => s.Metin.Length > 0)) dolu++;
            _cikti.WriteLine($"{dolu}/{dosyalar.Length} dosyada metin okundu");
            Assert.True(dolu * 2 >= dosyalar.Length);
        }

        [Fact]
        public void al_bos_dikdortgen()
        {
            Assert.Throws<ArgumentException>(() => Yakalayici.Al(new Rectangle(0, 0, 0, 10)));
            Assert.Throws<ArgumentException>(() => Yakalayici.Al(new Rectangle(0, 0, 10, -1)));
        }

        [Fact]
        public void oku_cok_buyuk_goruntu_olceklenir()
        {
            var ocr = Okuyucu.Olustur(out _);
            if (ocr == null) { _cikti.WriteLine("en-US OCR yok, atlandi."); return; }
            using (var b = new Bitmap(12000, 200))
            {
                using (var g = Graphics.FromImage(b)) g.Clear(Color.White);
                YaziCiz(b, Color.Black, Yazi);
                var s = ocr.Oku(b);
                Assert.NotNull(s);
                foreach (var l in s) Assert.True(l.Kutu.Right <= 12000 + 50);
            }
        }

        [Fact]
        public void temizle_ve_oku_dongusu_bellek_sizdirmaz()
        {
            var ocr = Okuyucu.Olustur(out _);
            if (ocr == null) { _cikti.WriteLine("en-US OCR yok, atlandi."); return; }
            using (var k = SariOrnek())
            {
                for (int i = 0; i < 20; i++) { using (var t = RenkSuzgeci.Temizle(k, RenkKipi.Sari, 185, out _, out _)) ocr.Oku(t); }
                long once = OzelBellek();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 500; i++) { using (var t = RenkSuzgeci.Temizle(k, RenkKipi.Sari, 185, out _, out _)) ocr.Oku(t); }
                long sonra = OzelBellek();
                _cikti.WriteLine($"{sw.ElapsedMilliseconds} ms, artis {(sonra - once) / 1048576} MB");
                Assert.True(sonra - once < 50L * 1048576, "özel bellek " + (sonra - once) / 1048576 + " MB arttı");
            }
        }

        private static long OzelBellek()
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            return System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;
        }
    }
}
