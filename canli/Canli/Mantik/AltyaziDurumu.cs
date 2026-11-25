using System;
using System.Collections.Generic;
using System.Linq;
using Canli.Goruntu;
using System.Drawing;
using System.Text.RegularExpressions;

namespace Canli.Mantik
{
    public enum KararTuru { Yok, YeniSatir, Bitti }

    public sealed class Karar
    {
        public KararTuru Tur { get; private set; }
        public string Metin { get; private set; }
        public Rectangle Kutu { get; private set; }

        public static readonly Karar Yok = new Karar { Tur = KararTuru.Yok };
        public static readonly Karar Bitti = new Karar { Tur = KararTuru.Bitti };
        public static Karar YeniSatir(string metin, Rectangle kutu)
        {
            return new Karar { Tur = KararTuru.YeniSatir, Metin = metin, Kutu = kutu };
        }
    }

    public sealed class GosterimDurumu
    {
        public string AnaMetin;      // null: ana kutu boş
        public Rectangle AnaKutu;
        public string Onceki;        // null: önceki kutu boş
    }

    /// <summary>
    /// Okunan metin akışından "yeni satır / aynı satır / bitti" kararı ve gösterim zamanlaması.
    /// Zaman enjekte edilir (milisaniye); gerçek zamanlayıcı yoktur.
    /// </summary>
    public sealed class AltyaziDurumu
    {
        public const double AyniSatirEsigi = 0.80;
        /// <summary>Harf bazlı aynı satır eşiği: gürültülü okumalar (≈0,84+) aynı, benzer ama farklı cümleler
        /// ("Did they get you?" / "Did they hurt you?" ≈0,79) ayrı kalır.</summary>
        public const double AyniSatirHarfEsigi = 0.80;
        /// <summary>Aynı satırın yeni okuması bu kat (ve en az 4 harf) uzunsa yarım okumanın tamamı sayılır, bir kez yeniden çevrilir.</summary>
        public const double IyilesmeKati = 1.15;
        /// <summary>Okuma, bu süre içinde görülen okumalardan birine benziyorsa o okumanın satırına bağlanır.</summary>
        public const double YakinOkumaMs = 4000;
        public const double YerelBellekEsigi = 0.85;
        public const int YerelBellekEnFazla = 2000;
        public const double TutmaMs = 1200, YerTutucuMs = 10000, GecCeviriMs = 5000,
                            GecAnaMs = 2200, OncekiMs = 4000, OncekiSayilirMs = 6000;
        public const string YerTutucu = "…";

        private static readonly Regex IkiHarf = new Regex("[A-Za-z]{2}", RegexOptions.Compiled);

        private sealed class Satir { public string En; public double Zaman; public Rectangle Kutu; public bool Iyilesti; public string Tr; public int TrHarf; }
        private sealed class Okuma { public string Metin; public double Zaman; public Satir Satir; }
        private readonly List<Okuma> _okumalar = new List<Okuma>();   // son YakinOkumaMs içindeki okumalar

        private readonly Func<double> _simdi;
        private readonly List<Satir> _son = new List<Satir>();   // son satırlar (en eski başta)
        private Satir _gecerli;                                   // şu an ekranda olan altyazı satırı (yoksa null)

        private string _ana;            // null değilse ana kutudaki metin
        private bool _anaTurkce;
        private Rectangle _anaKutu;
        private double _yerTutucuBitis; // yer tutucu bu zamandan sonra kalkar
        private double _gizleZaman;     // 0 değilse ana kutu bu zamandan sonra gizlenir (satır yokken)
        private string _onceki;
        private double _oncekiBitis;
        private string _sonTr;
        private Satir _sonTrSatir;      // son gösterilen çevirinin satırı (aynı satırın yeni çevirisi öncekiye itmez)
        private double _sonTrZaman;

        private readonly Dictionary<string, string> _bellek = new Dictionary<string, string>();
        private readonly Queue<string> _bellekSira = new Queue<string>();

        public AltyaziDurumu(Func<double> simdiMs) { _simdi = simdiMs; }

        /// <summary>Geçerli satırdan ÖNCEKİ en çok 5 İngilizce satır (en eskisi başta; geçerli satır yok).</summary>
        public IReadOnlyList<string> Baglam
        {
            get
            {
                var l = new List<string>();
                int son = _gecerli != null ? _son.Count - 1 : _son.Count;
                int bas = Math.Max(0, son - 5);
                for (int i = bas; i < son; i++) l.Add(_son[i].En);
                return l;
            }
        }

        private static bool Bos(string m) { return m.Length < 3 || !IkiHarf.IsMatch(m); }

        public Karar Okundu(string metin, Rectangle kutu)
        {
            metin = (metin ?? "").Trim();
            double simdi = _simdi();

            if (Bos(metin))
            {
                if (_gecerli == null) return Karar.Yok;
                _gecerli = null;
                if (_anaTurkce) _gizleZaman = simdi + TutmaMs;
                else _ana = null;   // yer tutucu: hemen kalkar
                return Karar.Bitti;
            }

            _okumalar.RemoveAll(o => simdi - o.Zaman > YakinOkumaMs);
            if (_gecerli == null)
            {
                // Altyazı kısa süre kaybolup geri geldi (ya da gürültü yüzünden boş okundu): aynı satır yeniden
                // çevrilmez; bilinen Türkçesi yeniden gösterilir.
                var yakin = _okumalar.LastOrDefault(o => AyniOkuma(metin, o.Metin));
                if (yakin != null)
                {
                    _gecerli = yakin.Satir;
                    _okumalar.Add(new Okuma { Metin = metin, Zaman = simdi, Satir = yakin.Satir });
                    if (yakin.Satir.Tr != null) AnaGoster(yakin.Satir.Tr, yakin.Satir.Kutu, simdi, yakin.Satir);
                    return Karar.Yok;
                }
            }
            if (_gecerli != null)
            {
                // Gürültülü okuma ya da kesik (eksik) okuma aynı satırdır.
                bool ayni = Benzerlik.HarfOrani(metin, _gecerli.En) >= AyniSatirHarfEsigi || KismiOkuma(metin, _gecerli.En);
                // Yarım okunan satırın tamamı (yeni okuma belirgin uzun ve eskisini içeriyor) bir kez yeniden çevrilir;
                // gösterim değişmez.
                if ((ayni || KismiOkuma(_gecerli.En, metin)) && !_gecerli.Iyilesti && Iyilesme(metin, _gecerli.En))
                {
                    _gecerli.Iyilesti = true;
                    _gecerli.En = metin;
                    return Karar.YeniSatir(metin, kutu);
                }
                if (ayni) { _okumalar.Add(new Okuma { Metin = metin, Zaman = simdi, Satir = _gecerli }); return Karar.Yok; }
                // Geçerli satıra değil ama son okumalardan birine (aynı satırın başka bir gürültülü okumasına) benziyor.
                var zincir = _okumalar.LastOrDefault(o => o.Satir == _gecerli && AyniOkuma(metin, o.Metin));
                if (zincir != null) { _okumalar.Add(new Okuma { Metin = metin, Zaman = simdi, Satir = _gecerli }); return Karar.Yok; }
            }

            var s = new Satir { En = metin, Zaman = simdi, Kutu = kutu };
            _gecerli = s;
            _okumalar.Add(new Okuma { Metin = metin, Zaman = simdi, Satir = s });
            _son.Add(s);
            if (_son.Count > 20) _son.RemoveAt(0);
            _ana = YerTutucu; _anaTurkce = false; _anaKutu = kutu;
            _yerTutucuBitis = simdi + YerTutucuMs; _gizleZaman = 0;
            return Karar.YeniSatir(metin, kutu);
        }

        /// <summary>Bir çeviri geldi (sunucudan ya da yerel bellekten). "###" ile başlayan: okunamadı.</summary>
        public void CeviriGeldi(string en, string tr)
        {
            double simdi = _simdi();
            tr = (tr ?? "").Trim();
            bool okunamaz = tr.StartsWith("###", StringComparison.Ordinal);
            // Çevirinin satırı son okumalardan bulunur (satırın okuması o arada değişmiş olabilir: gürültü, iyileşme).
            var okuma = _okumalar.LastOrDefault(o => o.Metin == en) ?? _okumalar.LastOrDefault(o => AyniOkuma(en, o.Metin));
            Satir satir = okuma?.Satir;
            bool gecerliSatir = _gecerli != null && (satir == _gecerli || (satir == null && AyniOkuma(en, _gecerli.En)));
            int harf = Benzerlik.Harfler(en).Length;
            // Yarım okumanın çevirisi, tamamınınkinden sonra gelirse yok sayılır.
            if (gecerliSatir && !okunamaz && _gecerli.Tr != null && harf < _gecerli.TrHarf) return;

            if (gecerliSatir)
            {
                if (okunamaz)
                {
                    // Satırın zaten bir çevirisi gösteriliyorsa (iyileştirme denemesi okunamadıysa) o kalır.
                    if (!(_anaTurkce && _sonTrSatir == _gecerli)) { _ana = null; _anaTurkce = false; }
                    return;
                }
                _gecerli.Tr = tr;
                _gecerli.TrHarf = harf;
                AnaGoster(tr, _gecerli.Kutu, simdi, _gecerli);
                return;
            }
            if (okunamaz || tr.Length == 0) return;

            // Geç gelen: satır artık değişti ya da bitti. Gecikme, satırın ilk görüldüğü andan ölçülür.
            Satir kaynak = satir;
            for (int i = _son.Count - 1; kaynak == null && i >= 0; i--)
                if (Benzerlik.HarfOrani(en, _son[i].En) >= AyniSatirHarfEsigi) { kaynak = _son[i]; break; }
            if (kaynak == null || simdi - kaynak.Zaman >= GecCeviriMs) return;
            kaynak.Tr = tr;

            if (_gecerli != null) { _onceki = tr; _oncekiBitis = simdi + OncekiMs; }
            else { AnaGoster(tr, kaynak.Kutu, simdi, kaynak); _gizleZaman = simdi + GecAnaMs; }
        }

        private void AnaGoster(string tr, Rectangle kutu, double simdi, Satir satir)
        {
            // Aynı satırın yeni çevirisi (yarım okumanın tamamı) eskisinin yerine geçer; soluk "önceki"ye itilmez.
            if (_sonTr != null && _sonTr != tr && _sonTrSatir != satir && simdi - _sonTrZaman < OncekiSayilirMs)
            {
                _onceki = _sonTr; _oncekiBitis = simdi + OncekiMs;
            }
            _ana = tr; _anaTurkce = true; _anaKutu = kutu;
            _sonTr = tr; _sonTrSatir = satir; _sonTrZaman = simdi; _gizleZaman = 0;
        }

        /// <summary>Eski okumanın harfleri, yenisine (çoğunlukla) yalnız eklemeyle ulaşılıyor mu?</summary>
        private static bool KismiOkuma(string eski, string yeni)
        {
            string e = Benzerlik.Harfler(eski), y = Benzerlik.Harfler(yeni);
            if (e.Length == 0 || y.Length <= e.Length) return false;
            int uzaklik = (int)Math.Round((1 - Benzerlik.Oran(e, y)) * y.Length);
            return uzaklik - (y.Length - e.Length) <= Math.Max(2, (int)(0.15 * e.Length));
        }

        /// <summary>OCR satırları ekrandaki sırayla: yukarıdan aşağı (aynı satır yüksekliğindekiler bir satır), soldan sağa.</summary>
        public static List<OcrSatiri> Sirala(IReadOnlyList<OcrSatiri> satirlar)
        {
            var liste = satirlar.Where(x => x != null).ToList();
            if (liste.Count < 2) return liste;
            double yukseklik = Math.Max(1, liste.Average(x => (double)x.Kutu.Height));
            var sonuc = new List<OcrSatiri>();
            var kalan = liste.OrderBy(x => x.Kutu.Y + x.Kutu.Height / 2.0).ToList();
            while (kalan.Count > 0)
            {
                double orta = kalan[0].Kutu.Y + kalan[0].Kutu.Height / 2.0;
                var sira = kalan.Where(x => Math.Abs(x.Kutu.Y + x.Kutu.Height / 2.0 - orta) <= yukseklik * 0.5).OrderBy(x => x.Kutu.X).ToList();
                sonuc.AddRange(sira);
                kalan.RemoveAll(sira.Contains);
            }
            return sonuc;
        }

        private static bool AyniOkuma(string a, string b)
        {
            return Benzerlik.HarfOrani(a, b) >= AyniSatirHarfEsigi || KismiOkuma(a, b) || KismiOkuma(b, a);
        }

        private static bool Iyilesme(string yeni, string eski)
        {
            int y = Benzerlik.Harfler(yeni).Length, e = Benzerlik.Harfler(eski).Length;
            return y >= e * IyilesmeKati && y >= e + 4;
        }

        public GosterimDurumu Simdi()
        {
            double simdi = _simdi();
            if (_ana == YerTutucu && !_anaTurkce && simdi > _yerTutucuBitis) _ana = null;
            if (_gizleZaman > 0 && simdi > _gizleZaman && _gecerli == null) { _ana = null; _anaTurkce = false; _gizleZaman = 0; }
            if (_onceki != null && simdi > _oncekiBitis) _onceki = null;
            return new GosterimDurumu { AnaMetin = _ana, AnaKutu = _anaKutu, Onceki = _onceki };
        }

        public string YerelBellekBul(string en)
        {
            if (en == null) return null;
            string dogrudan;
            if (_bellek.TryGetValue(en, out dogrudan)) return dogrudan;
            string bulunan = null; double enOran = 0;
            foreach (var k in _bellek)
            {
                double o = Benzerlik.Oran(k.Key, en);
                if (o >= YerelBellekEsigi && o > enOran) { enOran = o; bulunan = k.Value; }
            }
            return bulunan;
        }

        public void YerelBellekYaz(string en, string tr)
        {
            if (en == null || tr == null) return;
            if (!_bellek.ContainsKey(en))
            {
                _bellekSira.Enqueue(en);
                while (_bellekSira.Count > YerelBellekEnFazla) _bellek.Remove(_bellekSira.Dequeue());
            }
            _bellek[en] = tr;
        }
    }
}
