using System;
using System.Collections.Generic;
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
        public const double YerelBellekEsigi = 0.85;
        public const int YerelBellekEnFazla = 2000;
        public const double TutmaMs = 1200, YerTutucuMs = 10000, GecCeviriMs = 5000,
                            GecAnaMs = 2200, OncekiMs = 4000, OncekiSayilirMs = 6000;
        public const string YerTutucu = "…";

        private static readonly Regex IkiHarf = new Regex("[A-Za-z]{2}", RegexOptions.Compiled);

        private sealed class Satir { public string En; public double Zaman; public Rectangle Kutu; }

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

            if (_gecerli != null && Benzerlik.Oran(metin, _gecerli.En) >= AyniSatirEsigi) return Karar.Yok;

            var s = new Satir { En = metin, Zaman = simdi, Kutu = kutu };
            _gecerli = s;
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
            bool gecerliSatir = _gecerli != null && Benzerlik.Oran(en, _gecerli.En) >= AyniSatirEsigi;

            if (gecerliSatir)
            {
                if (okunamaz) { _ana = null; _anaTurkce = false; return; }
                AnaGoster(tr, _gecerli.Kutu, simdi);
                return;
            }
            if (okunamaz || tr.Length == 0) return;

            // Geç gelen: satır artık değişti ya da bitti. Gecikme, satırın ilk görüldüğü andan ölçülür.
            Satir kaynak = null;
            for (int i = _son.Count - 1; i >= 0; i--)
                if (Benzerlik.Oran(en, _son[i].En) >= AyniSatirEsigi) { kaynak = _son[i]; break; }
            if (kaynak == null || simdi - kaynak.Zaman >= GecCeviriMs) return;

            if (_gecerli != null) { _onceki = tr; _oncekiBitis = simdi + OncekiMs; }
            else { AnaGoster(tr, kaynak.Kutu, simdi); _gizleZaman = simdi + GecAnaMs; }
        }

        private void AnaGoster(string tr, Rectangle kutu, double simdi)
        {
            if (_sonTr != null && _sonTr != tr && simdi - _sonTrZaman < OncekiSayilirMs)
            {
                _onceki = _sonTr; _oncekiBitis = simdi + OncekiMs;
            }
            _ana = tr; _anaTurkce = true; _anaKutu = kutu;
            _sonTr = tr; _sonTrZaman = simdi; _gizleZaman = 0;
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
