using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using Canli.Goruntu;
using Xunit;
using Xunit.Abstractions;

namespace Canli.Testler
{
    public class RenkAlgilayiciTestleri
    {
        private readonly ITestOutputHelper _cikti;
        public RenkAlgilayiciTestleri(ITestOutputHelper cikti) { _cikti = cikti; }

        private const string Yazi = "ANNA: Don't you think I'd also love to live on an ocean beach?";

        private static Bitmap Zemin(int w, int h, Func<Random, Color> renk)
        {
            var rnd = new Random(11);
            var b = new Bitmap(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) b.SetPixel(x, y, renk(rnd));
            return b;
        }

        private static Bitmap Ornek(Func<Random, Color> zemin, Color yazi)
        {
            var b = Zemin(1100, 90, zemin);
            using (var g = Graphics.FromImage(b))
            using (var f = new Font("Arial", 30, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var br = new SolidBrush(yazi))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(Yazi, f, br, 20, 25);
            }
            return b;
        }

        private static Color Kar(Random r) { int v = r.Next(90, 236); return Color.FromArgb(v, v - r.Next(0, 12), v - r.Next(0, 20)); }
        private static Color Kum(Random r) => Color.FromArgb(r.Next(200, 241), r.Next(170, 221), r.Next(120, 201));
        private static Color Koyu(Random r) => Color.FromArgb(r.Next(10, 50), r.Next(10, 50), r.Next(10, 50));

        // Kelime kutuları sınamada OCR'sız verilir: yazının çizildiği bant.
        private static Rectangle[] Kutular() => new[] { new Rectangle(15, 22, 1060, 45) };

        private static int Uzaklik(Color a, Color b) =>
            Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

        [Theory]
        [InlineData("kar_turuncu")]
        [InlineData("kum_sari")]
        [InlineData("koyu_beyaz")]
        public void yazi_rengini_bulur(string durum)
        {
            Func<Random, Color> zemin = durum == "kar_turuncu" ? Kar : durum == "kum_sari" ? (Func<Random, Color>)Kum : Koyu;
            Color yazi = durum == "kar_turuncu" ? Color.FromArgb(202, 66, 0) : durum == "kum_sari" ? Color.FromArgb(240, 232, 70) : Color.White;
            using (var b = Ornek(zemin, yazi))
            {
                Color? bulunan = RenkAlgilayici.YaziRengi(b, Kutular());
                _cikti.WriteLine(durum + " → " + bulunan);
                Assert.True(bulunan.HasValue);
                Assert.True(Uzaklik(bulunan.Value, yazi) <= 40, durum + " " + bulunan.Value);
            }
        }

        /// <summary>Yalnız yerelde: CANLI_ALGI_ORNEGI = gerçek oyun ekran görüntüsü (telif: depoya girmez).</summary>
        [Fact]
        public void gercek_ekran_goruntusunde_rengi_bulur_ve_okur()
        {
            string yol = Environment.GetEnvironmentVariable("CANLI_ALGI_ORNEGI");
            if (string.IsNullOrEmpty(yol) || !System.IO.File.Exists(yol)) { _cikti.WriteLine("CANLI_ALGI_ORNEGI yok, atlandi."); return; }
            var ocr = Okuyucu.Olustur(out _);
            if (ocr == null) { _cikti.WriteLine("en-US OCR yok, atlandi."); return; }
            using (var tam = new Bitmap(yol))
            using (var bolge = tam.Clone(new Rectangle((int)(tam.Width * 0.12), (int)(tam.Height * 0.78), (int)(tam.Width * 0.76), (int)(tam.Height * 0.17)), tam.PixelFormat))
            {
                var ham = ocr.Oku(bolge);
                _cikti.WriteLine("ham OCR: " + string.Join(" | ", ham.Select(s => s.Metin)));
                Color? renk = RenkAlgilayici.YaziRengi(bolge, ham.Select(s => s.Kutu));
                _cikti.WriteLine("renk: " + renk);
                Assert.True(renk.HasValue);
                using (var t = RenkSuzgeci.TemizleRenk(bolge, renk.Value, out _, out _))
                {
                    string metin = string.Join(" ", ocr.Oku(t).Select(s => s.Metin));
                    _cikti.WriteLine("süzülmüş OCR: " + metin);
                    int kelime = metin.Split(' ').Count(k => k.Count(char.IsLetter) >= 3);
                    Assert.True(kelime >= 4, metin);
                }
            }
        }

        [Fact]
        public void yazisiz_zeminde_renk_bulmaz()
        {
            using (var b = Zemin(1100, 90, Kar))
                Assert.Null(RenkAlgilayici.YaziRengi(b, Kutular()));
        }

        [Fact]
        public void ozel_renk_suzgeci_karli_zeminde_yalniz_yaziyi_tutar()
        {
            using (var b = Ornek(Kar, Color.FromArgb(202, 66, 0)))
            using (var t = RenkSuzgeci.TemizleRenk(b, Color.FromArgb(202, 66, 0), out _, out _))
            {
                long n = 0;
                for (int y = 0; y < t.Height; y++) for (int x = 0; x < t.Width; x++) if (t.GetPixel(x, y).R == 0) n++;
                double oran = (double)n / (t.Width * t.Height);
                Assert.InRange(oran, 0.005, 0.15);
            }
        }

        [Fact]
        public void otomatik_renk_iki_saniyede_bir_ornekler_ve_uc_oyla_ogrenir()
        {
            double t = 0;
            var o = new OtomatikRenk(() => t);
            using (var b = Ornek(Kar, Color.FromArgb(202, 66, 0)))
            {
                Assert.True(o.OrneklemeZamani());
                Assert.False(o.Ornekle(b, Kutular()));          // 1. oy
                Assert.False(o.OrneklemeZamani());              // 2 sn dolmadı
                t = 2100; Assert.False(o.Ornekle(b, Kutular())); // 2. oy
                t = 4200; Assert.True(o.Ornekle(b, Kutular()));  // 3. oy → öğrenildi
                Assert.Single(o.Renkler);
                t = 6300; Assert.False(o.Ornekle(b, Kutular())); // aynı renk yeniden eklenmez
                Assert.Single(o.Renkler);
            }
        }

        [Fact]
        public void otomatik_renk_birden_cok_renk_ogrenir_ve_disari_verir()
        {
            double t = 0;
            var o = new OtomatikRenk(() => t);
            Color[] renkler = { Color.FromArgb(202, 66, 0), Color.FromArgb(240, 232, 70), Color.White, Color.FromArgb(60, 200, 60) };
            Func<Random, Color>[] zeminler = { Kar, Kum, Koyu, Koyu };
            for (int r = 0; r < renkler.Length; r++)
                using (var b = Ornek(zeminler[r], renkler[r]))
                    for (int i = 0; i < 3; i++) { t += 2100; o.Ornekle(b, Kutular()); }
            Assert.Equal(Math.Min(renkler.Length, OtomatikRenk.EnCokRenk), o.Renkler.Count);
            var yeni = new OtomatikRenk(() => 0, o.Renkler);
            Assert.Equal(o.Renkler.Count, yeni.Renkler.Count);
        }

        [Fact]
        public void birden_cok_renkle_suzme_hepsini_tutar()
        {
            using (var b = Ornek(Koyu, Color.FromArgb(202, 66, 0)))
            {
                long Say(Bitmap t) { long n = 0; for (int y = 0; y < t.Height; y++) for (int x = 0; x < t.Width; x++) if (t.GetPixel(x, y).R == 0) n++; return n; }
                using (var yalnizBeyaz = RenkSuzgeci.TemizleRenkler(b, new[] { Color.White }, out _, out _))
                using (var ikisi = RenkSuzgeci.TemizleRenkler(b, new[] { Color.White, Color.FromArgb(202, 66, 0) }, out _, out _))
                {
                    Assert.True(Say(yalnizBeyaz) < 10);
                    Assert.True(Say(ikisi) > 1000);
                }
            }
        }

        [Fact]
        public void zemin_lekesi_olan_ogrenilmis_renk_o_karede_atlanir()
        {
            // Metro: öğrenilmiş açık gri (ipucu yazısı) karlı zeminle aynı renk; zemin geniş gri lekelerden oluşur.
            var gri = Color.FromArgb(199, 199, 199);
            var turuncu = Color.FromArgb(255, 151, 0);
            using (var b = new Bitmap(1100, 90))
            {
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.FromArgb(60, 60, 70));
                    using (var br = new SolidBrush(Color.FromArgb(205, 203, 200))) g.FillRectangle(br, 300, 0, 500, 90);  // kar lekesi
                    using (var f = new Font("Arial", 30, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var yb = new SolidBrush(turuncu))
                    {
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        g.DrawString(Yazi, f, yb, 20, 25);
                    }
                }
                long Say(Bitmap t, int x0, int x1) { long n = 0; for (int y = 0; y < t.Height; y++) for (int x = x0; x < x1; x++) if (t.GetPixel(x, y).R == 0) n++; return n; }
                using (var t = RenkSuzgeci.TemizleRenkler(b, new[] { gri, turuncu }, out _, out _))
                {
                    // Kar lekesinin yazı olmayan üst şeridi (y 0-20) tamamen boş kalmalı; turuncu yazı tutulmalı.
                    long ust = 0; for (int y = 0; y < 20; y++) for (int x = 300; x < 800; x++) if (t.GetPixel(x, y).R == 0) ust++;
                    Assert.Equal(0, ust);
                    Assert.True(Say(t, 0, 1100) > 1000);
                }
            }
        }

        [Fact]
        public void oylama_ayni_renk_uc_kez_gelince_karar_verir()
        {
            var o = new RenkOylamasi();
            Assert.Null(o.Ekle(Color.FromArgb(202, 66, 0)));
            Assert.Null(o.Ekle(Color.FromArgb(240, 232, 70)));   // tek seferlik farklı renk sayılmaz
            Assert.Null(o.Ekle(Color.FromArgb(198, 70, 4)));
            Color? karar = o.Ekle(Color.FromArgb(205, 63, 0));
            Assert.True(karar.HasValue);
            Assert.True(Uzaklik(karar.Value, Color.FromArgb(202, 66, 0)) <= 10);
        }
    }
}
