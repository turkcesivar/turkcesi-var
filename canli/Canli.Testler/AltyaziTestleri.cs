using System;
using System.Collections.Generic;
using System.Drawing;
using Canli.Mantik;
using Xunit;

namespace Canli.Testler
{
    public class AltyaziTestleri
    {
        private double _t;
        private readonly Rectangle _kutu = new Rectangle(10, 20, 300, 40);

        private AltyaziDurumu Yeni() { return new AltyaziDurumu(() => _t); }

        private sealed class SahteEkranlar : IEkranKaynagi
        {
            public List<Rectangle> Liste = new List<Rectangle>();
            public Rectangle Pencere;
            public IReadOnlyList<Rectangle> Ekranlar() { return Liste; }
            public Rectangle Birincil() { return Liste[0]; }
            public Rectangle PencereDikdortgeni(IntPtr h) { return Pencere; }
        }

        [Fact]
        public void benzerlik_titreme_ayni_satir()
        {
            Assert.True(Benzerlik.Oran("Collezolfo, we must go now.", "Collezoifo, we must go now.") >= 0.80);
            Assert.True(Benzerlik.Oran("Collezolfo", "Collezoifo") >= 0.80);
            Assert.Equal(1.0, Benzerlik.Oran("ABC def", "abc DEF"));
            Assert.True(Benzerlik.Oran("Hello there", "Completely different") < 0.5);

            var d = Yeni();
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("Collezolfo, we must go now.", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("Collezoifo, we must go now.", _kutu).Tur);
        }

        [Fact]
        public void yeni_satir_yer_tutucu()
        {
            var d = Yeni();
            var k = d.Okundu("Hello there, friend.", _kutu);
            Assert.Equal(KararTuru.YeniSatir, k.Tur);
            Assert.Equal("Hello there, friend.", k.Metin);
            Assert.Equal(_kutu, k.Kutu);
            var g = d.Simdi();
            Assert.Equal("…", g.AnaMetin);
            Assert.Equal(_kutu, g.AnaKutu);
            Assert.Null(g.Onceki);
        }

        [Fact]
        public void bos_okuma_satir_yokken_yok_ve_bitti_bir_kez()
        {
            var d = Yeni();
            Assert.Equal(KararTuru.Yok, d.Okundu("", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("a1", _kutu).Tur);
            d.Okundu("Hello there, friend.", _kutu);
            Assert.Equal(KararTuru.Bitti, d.Okundu("x", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("12 3", _kutu).Tur);
        }

        [Fact]
        public void ceviri_gelince_ana_kutu()
        {
            var d = Yeni();
            d.Okundu("Hello there, friend.", _kutu);
            _t = 400;
            d.CeviriGeldi("Hello there, friend.", "Merhaba dostum.");
            var g = d.Simdi();
            Assert.Equal("Merhaba dostum.", g.AnaMetin);
            Assert.Equal(_kutu, g.AnaKutu);
        }

        [Fact]
        public void satir_bitince_tutma_1200ms()
        {
            var d = Yeni();
            d.Okundu("Hello there, friend.", _kutu);
            _t = 300; d.CeviriGeldi("Hello there, friend.", "Merhaba dostum.");
            _t = 1000; Assert.Equal(KararTuru.Bitti, d.Okundu("", _kutu).Tur);
            _t = 2199; Assert.Equal("Merhaba dostum.", d.Simdi().AnaMetin);
            _t = 2201; Assert.Null(d.Simdi().AnaMetin);
        }

        [Fact]
        public void satir_bitince_yer_tutucu_hemen_kalkar()
        {
            var d = Yeni();
            d.Okundu("Hello there, friend.", _kutu);
            _t = 100; d.Okundu("", _kutu);
            Assert.Null(d.Simdi().AnaMetin);
        }

        [Fact]
        public void yer_tutucu_10sn_sonra_kalkar()
        {
            var d = Yeni();
            d.Okundu("Hello there, friend.", _kutu);
            _t = 9999; Assert.Equal("…", d.Simdi().AnaMetin);
            _t = 10001; Assert.Null(d.Simdi().AnaMetin);
        }

        [Fact]
        public void gec_ceviri_onceki_kutuda()
        {
            var d = Yeni();
            d.Okundu("First line of talk.", _kutu);
            _t = 1000; d.Okundu("Second line of talk.", _kutu);
            _t = 3000; d.CeviriGeldi("First line of talk.", "Birinci satir.");
            var g = d.Simdi();
            Assert.Equal("Birinci satir.", g.Onceki);
            Assert.Equal("…", g.AnaMetin);
        }

        [Fact]
        public void gec_ceviri_satir_yoksa_ana_kutuda_2200ms()
        {
            var d = Yeni();
            d.Okundu("First line of talk.", _kutu);
            _t = 500; d.Okundu("", _kutu);
            _t = 3000; d.CeviriGeldi("First line of talk.", "Birinci satir.");
            Assert.Equal("Birinci satir.", d.Simdi().AnaMetin);
            _t = 5199; Assert.Equal("Birinci satir.", d.Simdi().AnaMetin);
            _t = 5201; Assert.Null(d.Simdi().AnaMetin);
        }

        [Fact]
        public void cok_gec_ceviri_gosterilmez()
        {
            var d = Yeni();
            d.Okundu("First line of talk.", _kutu);
            _t = 500; d.Okundu("", _kutu);
            _t = 5001; d.CeviriGeldi("First line of talk.", "Birinci satir.");
            Assert.Null(d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
        }

        [Fact]
        public void onceki_4sn_soluk()
        {
            var d = Yeni();
            d.Okundu("First line of talk.", _kutu);
            _t = 200; d.CeviriGeldi("First line of talk.", "Birinci satir.");
            _t = 1000; d.Okundu("Second line of talk.", _kutu);
            _t = 1500; d.CeviriGeldi("Second line of talk.", "Ikinci satir.");
            Assert.Equal("Birinci satir.", d.Simdi().Onceki);
            Assert.Equal("Ikinci satir.", d.Simdi().AnaMetin);
            _t = 5499; Assert.Equal("Birinci satir.", d.Simdi().Onceki);
            _t = 5501; Assert.Null(d.Simdi().Onceki);
        }

        [Fact]
        public void okunamaz_ceviri_yer_tutucuyu_kaldirir()
        {
            var d = Yeni();
            d.Okundu("Hello there, friend.", _kutu);
            d.CeviriGeldi("Hello there, friend.", "### okunamadi");
            Assert.Null(d.Simdi().AnaMetin);
        }

        [Fact]
        public void baglam_son_5()
        {
            var d = Yeni();
            for (int i = 1; i <= 7; i++)
            {
                _t += 100;
                d.Okundu("Distinct line number " + new string((char)('a' + i * 2), 6) + i, _kutu);
            }
            var b = d.Baglam;
            Assert.Equal(5, b.Count);
            Assert.EndsWith("7", b[4]);
            Assert.EndsWith("3", b[0]);
        }

        [Fact]
        public void yerel_bellek_085()
        {
            var d = Yeni();
            d.YerelBellekYaz("Collezolfo, we must go now.", "Collezolfo, simdi gitmeliyiz.");
            Assert.Equal("Collezolfo, simdi gitmeliyiz.", d.YerelBellekBul("Collezolfo, we must go now."));
            Assert.Equal("Collezolfo, simdi gitmeliyiz.", d.YerelBellekBul("Collezoifo, we must go now."));
            Assert.Null(d.YerelBellekBul("Something else entirely here."));
        }

        [Fact]
        public void yerel_bellek_2000_kayit_en_eskisi_duser()
        {
            var d = Yeni();
            for (int i = 0; i < 2001; i++) d.YerelBellekYaz("anahtar-" + i * 7919, "tr" + i);
            Assert.Null(d.YerelBellekBul("anahtar-0"));
            Assert.Equal("tr1", d.YerelBellekBul("anahtar-7919"));
            Assert.Equal("tr2000", d.YerelBellekBul("anahtar-" + 2000 * 7919));
        }

        [Fact]
        public void ekran_secimi_oyun_penceresine_gore()
        {
            var s = new SahteEkranlar();
            s.Liste.Add(new Rectangle(0, 0, 1920, 1080));
            s.Liste.Add(new Rectangle(1920, 0, 2560, 1440));
            var h = new IntPtr(42);

            s.Pencere = new Rectangle(2000, 100, 1280, 720);       // merkez 2. ekranda
            Assert.Equal(s.Liste[1], EkranSecici.OyunEkrani(h, s));

            s.Pencere = new Rectangle(-100, -50, 1000, 600);       // merkez 1. ekranda
            Assert.Equal(s.Liste[0], EkranSecici.OyunEkrani(h, s));

            s.Pencere = new Rectangle(1500, 1300, 1000, 1000);     // merkezi (2000,1800): hiçbirinde; 2. ekranla daha çok kesişir
            Assert.Equal(s.Liste[1], EkranSecici.OyunEkrani(h, s));

            s.Pencere = new Rectangle(9000, 9000, 100, 100);       // hiçbiriyle kesişmez
            Assert.Equal(s.Liste[0], EkranSecici.OyunEkrani(h, s));

            Assert.Equal(s.Liste[0], EkranSecici.OyunEkrani(IntPtr.Zero, s));
        }

        [Fact]
        public void oranlar_piksele()
        {
            var r = EkranSecici.OranlariPiksele(new Rectangle(0, 0, 2560, 1440), new[] { 0.12, 0.78, 0.76, 0.17 });
            Assert.Equal(new Rectangle(307, 1123, 1946, 245), r);
            var r2 = EkranSecici.OranlariPiksele(new Rectangle(1920, 0, 2560, 1440), new[] { 0.12, 0.78, 0.76, 0.17 });
            Assert.Equal(1920 + 307, r2.X);
        }

        [Fact]
        public void siyah_kare_uyarisi()
        {
            var s = new SiyahKareSayaci();
            int uyari = 0;
            for (int i = 0; i < 60; i++) if (s.Ekle(true)) uyari++;
            Assert.Equal(1, uyari);

            Assert.False(s.Ekle(false));           // sıfırlar
            for (int i = 0; i < 24; i++) Assert.False(s.Ekle(true));
            Assert.True(s.Ekle(true));             // 25. yeniden
        }
    }
}
