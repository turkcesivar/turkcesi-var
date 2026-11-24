using System.Security.Cryptography;
using System.Text;
using Canli;
using Xunit;

namespace Canli.Testler
{
    public class ProtokolTestleri
    {
        [Fact]
        public void basla_komutu_cozulur()
        {
            string satir = "{\"komut\":\"basla\",\"lisans\":\"L1\",\"oyun\":\"steam:123\",\"oyunAdi\":\"Oyun Adı\",\"profil\":null,\"sureciAdi\":\"Oyun.exe\"}";
            Assert.True(Protokol.KomutCoz(satir, out Komut k, out string hata));
            Assert.Null(hata);
            Assert.Equal("basla", k.Tur);
            Assert.Equal("L1", k.Lisans);
            Assert.Equal("steam:123", k.Oyun);
            Assert.Equal("Oyun Adı", k.OyunAdi);
            Assert.Equal("Oyun.exe", k.SurecAdi);
            Assert.Equal(new[] { 0.12, 0.78, 0.76, 0.17 }, k.Profil.AltyaziBolgesi);
            Assert.Equal("ikisi", k.Profil.Renk);
            Assert.Empty(k.Profil.EkBolgeler);
        }

        [Fact]
        public void basla_komutu_profille_cozulur()
        {
            string satir = "{\"komut\":\"basla\",\"lisans\":\"L\",\"oyun\":\"o\",\"oyunAdi\":\"a\",\"profil\":{\"altyaziBolgesi\":[0.1,0.8,0.8,0.15],\"renk\":\"sari\",\"ekBolgeler\":[[0,0,0.3,0.1]]},\"sureciAdi\":null}";
            Assert.True(Protokol.KomutCoz(satir, out Komut k, out _));
            Assert.Equal(new[] { 0.1, 0.8, 0.8, 0.15 }, k.Profil.AltyaziBolgesi);
            Assert.Equal("sari", k.Profil.Renk);
            Assert.Single(k.Profil.EkBolgeler);
            Assert.Equal(new[] { 0.0, 0.0, 0.3, 0.1 }, k.Profil.EkBolgeler[0]);
        }

        [Theory]
        [InlineData("duraklat")]
        [InlineData("devam")]
        [InlineData("dur")]
        [InlineData("cik")]
        public void basit_komutlar_cozulur(string tur)
        {
            Assert.True(Protokol.KomutCoz("{\"komut\":\"" + tur + "\"}", out Komut k, out _));
            Assert.Equal(tur, k.Tur);
        }

        [Fact]
        public void bozuk_json_gecersiz_komut()
        {
            Assert.False(Protokol.KomutCoz("{bozuk", out Komut k, out string hata));
            Assert.Null(k);
            Assert.Equal("gecersiz_komut", hata);
        }

        [Fact]
        public void bilinmeyen_komut_gecersiz()
        {
            Assert.False(Protokol.KomutCoz("{\"komut\":\"uc\"}", out Komut k, out string hata));
            Assert.Null(k);
            Assert.Equal("gecersiz_komut", hata);
        }

        [Fact]
        public void olay_tek_satir_json()
        {
            string s = Protokol.Olay("uyari", ("kod", "çeviri şğ"), ("kalan", 5));
            Assert.DoesNotContain("\n", s);
            Assert.Equal("{\"olay\":\"uyari\",\"kod\":\"çeviri şğ\",\"kalan\":5}", s);
            Assert.Equal("{\"olay\":\"hazir\"}", Protokol.Olay("hazir"));
        }

        [Fact]
        public void komut_satirindan_basla()
        {
            var args = new[] { "--lisans", "K", "--oyun", "test:x", "--oyunAdi", "Ad", "--renk", "sari",
                "--bolge", "0.1,0.8,0.8,0.15", "--ek", "0,0,0.3,0.1" };
            Komut k = Protokol.KomutSatirindan(args);
            Assert.Equal("basla", k.Tur);
            Assert.Equal("K", k.Lisans);
            Assert.Equal("test:x", k.Oyun);
            Assert.Equal("Ad", k.OyunAdi);
            Assert.Equal("sari", k.Profil.Renk);
            Assert.Equal(new[] { 0.1, 0.8, 0.8, 0.15 }, k.Profil.AltyaziBolgesi);
            Assert.Single(k.Profil.EkBolgeler);
            Assert.Equal(new[] { 0.0, 0.0, 0.3, 0.1 }, k.Profil.EkBolgeler[0]);
        }

        [Fact]
        public void cihaz_ozeti_sabit()
        {
            string beklenen;
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes("turkcesivar:ABC"));
                var sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                beklenen = sb.ToString();
            }
            Assert.Equal(beklenen, CihazKimligi.Ozet("ABC"));
            Assert.Equal(64, CihazKimligi.Ozet("ABC").Length);
        }
    }
}
