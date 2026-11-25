using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Canli.Goruntu;
using Canli.Kaplama;
using Canli.Mantik;
using Xunit;

namespace Canli.Testler
{
    public class MenuTestleri
    {
        private double _t;

        // ---- Menü algılayıcı ----

        [Fact]
        public void menu_sabitlik_450ms()
        {
            var m = new MenuAlgilayici(() => _t);
            Assert.Equal(MenuAlgilayici.Karar.Yok, m.Imza(1.0));     // ilk kare: büyük değişim, sabitlik başlar
            _t = 200; Assert.Equal(MenuAlgilayici.Karar.Yok, m.Imza(0.0));
            _t = 449; Assert.Equal(MenuAlgilayici.Karar.Yok, m.Imza(0.001));
            _t = 451; Assert.Equal(MenuAlgilayici.Karar.Oku, m.Imza(0.0));
            _t = 900; Assert.Equal(MenuAlgilayici.Karar.Yok, m.Imza(0.0));   // aynı sabitlikte bir kez
        }

        [Fact]
        public void menu_buyuk_degisimde_kaldir()
        {
            var m = new MenuAlgilayici(() => _t);
            m.Imza(1.0);
            _t = 500; Assert.Equal(MenuAlgilayici.Karar.Oku, m.Imza(0.0));
            m.MenuAcik = true;
            _t = 600; Assert.Equal(MenuAlgilayici.Karar.Kaldir, m.Imza(0.03));
            Assert.False(m.MenuAcik);
            // Menü kapalıyken büyük değişim yalnız sabitliği sıfırlar.
            _t = 700; Assert.Equal(MenuAlgilayici.Karar.Yok, m.Imza(0.5));
        }

        [Fact]
        public void menu_kucuk_degisimde_yeniden_oku()
        {
            var m = new MenuAlgilayici(() => _t);
            m.Imza(1.0);
            _t = 500; Assert.Equal(MenuAlgilayici.Karar.Oku, m.Imza(0.0));
            m.MenuAcik = true;
            _t = 600; Assert.Equal(MenuAlgilayici.Karar.Yok, m.Imza(0.01));   // küçük değişim: kutular kalır
            Assert.True(m.MenuAcik);
            _t = 1000; Assert.Equal(MenuAlgilayici.Karar.Yok, m.Imza(0.0));
            _t = 1051; Assert.Equal(MenuAlgilayici.Karar.Oku, m.Imza(0.0));  // yeniden 450 ms sabit → yeniden oku
        }

        [Fact]
        public void otomatik_menu_5_satir()
        {
            var m = new MenuAlgilayici(() => _t);
            Assert.False(m.YeterliSatir(4));
            Assert.True(m.YeterliSatir(5));
            Assert.True(m.YeterliSatir(12));
        }

        // ---- Ön plan bekçisi ----

        [Fact]
        public void on_plan_surec_adi_eslesir()
        {
            var b = new OnPlanBekcisi("Mafia.exe", () => ("mafia", new IntPtr(42)));
            Assert.True(b.OyunOnde(out IntPtr p));
            Assert.Equal(new IntPtr(42), p);
            Assert.True(new OnPlanBekcisi("mafia", () => ("MAFIA.EXE", new IntPtr(1))).OyunOnde(out _));
            Assert.False(new OnPlanBekcisi("Mafia.exe", () => ("explorer", new IntPtr(7))).OyunOnde(out _));
            Assert.False(new OnPlanBekcisi("Mafia.exe", () => (null, IntPtr.Zero)).OyunOnde(out _));
        }

        [Fact]
        public void on_plan_surec_bos_hep_true()
        {
            var b = new OnPlanBekcisi("", () => ("explorer", new IntPtr(7)));
            Assert.True(b.OyunOnde(out _));
            Assert.True(new OnPlanBekcisi(null, () => (null, IntPtr.Zero)).OyunOnde(out _));
        }

        // ---- Çalıştırıcı (sahte yakalama/OCR/kaplama/zamanlayıcı) ----

        [Fact]
        public async Task on_plan_degilken_okuma_yok()
        {
            var s = new Sahneler("{\"tr\":\"Gidelim.\",\"kalan\":5}");
            var c = s.Calistirici(new OnPlanBekcisi("Mafia.exe", () => ("explorer", new IntPtr(7))));
            await c.Basla();
            Assert.True(s.Zaman.Basladi);
            s.Ekran.Satirlar = new[] { "We have to go now, Enzo." };

            c.YakalamaTik();
            c.EkBolgeTik();
            c.YoklamaTik();

            Assert.Equal(0, s.Ekran.AlSayisi);
            Assert.Equal(0, s.Ekran.ImzaSayisi);
            Assert.Equal(0, s.Ekran.OkuSayisi);
            Assert.True(s.Kaplama.GizleSayisi > 0);

            // Karşı sınama: oyun öndeyse okunur.
            var s2 = new Sahneler("{\"tr\":\"Gidelim.\",\"kalan\":5}");
            var c2 = s2.Calistirici(new OnPlanBekcisi("Mafia.exe", () => ("Mafia", new IntPtr(7))));
            await c2.Basla();
            s2.Ekran.Satirlar = new[] { "We have to go now, Enzo." };
            c2.YakalamaTik();
            Assert.True(s2.Ekran.AlSayisi > 0);
            Assert.True(s2.Ekran.OkuSayisi > 0);
        }

        [Fact]
        public async Task ek_bolge_odak_kaybinda_okumaz()
        {
            bool onde = true;
            var s = new Sahneler("{\"tr\":\"x\"}", oyunGovdesi: "{\"durum\":\"acik\",\"profil\":{\"ekBolgeler\":[[0,0,0.3,0.1]]}}");
            var c = s.Calistirici(new OnPlanBekcisi("Mafia.exe", () => (onde ? "Mafia" : "explorer", new IntPtr(7))));
            await c.Basla();
            c.YakalamaTik();                 // oyun önde: ekran bilinir
            c.EkBolgeTik();
            int al = s.Ekran.AlSayisi;
            Assert.True(al > 0);             // karşı sınama: öndeyken ek bölge okunur

            onde = false;                    // odak başka uygulamaya geçti; yakalama tıkı henüz çalışmadı
            c.EkBolgeTik();
            Assert.Equal(al, s.Ekran.AlSayisi);
            Assert.True(s.Kaplama.GizleSayisi > 0);
        }

        [Fact]
        public async Task menu_odak_kaybinda_okumaz()
        {
            bool onde = true;
            var s = new Sahneler("{\"tr\":\"x\"}");
            // F7 hep basılı görünür: ilk basış kenarı oyun arkadayken gelir.
            var c = s.Calistirici(new OnPlanBekcisi("Mafia.exe", () => (onde ? "Mafia" : "explorer", new IntPtr(7))), _ => !onde);
            await c.Basla();
            onde = false;
            c.YakalamaTik();
            Assert.Equal(0, s.Ekran.AlSayisi);
            Assert.Equal(0, s.Ekran.ImzaSayisi);
        }

        [Fact]
        public async Task lisans_bitince_durur()
        {
            var s = new Sahneler(null, ceviriDurumu: HttpStatusCode.Forbidden, ceviriGovdesi: "{\"hata\":\"suresi_bitti\"}");
            var c = s.Calistirici(new OnPlanBekcisi(""));
            await c.Basla();
            Assert.Contains("{\"olay\":\"basladi\"}", s.Cikis.Satirlar());

            s.Ekran.Satirlar = new[] { "We have to go now, Enzo." };
            c.YakalamaTik();

            Assert.True(SpinWait.SpinUntil(() => s.Cikis.Satirlar().Any(l => l.Contains("\"durdu\"")), 5000));
            var satirlar = s.Cikis.Satirlar();
            Assert.Contains("{\"olay\":\"hata\",\"kod\":\"suresi_bitti\"}", satirlar);
            Assert.Contains("{\"olay\":\"durdu\",\"neden\":\"lisans\"}", satirlar);
            Assert.True(s.Kaplama.GizleSayisi > 0);
            Assert.True(s.Zaman.Durduruldu);
            Assert.True(c.Durdu);

            // Durduktan sonra zamanlayıcı gövdeleri hiçbir şey yapmaz.
            int al = s.Ekran.AlSayisi;
            c.YakalamaTik();
            Assert.Equal(al, s.Ekran.AlSayisi);
        }

        [Fact]
        public async Task paketli_oyunda_baslamaz()
        {
            var s = new Sahneler("{\"tr\":\"x\"}", oyunGovdesi: "{\"durum\":\"paketli\"}");
            var c = s.Calistirici(new OnPlanBekcisi(""));
            await c.Basla();
            var satirlar = s.Cikis.Satirlar();
            Assert.Contains("{\"olay\":\"hata\",\"kod\":\"paketli\"}", satirlar);
            Assert.Contains("{\"olay\":\"durdu\",\"neden\":\"lisans\"}", satirlar);
            Assert.DoesNotContain("{\"olay\":\"basladi\"}", satirlar);
            Assert.False(s.Zaman.Basladi);
        }

        [Fact]
        public async Task gecersiz_lisansta_baslamaz()
        {
            var s = new Sahneler("{\"tr\":\"x\"}", durumDurumu: HttpStatusCode.Unauthorized, durumGovdesi: "{\"hata\":\"lisans_yok\"}");
            var c = s.Calistirici(new OnPlanBekcisi(""));
            await c.Basla();
            var satirlar = s.Cikis.Satirlar();
            Assert.Contains("{\"olay\":\"hata\",\"kod\":\"lisans_yok\"}", satirlar);
            Assert.Contains("{\"olay\":\"durdu\",\"neden\":\"lisans\"}", satirlar);
            Assert.DoesNotContain("{\"olay\":\"basladi\"}", satirlar);
            Assert.False(s.Zaman.Basladi);
        }

        [Fact]
        public async Task acilista_kalan_bildirilir()
        {
            var s = new Sahneler("{\"tr\":\"x\"}", durumGovdesi: "{\"durum\":\"etkin\",\"kalan\":1234}");
            var c = s.Calistirici(new OnPlanBekcisi(""));
            await c.Basla();
            c.YoklamaTik();
            Assert.Contains("{\"olay\":\"kalan\",\"kalan\":1234}", s.Cikis.Satirlar());
        }

        [Fact]
        public void komut_profili_sunucuyu_ezer()
        {
            var sunucu = new Profil { AltyaziBolgesi = new[] { 0.1, 0.7, 0.8, 0.2 }, Renk = "sari", EkBolgeler = new[] { new[] { 0.0, 0.0, 0.3, 0.1 } } };

            var verilen = new Komut { Profil = new Profil { AltyaziBolgesi = new[] { 0.2, 0.8, 0.6, 0.1 }, Renk = "beyaz", EkBolgeler = new double[0][] }, ProfilVerildi = true };
            Assert.Equal("beyaz", Calistirici.ProfilSec(verilen, sunucu).Renk);

            var bos = new Komut { Profil = Profil.Varsayilan };
            var secilen = Calistirici.ProfilSec(bos, sunucu);
            Assert.Equal("sari", secilen.Renk);
            Assert.Equal(new[] { 0.1, 0.7, 0.8, 0.2 }, secilen.AltyaziBolgesi);
            Assert.Single(secilen.EkBolgeler);

            Assert.Equal("ikisi", Calistirici.ProfilSec(bos, null).Renk);
        }

        [Fact]
        public void menu_satiri_ekran_kenarinda_kaydirilir()
        {
            // 1920 genişlikte x=1500'deki kutu: sağ kenara 408 px kalır.
            Assert.Equal(408f, MenuYerlesimi.SatirSiniri(1500, 300, 1920));
            // Kenara dayanmış kutu kendi genişliğinin altına inmez.
            Assert.Equal(300f, MenuYerlesimi.SatirSiniri(1800, 300, 1920));
        }

        [Fact]
        public void gecersiz_bolge_varsayilana_doner()
        {
            Assert.True(Profil.BolgeGecerli(new[] { 0.12, 0.78, 0.76, 0.17 }));
            Assert.False(Profil.BolgeGecerli(new[] { 0.1, 0.8, 0.0, 0.1 }));      // genişlik 0
            Assert.False(Profil.BolgeGecerli(new[] { 0.5, 0.9, 0.6, 0.2 }));      // ekran dışı
            Assert.False(Profil.BolgeGecerli(new[] { -0.1, 0.0, 0.3, 0.1 }));
            Assert.False(Profil.BolgeGecerli(null));

            var komut = new Komut
            {
                ProfilVerildi = true,
                Profil = new Profil { AltyaziBolgesi = new[] { 0.1, 0.8, 0.0, 0.1 }, Renk = "sari",
                                      EkBolgeler = new[] { new[] { 0.0, 0.0, 0.3, 0.1 }, new[] { 0.9, 0.9, 0.5, 0.5 } } }
            };
            var p = Calistirici.ProfilSec(komut, null);
            Assert.Equal(Profil.Varsayilan.AltyaziBolgesi, p.AltyaziBolgesi);
            Assert.Equal("sari", p.Renk);
            Assert.Single(p.EkBolgeler);
        }

        [Fact]
        public async Task onbellek_isabeti_sunucuya_gitmez()
        {
            var s = new Sahneler("{\"tr\":\"x\"}",
                bellekGovdesi: "{\"surum\":\"1-1\",\"satirlar\":[[\"We have to go now, Enzo.\",\"Şimdi gitmeliyiz, Enzo.\",\"a\"]]}");
            var tani = new SahteTani();
            var c = s.Calistirici(new OnPlanBekcisi(""), tani: tani);
            await c.Basla();
            Assert.True(SpinWait.SpinUntil(() => c.OnBellekSayisi == 1, 5000));
            Assert.Contains(tani.Satirlar(), l => l.Contains("\"on_bellek\"") && l.Contains("\"kaynak\":\"sunucu\""));

            s.Ekran.Satirlar = new[] { "We have to go now, Enz0." };
            c.YakalamaTik();
            c.YoklamaTik();
            Assert.Equal("Şimdi gitmeliyiz, Enzo.", s.Kaplama.Ana);
            Assert.Equal(0, s.CeviriIstegi);
            Assert.Contains(tani.Satirlar(), l => l.Contains("\"altyazi\"") && l.Contains("\"kaynak\":\"onbellek\""));
        }

        // ---- Tanı kaydı ----

        [Fact]
        public async Task tani_altyazi_satiri_kaynak_ve_surelerle_yazilir()
        {
            var s = new Sahneler("{\"tr\":\"Gitmeliyiz, Enzo.\",\"kaynak\":\"model\",\"okunamaz\":false,\"kalan\":9}");
            var tani = new SahteTani();
            var c = s.Calistirici(new OnPlanBekcisi(""), tani: tani);
            await c.Basla();
            Assert.Contains(tani.Satirlar(), l => l.StartsWith("{\"olay\":\"basladi\",\"oyun\":\"test:x\""));

            s.Ekran.Satirlar = new[] { "We have to go now, Enzo." };
            c.YakalamaTik();

            Assert.True(SpinWait.SpinUntil(() => tani.Satirlar().Any(l => l.Contains("\"altyazi\"")), 5000));
            string satir = tani.Satirlar().First(l => l.Contains("\"altyazi\""));
            Assert.Contains("\"en\":\"We have to go now, Enzo.\"", satir);
            Assert.Contains("\"kaynak\":\"model\"", satir);
            Assert.Contains("\"hata\":null", satir);
            Assert.DoesNotContain("\"istekMs\":null", satir);
            Assert.Contains("\"toplamMs\":", satir);
            // Tanı çıktısı protokol kanalına karışmaz.
            Assert.DoesNotContain(s.Cikis.Satirlar(), l => l.Contains("altyazi"));
        }

        [Fact]
        public async Task tani_f7_menu_okuma_ve_ceviri_yazilir()
        {
            var s = new Sahneler("{\"tr\":\"x\"}");
            var tani = new SahteTani();
            var c = s.Calistirici(new OnPlanBekcisi(""), tus: k => k == Calistirici.VkF7, tani: tani);
            await c.Basla();
            s.Ekran.Satirlar = new[] { "New Game", "Continue", "Options" };
            c.YakalamaTik();

            Assert.True(SpinWait.SpinUntil(() => tani.Satirlar().Any(l => l.Contains("\"menu_ceviri\"")), 5000));
            var satirlar = tani.Satirlar();
            Assert.Contains(satirlar, l => l.Contains("\"menu_okundu\"") && l.Contains("\"elle\":true") && l.Contains("\"satir\":3"));
            Assert.Contains(satirlar, l => l.Contains("\"menu_ceviri\"") && l.Contains("\"tur\":\"menu\"") && l.Contains("\"oge\":3"));
        }

        [Fact]
        public void dosya_tani_satirlari_yazar()
        {
            string klasor = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "canli-tani-" + Guid.NewGuid().ToString("N"));
            try
            {
                string yol;
                using (var t = new DosyaTani(klasor, () => 1234.56))
                {
                    yol = t.Yol;
                    t.Yaz("altyazi", ("en", "Çalış"), ("ocrMs", 12.5), ("hata", null));
                }
                var satirlar = System.IO.File.ReadAllLines(yol);
                Assert.Single(satirlar);
                Assert.Equal("{\"olay\":\"altyazi\",\"t\":1234.6,\"en\":\"Çalış\",\"ocrMs\":12.5,\"hata\":null}", satirlar[0]);
            }
            finally { try { System.IO.Directory.Delete(klasor, true); } catch (Exception) { } }
        }

        [Fact]
        public void kapali_dosya_taniya_yazmak_hata_vermez()
        {
            string klasor = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "canli-tani-" + Guid.NewGuid().ToString("N"));
            try
            {
                var t = new DosyaTani(klasor, () => 0);
                t.Dispose();
                t.Yaz("durdu", ("neden", "komut"));
                t.Dispose();
            }
            finally { try { System.IO.Directory.Delete(klasor, true); } catch (Exception) { } }
        }

        private sealed class SahteTani : ITaniKaydi
        {
            private readonly List<string> _l = new List<string>();
            public void Yaz(string tur, params (string anahtar, object deger)[] alanlar)
            {
                lock (_l) _l.Add(Protokol.Olay(tur, alanlar));
            }
            public List<string> Satirlar() { lock (_l) return _l.ToList(); }
        }

        // ---- Sahteler ----

        private sealed class Sahneler
        {
            public readonly SahteCikis Cikis = new SahteCikis();
            public readonly SahteEkranOkuyucu Ekran = new SahteEkranOkuyucu();
            public readonly SahteKaplama Kaplama = new SahteKaplama();
            public readonly SahteZamanlayicilar Zaman = new SahteZamanlayicilar();
            public readonly SunucuIstemcisi Sunucu;
            public int CeviriIstegi;
            private double _t;

            public Sahneler(string ceviriYaniti, HttpStatusCode ceviriDurumu = HttpStatusCode.OK, string ceviriGovdesi = null,
                            string oyunGovdesi = "{\"durum\":\"acik\"}",
                            HttpStatusCode durumDurumu = HttpStatusCode.OK, string durumGovdesi = "{\"durum\":\"etkin\"}",
                            string bellekGovdesi = "{\"surum\":\"0-0\",\"satirlar\":[]}")
            {
                var isleyici = new SahteIsleyici(yol =>
                {
                    if (yol.Contains("/bellek/")) return (HttpStatusCode.OK, bellekGovdesi);
                    if (yol.EndsWith("/ceviri")) { Interlocked.Increment(ref CeviriIstegi); return (ceviriDurumu, ceviriGovdesi ?? ceviriYaniti); }
                    if (yol.Contains("/oyun/")) return (HttpStatusCode.OK, oyunGovdesi);
                    if (yol.EndsWith("/durum")) return (durumDurumu, durumGovdesi);
                    return (HttpStatusCode.OK, "{\"ceviriler\":[]}");
                });
                Sunucu = new SunucuIstemcisi(isleyici, "https://ornek.invalid", "K", "C");
            }

            public Calistirici Calistirici(OnPlanBekcisi bekci, Func<int, bool> tus = null, ITaniKaydi tani = null)
            {
                var komut = new Komut { Tur = "basla", Lisans = "K", Oyun = "test:x", OyunAdi = "Test", Profil = Profil.Varsayilan };
                var ekranlar = new TekEkran();
                return new Calistirici(komut, Cikis, Sunucu, Ekran, bekci, Kaplama, Zaman, ekranlar, () => _t += 50, tus ?? (_ => false), tani);
            }
        }

        private sealed class SahteIsleyici : HttpMessageHandler
        {
            private readonly Func<string, (HttpStatusCode, string)> _yanit;
            public SahteIsleyici(Func<string, (HttpStatusCode, string)> yanit) { _yanit = yanit; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken iptal)
            {
                var (durum, govde) = _yanit(istek.RequestUri.AbsolutePath);
                return Task.FromResult(new HttpResponseMessage(durum) { Content = new StringContent(govde, Encoding.UTF8, "application/json") });
            }
        }

        private sealed class SahteCikis : ICikis
        {
            private readonly List<string> _l = new List<string>();
            public void Yaz(string jsonSatiri) { lock (_l) _l.Add(jsonSatiri); }
            public List<string> Satirlar() { lock (_l) return _l.ToList(); }
        }

        private sealed class SahteEkranOkuyucu : IEkranOkuyucu
        {
            public int AlSayisi, ImzaSayisi, OkuSayisi;
            public string[] Satirlar = new string[0];

            public Bitmap Al(Rectangle r) { Interlocked.Increment(ref AlSayisi); return new Bitmap(8, 4); }
            public byte[] KucukImza(Rectangle r) { Interlocked.Increment(ref ImzaSayisi); return new byte[64 * 36]; }
            public IReadOnlyList<OcrSatiri> Oku(Bitmap b)
            {
                Interlocked.Increment(ref OkuSayisi);
                return Satirlar.Select((m, i) => new OcrSatiri { Metin = m, Kutu = new Rectangle(10, 10 + 30 * i, 200, 24) }).ToList();
            }
        }

        private sealed class SahteKaplama : IKaplama
        {
            public int GizleSayisi;
            public string Ana;
            public void Konumla(Rectangle ekran, Rectangle altyaziBolgesi) { }
            public void AnaGoster(string metin, Rectangle kutu) { Ana = metin; }
            public void OncekiGoster(string metin) { }
            public void MenuGoster(IReadOnlyList<MenuKutusu> kutular) { }
            public void GizleHepsi() { Interlocked.Increment(ref GizleSayisi); Ana = null; }
            public void Dispose() { }
        }

        private sealed class SahteZamanlayicilar : IZamanlayicilar
        {
            public bool Basladi, Durduruldu;
            public void Baslat(Action yakalama, Action yoklama, Action ekBolge) { Basladi = true; }
            public void Durdur() { Durduruldu = true; }
            public void Dispose() { }
        }

        private sealed class TekEkran : IEkranKaynagi
        {
            private readonly Rectangle _e = new Rectangle(0, 0, 1920, 1080);
            public IReadOnlyList<Rectangle> Ekranlar() { return new[] { _e }; }
            public Rectangle Birincil() { return _e; }
            public Rectangle PencereDikdortgeni(IntPtr h) { return _e; }
        }
    }
}
