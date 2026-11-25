using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        // Metro tanı kaydı (20:14): geri gelen satır ve aynı çevirisi olan yeni okuma kutuda iki kez görünüyordu.
        [Fact]
        public void alt_alta_ayni_ceviri_iki_kez_gorunmez()
        {
            _t = 0;
            var d = Yeni();
            d.Okundu("YERMAK: A mutant?! Watchman!", _kutu);
            _t = 500; d.CeviriGeldi("YERMAK: A mutant?! Watchman!", "YERMAK: Bir mutant mu?! Bekçi!");
            _t = 1000; d.Okundu("YERMAK: Whaaaa", _kutu);
            _t = 1500; d.CeviriGeldi("YERMAK: Whaaaa", "YERMAK: Vaaay!");
            _t = 1700; d.Okundu("", _kutu);
            _t = 1900; d.Okundu("YERMAK: A mutant?! Watchman!", _kutu);   // eski satır geri geldi
            var satirlar = d.Simdi().AnaMetin.Split('\n');
            Assert.Equal(satirlar.Length, satirlar.Distinct().Count());
            Assert.Equal("YERMAK: Bir mutant mu?! Bekçi!", satirlar.Last());

            // Aynı satırın farklı okuması yeni satır sayılsa da aynı Türkçe ikinci kez eklenmez.
            _t = 2500; d.Okundu("WORKER: What's with that mumbling? SPEAKER: Team leader", _kutu);
            _t = 3000; d.CeviriGeldi("WORKER: What's with that mumbling? SPEAKER: Team leader", "WORKER: Bu mırıldanma da ne?");
            _t = 3200; d.Okundu("WORKER: What's with that mumbling?", _kutu);
            _t = 3300; d.CeviriGeldi("WORKER: What's with that mumbling?", "WORKER: Bu mırıldanma da ne?");
            satirlar = d.Simdi().AnaMetin.Split('\n');
            Assert.Equal(satirlar.Length, satirlar.Distinct().Count());
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

        // Örnekler Metro Exodus tanı kaydından: aynı altyazının gürültülü okumaları.
        [Fact]
        public void gurultulu_okumalar_ayni_satir_sayilir()
        {
            var d = Yeni();
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("Did they get you? No? Good.", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("Did thyrget you? NO? Good.", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("Did they-get yoo No? Goodk.", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("Did they get you? Nob", _kutu).Tur);
            // Farklı ama benzer satırlar ayrı kalır.
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("Did they hurt you?", _kutu).Tur);
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("One more!", _kutu).Tur);
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("One down!", _kutu).Tur);
        }

        [Fact]
        public void altyazi_kisa_kaybolup_donunce_yeniden_cevrilmez_turkcesi_gosterilir()
        {
            var d = Yeni();
            d.Okundu("Did they get you? No? Good.", _kutu);
            d.CeviriGeldi("Did they get you? No? Good.", "Sana bir şey oldu mu? Hayır mı? İyi.");
            Assert.Equal(KararTuru.Bitti, d.Okundu("", _kutu).Tur);
            _t += 1500;   // tutma süresi geçti, Türkçe kalktı
            Assert.Null(d.Simdi().AnaMetin);
            Assert.Equal(KararTuru.Yok, d.Okundu("Did they-get yoo No? Goodk.", _kutu).Tur);
            Assert.Equal("Sana bir şey oldu mu? Hayır mı? İyi.", d.Simdi().AnaMetin);
            // 4 sn'den sonra aynı satır yeniden görülürse yeni satır sayılır (yerel bellek karşılar).
            d.Okundu("", _kutu);
            _t += 5000;
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("Did they get you? No? Good.", _kutu).Tur);
        }

        [Fact]
        public void gurultu_zinciri_son_okumalara_baglanir()
        {
            var d = Yeni();
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("Let's pick up the pace, now the whole area knows we're here!", _kutu).Tur);
            _t += 200; Assert.Equal(KararTuru.Yok, d.Okundu("Let's pick up the pace, now the whole area knoW's we!re ere!", _kutu).Tur);
            _t += 200; Assert.Equal(KararTuru.Yok, d.Okundu("Let's pick up-th pce, nowthe whole- area khows we're here!", _kutu).Tur);
            _t += 200; Assert.Equal(KararTuru.Yok, d.Okundu("Let's pick up the pace, now the 'Whole area khowsswe're here!", _kutu).Tur);
        }

        [Fact]
        public void ocr_satirlari_yukaridan_asagi_soldan_saga_birlesir()
        {
            var satirlar = new[]
            {
                new Canli.Goruntu.OcrSatiri { Metin = "it was far away...", Kutu = new Rectangle(400, 60, 300, 30) },
                new Canli.Goruntu.OcrSatiri { Metin = "At least,", Kutu = new Rectangle(100, 62, 250, 30) },
                new Canli.Goruntu.OcrSatiri { Metin = "Something large roaring.", Kutu = new Rectangle(150, 20, 500, 30) },
            };
            Assert.Equal("Something large roaring. At least, it was far away...",
                         string.Join(" ", AltyaziDurumu.Sirala(satirlar).Select(x => x.Metin)));
        }

        [Fact]
        public void gecerli_satirin_eski_okumasiyla_gelen_ceviri_ana_kutuda_gosterilir()
        {
            var d = Yeni();
            d.Okundu("Let's now the whole area knows we're here!", _kutu);
            _t += 200;
            d.Okundu("Let's pick up the pace, now the whole area knows we're here!", _kutu);   // iyileşme
            _t += 1500;
            // İlk (yarım) okumanın çevirisi geldi: hâlâ ekrandaki satırın çevirisi; gri kutuya gitmez.
            d.CeviriGeldi("Let's now the whole area knows we're here!", "Artık bütün bölge burada olduğumuzu biliyor!");
            Assert.Equal("Artık bütün bölge burada olduğumuzu biliyor!", d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
            d.CeviriGeldi("Let's pick up the pace, now the whole area knows we're here!", "Hızlanalım, bütün bölge burada olduğumuzu biliyor!");
            Assert.Equal("Hızlanalım, bütün bölge burada olduğumuzu biliyor!", d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
            // Yarım okumanın çevirisi tamamınınkinden sonra gelirse yok sayılır.
            d.CeviriGeldi("Let's now the whole area knows we're here!", "Artık bütün bölge burada olduğumuzu biliyor!");
            Assert.Equal("Hızlanalım, bütün bölge burada olduğumuzu biliyor!", d.Simdi().AnaMetin);
        }

        [Fact]
        public void yarim_okumadan_sonra_belirgin_uzun_okuma_bir_kez_yeniden_gonderilir()
        {
            var d = Yeni();
            Assert.Equal(KararTuru.YeniSatir, d.Okundu("Let's now the whole area knows we're here!", _kutu).Tur);
            var k = d.Okundu("Let's pick up the pace, now the whole area knows we're here!", _kutu);
            Assert.Equal(KararTuru.YeniSatir, k.Tur);
            Assert.Equal("Let's pick up the pace, now the whole area knows we're here!", k.Metin);
            Assert.Equal(KararTuru.Yok, d.Okundu("Let's pick up the pace, now the whole area knows we're here! ", _kutu).Tur);
            Assert.Equal(KararTuru.Yok, d.Okundu("Let's pick up-thé pace, nowthe whole- area khows we're here!", _kutu).Tur);
        }

        [Fact]
        public void ayni_satirin_yeni_cevirisi_onceki_kutuya_itmez()
        {
            var d = Yeni();
            d.Okundu("Let's now the whole area knows we're here!", _kutu);
            d.CeviriGeldi("Let's now the whole area knows we're here!", "Artık bütün bölge burada olduğumuzu biliyor!");
            _t += 300;
            d.Okundu("Let's pick up the pace, now the whole area knows we're here!", _kutu);
            d.CeviriGeldi("Let's pick up the pace, now the whole area knows we're here!", "Hızlanalım, artık bütün bölge burada olduğumuzu biliyor!");
            var g = d.Simdi();
            Assert.Equal("Hızlanalım, artık bütün bölge burada olduğumuzu biliyor!", g.AnaMetin);
            Assert.Null(g.Onceki);
        }

        [Fact]
        public void yerel_bellek_gurultulu_okumayi_bulur()
        {
            var d = Yeni();
            d.YerelBellekYaz("Did they get you? No? Good.", "Sana bir şey oldu mu? Hayır mı? İyi.");
            Assert.Equal("Sana bir şey oldu mu? Hayır mı? İyi.", d.YerelBellekBul("Did thyrget you? NO? Good."));
            Assert.Null(d.YerelBellekBul("They probably heard us coming down from the roof."));
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
        public void gec_ceviri_ana_kutuda_konusmalar_alt_alta()
        {
            // Hızlı diyalog: birinci satırın çevirisi ikinci satır başladıktan sonra geldi → yine ana kutuda;
            // ikincisi gelince birincisi okunabilsin diye gri kutuya geçer.
            var d = Yeni();
            d.Okundu("First line of talk.", _kutu);
            _t = 1000; d.Okundu("Second line of talk.", _kutu);
            _t = 3000; d.CeviriGeldi("First line of talk.", "Birinci satir.");
            Assert.Equal("Birinci satir.", d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
            _t = 3500; d.CeviriGeldi("Second line of talk.", "Ikinci satir.");
            // Konuşmalar alt alta, aynı renkte (soluk ayrı kutu yok): en yenisi en altta.
            Assert.Equal("Birinci satir.\nIkinci satir.", d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
            _t = 8999; Assert.Equal("Birinci satir.\nIkinci satir.", d.Simdi().AnaMetin);   // birinci 3000 ms'de gösterildi
            _t = 9001; Assert.Equal("Ikinci satir.", d.Simdi().AnaMetin);
        }

        [Fact]
        public void en_cok_uc_konusma_alt_alta()
        {
            var d = Yeni();
            string[] en = { "First line of talk.", "Second speech here.", "Third one now.", "Fourth and last." };
            for (int i = 0; i < 4; i++)
            {
                _t += 500; d.Okundu(en[i], _kutu);
                _t += 300; d.CeviriGeldi(en[i], "Satir " + (i + 1));
            }
            Assert.Equal("Satir 2\nSatir 3\nSatir 4", d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
        }

        [Fact]
        public void eski_satirin_cevirisi_yenisinden_sonra_gelirse_yok_sayilir()
        {
            var d = Yeni();
            d.Okundu("First line of talk.", _kutu);
            _t = 500; d.Okundu("Second line of talk.", _kutu);
            _t = 1500; d.CeviriGeldi("Second line of talk.", "Ikinci satir.");
            _t = 2000; d.CeviriGeldi("First line of talk.", "Birinci satir.");
            Assert.Equal("Ikinci satir.", d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
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
        public void yeni_satir_gelince_onceki_turkce_yenisi_gelene_kadar_kalir()
        {
            var d = Yeni();
            d.Okundu("First line of talk.", _kutu);
            _t = 200; d.CeviriGeldi("First line of talk.", "Birinci satir.");
            _t = 1000; d.Okundu("Second line of talk.", _kutu);
            // Yer tutucu Türkçeyi silmez: ikinci çeviri gelene kadar birinci kalır.
            Assert.Equal("Birinci satir.", d.Simdi().AnaMetin);
            _t = 1500; d.CeviriGeldi("Second line of talk.", "Ikinci satir.");
            Assert.Equal("Birinci satir.\nIkinci satir.", d.Simdi().AnaMetin);
            Assert.Null(d.Simdi().Onceki);
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
            Assert.EndsWith("6", b[4]);   // geçerli (7.) satır yok
            Assert.EndsWith("2", b[0]);
            Assert.DoesNotContain(b, x => x.EndsWith("7"));
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
