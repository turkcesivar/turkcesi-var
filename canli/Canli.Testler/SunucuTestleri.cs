using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Canli;
using Xunit;

namespace Canli.Testler
{
    public class SunucuTestleri
    {
        private sealed class SahteIsleyici : HttpMessageHandler
        {
            public readonly List<(HttpRequestMessage istek, string govde)> Istekler = new List<(HttpRequestMessage, string)>();
            public Func<HttpRequestMessage, string, (HttpStatusCode, string)> Yanit;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken iptal)
            {
                string govde = istek.Content == null ? null : await istek.Content.ReadAsStringAsync();
                lock (Istekler) Istekler.Add((istek, govde));
                var (durum, metin) = Yanit(istek, govde);
                return new HttpResponseMessage(durum) { Content = new StringContent(metin, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class AskidaIsleyici : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken iptal)
            {
                await Task.Delay(Timeout.Infinite, iptal);
                return null;
            }
        }

        private sealed class AskidaGovdeIsleyici : HttpMessageHandler
        {
            private sealed class DonmusAkis : System.IO.Stream
            {
                public override bool CanRead { get { return true; } }
                public override bool CanSeek { get { return false; } }
                public override bool CanWrite { get { return false; } }
                public override long Length { get { throw new NotSupportedException(); } }
                public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
                public override void Flush() { }
                public override int Read(byte[] b, int o, int c) { Thread.Sleep(Timeout.Infinite); return 0; }
                public override System.Threading.Tasks.Task<int> ReadAsync(byte[] b, int o, int c, CancellationToken t)
                {
                    return new TaskCompletionSource<int>().Task;
                }
                public override long Seek(long o, System.IO.SeekOrigin s) { throw new NotSupportedException(); }
                public override void SetLength(long v) { throw new NotSupportedException(); }
                public override void Write(byte[] b, int o, int c) { throw new NotSupportedException(); }
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken iptal)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new DonmusAkis()) });
            }
        }

        private static SunucuIstemcisi Yap(HttpMessageHandler h)
        {
            return new SunucuIstemcisi(h, "https://api.test", "K", "ozet123");
        }

        [Fact]
        public async Task basliklar_dogru()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => (HttpStatusCode.OK, "{\"tr\":\"Merhaba\",\"kaynak\":\"model\",\"okunamaz\":false,\"kalan\":7}") };
            var baglam = new[] { "1", "2", "3", "4", "5", "6", "7" };
            await Yap(h).CeviriAsync("steam:1", "Oyun", "Hello", baglam);
            var (istek, govde) = h.Istekler.Single();
            Assert.Equal("Bearer", istek.Headers.Authorization.Scheme);
            Assert.Equal("K", istek.Headers.Authorization.Parameter);
            Assert.Equal("ozet123", istek.Headers.GetValues("X-Cihaz").Single());
            Assert.Equal("https://api.test/ceviri", istek.RequestUri.ToString());
            var j = JsonDocument.Parse(govde).RootElement;
            Assert.Equal("steam:1", j.GetProperty("oyun").GetString());
            Assert.Equal("Oyun", j.GetProperty("oyunAdi").GetString());
            Assert.Equal("Hello", j.GetProperty("en").GetString());
            var b = j.GetProperty("baglam").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(new[] { "3", "4", "5", "6", "7" }, b);
        }

        [Fact]
        public async Task ceviri_basarili()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => (HttpStatusCode.OK, "{\"tr\":\"Merhaba\",\"kaynak\":\"bellek\",\"okunamaz\":false,\"kalan\":42}") };
            var s = await Yap(h).CeviriAsync("o", "a", "Hello", new string[0]);
            Assert.Equal("Merhaba", s.Tr);
            Assert.Equal(42, s.Kalan);
            Assert.False(s.Okunamaz);
            Assert.Null(s.HataKodu);

            h.Yanit = (i, g) => (HttpStatusCode.OK, "{\"okunamaz\":true}");
            var o = await Yap(h).CeviriAsync("o", "a", "xx", null);
            Assert.True(o.Okunamaz);
            Assert.Null(o.HataKodu);
        }

        [Fact]
        public async Task oyun_kimligi_kodlanir_ve_profil_okunur()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => (HttpStatusCode.OK, "{\"durum\":\"acik\",\"profil\":{\"renk\":\"sari\",\"altyaziBolgesi\":[0.1,0.8,0.8,0.15]}}") };
            var b = await Yap(h).OyunAsync("steam:123");
            Assert.Equal("acik", b.Durum);
            Assert.Equal("sari", b.Profil.Renk);
            Assert.Equal(new[] { 0.1, 0.8, 0.8, 0.15 }, b.Profil.AltyaziBolgesi);
            Assert.EndsWith("/oyun/steam%3A123", h.Istekler[0].istek.RequestUri.AbsoluteUri);
        }

        [Theory]
        [InlineData("lisans_yok", 401, "hata", true)]
        [InlineData("cihaz_dustu", 401, "hata", true)]
        [InlineData("suresi_bitti", 403, "hata", true)]
        [InlineData("paketli", 403, "hata", true)]
        [InlineData("yasakli", 403, "hata", true)]
        [InlineData("hak_bitti", 429, "uyari", false)]
        [InlineData("gun_bitti", 429, "uyari", false)]
        [InlineData("butce", 503, "uyari", false)]
        [InlineData("hiz", 429, "uyari", false)]
        [InlineData("gecici_hata", 502, "uyari", false)]
        [InlineData("deneme_siniri", 429, "uyari", false)]
        [InlineData("cok_uzun", 413, "uyari", false)]
        [InlineData("gecersiz", 400, "uyari", false)]
        [InlineData("yapilandirma", 500, "hata", true)]
        [InlineData("cihaz_yok", 400, "hata", true)]
        public async Task hata_kodlari_siniflandirilir(string kod, int durum, string tur, bool durdur)
        {
            var h = new SahteIsleyici { Yanit = (i, g) => ((HttpStatusCode)durum, "{\"hata\":\"" + kod + "\"}") };
            var s = await Yap(h).CeviriAsync("o", "a", "Hi", null);
            Assert.Equal(kod, s.HataKodu);
            Assert.Equal((tur, durdur), HataYonetimi.Siniflandir(kod));
        }

        [Fact]
        public void cevrimdisi_uyaridir()
        {
            Assert.Equal(("uyari", false), HataYonetimi.Siniflandir("cevrimdisi"));
        }

        [Fact]
        public async Task bozuk_hata_govdesi_gecici_hata_olur()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => (HttpStatusCode.BadGateway, "<html>nope</html>") };
            Assert.Equal("gecici_hata", (await Yap(h).CeviriAsync("o", "a", "Hi", null)).HataKodu);
            h.Yanit = (i, g) => (HttpStatusCode.OK, "bozuk");
            Assert.Equal("gecici_hata", (await Yap(h).CeviriAsync("o", "a", "Hi", null)).HataKodu);
        }

        [Fact]
        public async Task sunucu_yanitsiz_yer_tutucu_kalkar()
        {
            var s = Yap(new AskidaIsleyici());
            s.ZamanAsimi = TimeSpan.FromMilliseconds(200);
            var sonuc = await s.CeviriAsync("o", "a", "Hi", null);
            Assert.Equal("cevrimdisi", sonuc.HataKodu);
            Assert.Equal("cevrimdisi", (await s.MenuAsync("o", "a", new[] { "Play" })).HataKodu);
            Assert.Equal("cevrimdisi", (await s.OyunAsync("x")).HataKodu);
        }

        [Fact]
        public async Task ag_hatasi_cevrimdisi_olur()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => throw new HttpRequestException("kopuk") };
            Assert.Equal("cevrimdisi", (await Yap(h).CeviriAsync("o", "a", "Hi", null)).HataKodu);
        }

        private static string MenuYanit(string govde, string neden = null)
        {
            var oge = JsonDocument.Parse(govde).RootElement.GetProperty("ogeler").EnumerateArray().Select(x => x.GetString()).ToArray();
            var ceviri = oge.Select(x => x == "bos" ? "null" : "\"T-" + x + "\"");
            return "{\"ceviriler\":[" + string.Join(",", ceviri) + "]" + (neden != null ? ",\"neden\":\"" + neden + "\"" : "") + "}";
        }

        [Fact]
        public async Task menu_null_ogeler_ve_neden()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => (HttpStatusCode.OK, MenuYanit(g, "hak_bitti")) };
            var uzun = new string('x', 301);
            var s = await Yap(h).MenuAsync("o", "a", new[] { "Play", "bos", uzun, "Quit" });
            Assert.Equal(new[] { "T-Play", null, null, "T-Quit" }, s.Ceviriler);
            Assert.Equal("hak_bitti", s.Neden);
            Assert.Null(s.HataKodu);
        }

        [Fact]
        public async Task govde_sinirlari()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => (HttpStatusCode.OK, MenuYanit(g)) };
            var c = Yap(h);

            var uzun = await c.CeviriAsync("o", "a", new string('a', 301), null);
            Assert.Equal("cok_uzun", uzun.HataKodu);
            Assert.Empty(h.Istekler);

            var baglam = new[] { new string('b', 400) };
            await c.CeviriAsync("o", "a", new string('a', 300), baglam);
            var b = JsonDocument.Parse(h.Istekler[0].govde).RootElement.GetProperty("baglam")[0].GetString();
            Assert.Equal(300, b.Length);
            h.Istekler.Clear();

            // 20 küçük öğe -> 8+8+4
            var ogeler = Enumerable.Range(0, 20).Select(i => "o" + i).ToArray();
            var s = await c.MenuAsync("o", "a", ogeler);
            Assert.Equal(3, h.Istekler.Count);
            Assert.All(h.Istekler, x => Assert.True(JsonDocument.Parse(x.govde).RootElement.GetProperty("ogeler").GetArrayLength() <= 8));
            Assert.Equal(ogeler.Select(x => "T-" + x).ToArray(), s.Ceviriler);
            h.Istekler.Clear();

            // karakter sınırı: 6 öğe x 300 = 1800 > 1500 -> en az 2 parça
            var buyuk = Enumerable.Range(0, 6).Select(i => new string((char)('a' + i), 300)).ToArray();
            var s2 = await c.MenuAsync("o", "a", buyuk);
            Assert.True(h.Istekler.Count >= 2);
            foreach (var (_, g) in h.Istekler)
                Assert.True(JsonDocument.Parse(g).RootElement.GetProperty("ogeler").EnumerateArray().Sum(x => x.GetString().Length) <= 1500);
            Assert.Equal(buyuk.Select(x => "T-" + x).ToArray(), s2.Ceviriler);
        }

        [Fact]
        public async Task menu_durduran_hata_yayilir()
        {
            var h = new SahteIsleyici
            {
                Yanit = (i, g) => g.Contains("o9") ? (HttpStatusCode.Forbidden, "{\"hata\":\"suresi_bitti\"}") : (HttpStatusCode.OK, MenuYanit(g))
            };
            var ogeler = Enumerable.Range(0, 10).Select(i => "o" + i).ToArray();
            var s = await Yap(h).MenuAsync("o", "a", ogeler);
            Assert.Equal("suresi_bitti", s.HataKodu);
        }

        [Fact]
        public async Task govde_okumasi_askida_zaman_asimi()
        {
            var s = Yap(new AskidaGovdeIsleyici());
            s.ZamanAsimi = TimeSpan.FromMilliseconds(300);
            var gorev = s.CeviriAsync("o", "a", "Hi", null);
            Assert.Same(gorev, await Task.WhenAny(gorev, Task.Delay(5000)));
            Assert.Equal("cevrimdisi", (await gorev).HataKodu);
        }

        [Fact]
        public async Task menu_durduran_uyariyi_ezer()
        {
            var h = new SahteIsleyici
            {
                Yanit = (i, g) => g.Contains("o9") ? (HttpStatusCode.InternalServerError, "{\"hata\":\"yapilandirma\"}") : ((HttpStatusCode)429, "{\"hata\":\"hiz\"}")
            };
            var ogeler = Enumerable.Range(0, 10).Select(i => "o" + i).ToArray();
            var s = await Yap(h).MenuAsync("o", "a", ogeler);
            Assert.Equal("yapilandirma", s.HataKodu);
        }

        [Fact]
        public async Task menu_hepsi_atlanirsa_hata_kodu()
        {
            var h = new SahteIsleyici { Yanit = (i, g) => (HttpStatusCode.OK, "{}") };
            var c = Yap(h);
            Assert.Equal("cok_uzun", (await c.MenuAsync("o", "a", new[] { new string('x', 301) })).HataKodu);
            Assert.Null((await c.MenuAsync("o", "a", new string[] { null })).HataKodu);
            Assert.Empty(h.Istekler);
        }
    }
}
