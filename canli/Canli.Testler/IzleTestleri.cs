using System;
using System.Collections.Generic;
using System.Linq;
using Canli.Mantik;
using Xunit;

namespace Canli.Testler
{
    public class IzleTestleri
    {
        [Fact]
        public void izle_komutu_cozulur()
        {
            Assert.True(Protokol.KomutCoz("{\"komut\":\"izle\"}", out Komut k, out _));
            Assert.Equal("izle", k.Tur);
        }

        [Fact]
        public void izleyici_yalniz_degisince_yazar()
        {
            var yollar = new Queue<string>(new[] { Y("C:/A/a.exe"), Y("c:/a/A.EXE"), Y("C:/B/b.exe"), null });
            var cikis = new SahteCikis();
            var izleyici = new OnPlanIzleyici(() => yollar.Dequeue(), cikis);
            for (int i = 0; i < 4; i++) izleyici.Tik();
            Assert.Equal(new[]
            {
                "{\"olay\":\"onplan\",\"yol\":\"" + J("C:/A/a.exe") + "\"}",
                "{\"olay\":\"onplan\",\"yol\":\"" + J("C:/B/b.exe") + "\"}",
                "{\"olay\":\"onplan\",\"yol\":null}"
            }, cikis.Satirlar().ToArray());
        }

        private static string Y(string s) { return s.Replace('/', (char)92); }
        private static string J(string s) { return s.Replace("/", new string((char)92, 2)); }

        private sealed class SahteCikis : ICikis
        {
            private readonly List<string> _l = new List<string>();
            public void Yaz(string jsonSatiri) { lock (_l) _l.Add(jsonSatiri); }
            public List<string> Satirlar() { lock (_l) return _l.ToList(); }
        }
    }
}
