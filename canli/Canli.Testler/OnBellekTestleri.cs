using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Canli.Mantik;
using Xunit;

namespace Canli.Testler
{
    public class OnBellekTestleri
    {
        private static OnBellek Yap(params (string en, string tr, bool menu)[] satirlar)
        {
            var b = new OnBellek();
            b.Yukle(satirlar);
            return b;
        }

        [Fact]
        public void tam_eslesme_buyuk_kucuk_bosluk_tirnak()
        {
            var b = Yap(("Federico's crew is late.", "Federico'nun adamları gecikti.", false));
            Assert.Equal("Federico'nun adamları gecikti.", b.AltyaziBul("federico’s   crew is LATE."));
            Assert.Equal(1, b.Sayi);
        }

        [Fact]
        public void ocr_hatasi_bulanik_bulur()
        {
            var b = Yap(("We have to go now, Enzo.", "Şimdi gitmeliyiz, Enzo.", false),
                        ("We have to go to the church.", "Kiliseye gitmeliyiz.", false));
            Assert.Equal("Şimdi gitmeliyiz, Enzo.", b.AltyaziBul("We have t0 go now, Enz0."));
            Assert.Equal("Kiliseye gitmeliyiz.", b.AltyaziBul("We have to go to the chruch."));
        }

        [Fact]
        public void kisa_satir_yanlis_eslesmez()
        {
            var b = Yap(("Yes.", "Evet.", false), ("Yes, sir. Right away, I will bring the car.", "Evet efendim. Hemen arabayı getiriyorum.", false));
            Assert.Null(b.AltyaziBul("No."));
            Assert.Null(b.AltyaziBul("Yes, sir."));
            Assert.Equal("Evet.", b.AltyaziBul("Yes."));
        }

        [Fact]
        public void sayi_ya_da_olumsuzluk_farki_bulanik_eslesmez()
        {
            var b = Yap(("You owe me 500 dollars.", "Bana 500 dolar borçlusun.", false),
                        ("Chapter 1", "Bölüm 1", true),
                        ("I can't do that.", "Bunu yapamam.", false));
            Assert.Null(b.AltyaziBul("You owe me 300 dollars."));
            Assert.Null(b.MenuBul("Chapter 2"));
            Assert.Null(b.AltyaziBul("I can do that."));
            // OCR harf hatası (sayı ve olumsuzluk aynı) yine bulunur.
            Assert.Equal("Bana 500 dolar borçlusun.", b.AltyaziBul("You owe me 500 dollers."));
            Assert.Equal("Bunu yapamam.", b.AltyaziBul("I can't d0 that."));
        }

        [Fact]
        public void konusmaci_oneki()
        {
            var b = Yap(("Where is the money?", "Para nerede?", false));
            Assert.Equal("ENZO: Para nerede?", b.AltyaziBul("ENZO: Where is the money?"));
            Assert.Null(b.AltyaziBul("This is a very long prefix that is not a name: Where is the money?"));
        }

        [Fact]
        public void menu_once_harf_duyarli_sonra_buyuk_harfe_dayanikli()
        {
            var b = Yap(("Options", "Seçenekler", true), ("Continue", "Devam et", true));
            Assert.Equal("Seçenekler", b.MenuBul("Options"));
            Assert.Equal("Seçenekler", b.MenuBul("OPTIONS"));
            Assert.Equal("Devam et", b.MenuBul("Contlnue"));
            Assert.Null(b.AltyaziBul("Options"));
            Assert.Null(b.MenuBul("Load"));
        }

        [Fact]
        public void yeniden_yukle_eskiyi_degistirir()
        {
            var b = Yap(("Hello.", "Merhaba.", false));
            b.Yukle(new[] { ("Bye.", "Hoşça kal.", false) });
            Assert.Null(b.AltyaziBul("Hello."));
            Assert.Equal("Hoşça kal.", b.AltyaziBul("Bye."));
        }

        [Fact]
        public void otuz_bin_satirda_hizli()
        {
            var rnd = new Random(7);
            string[] kelimeler = { "the", "family", "money", "church", "car", "gun", "don", "we", "have", "to", "go", "now", "Enzo", "Luca", "Sicily", "boat", "wine", "never", "tell", "anyone" };
            var satirlar = new List<(string, string, bool)>();
            for (int i = 0; i < 30000; i++)
            {
                int n = 4 + rnd.Next(10);
                string s = string.Join(" ", Enumerable.Range(0, n).Select(_ => kelimeler[rnd.Next(kelimeler.Length)])) + " " + i + ".";
                satirlar.Add((s, "tr" + i, false));
            }
            var b = new OnBellek();
            b.Yukle(satirlar);
            var hedef = satirlar[12345].Item1;
            string bozuk = hedef.Replace('e', 'c');
            b.AltyaziBul(bozuk); // ısınma
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) b.AltyaziBul(bozuk + (i % 2 == 0 ? "" : " "));
            sw.Stop();
            Assert.True(sw.Elapsed.TotalMilliseconds / 20 < 5, "ortalama " + sw.Elapsed.TotalMilliseconds / 20 + " ms");
        }

        [Fact]
        public void disk_onbellegi_yaz_oku_ve_bozuk_yok_sayilir()
        {
            string klasor = Path.Combine(Path.GetTempPath(), "canli-ob-" + Guid.NewGuid().ToString("N"));
            try
            {
                var d = new OnBellekDiski(klasor);
                Assert.Null(d.Oku("steam:1"));
                d.Yaz("steam:1", "3-99", new List<(string, string, bool)> { ("Hi.", "Selam.", false), ("Options", "Seçenekler", true) });
                var o = d.Oku("steam:1");
                Assert.Equal("3-99", o.Value.surum);
                Assert.Equal(2, o.Value.satirlar.Count);
                Assert.Contains(("Options", "Seçenekler", true), o.Value.satirlar);
                Assert.Null(d.Oku("steam:2"));
                File.WriteAllText(Directory.GetFiles(klasor)[0], "{bozuk");
                Assert.Null(d.Oku("steam:1"));
            }
            finally { try { Directory.Delete(klasor, true); } catch (Exception) { } }
        }
    }
}
