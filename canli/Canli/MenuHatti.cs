using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Canli.Goruntu;
using Canli.Kaplama;
using Canli.Mantik;

namespace Canli
{
    /// <summary>
    /// Menü (tam ekran, F7 ya da otomatik) ve ek bölge (ör. sol üst görevler) okuma ve çevirisi
    /// (canli.ps1 EkraniCevir / HudOku / GonderMenu). UI iş parçacığından çağrılır; await'ler UI'ya döner.
    /// </summary>
    internal sealed class MenuHatti
    {
        public const double EkBellekEsigi = 0.85;
        private const int Esik = 185;

        private static readonly Regex IkiHarf = new Regex("[A-Za-z]{2}", RegexOptions.Compiled);
        private static readonly Regex UcHarf = new Regex("[A-Za-z]{3}", RegexOptions.Compiled);

        private readonly string _oyun, _oyunAdi;
        private readonly SunucuIstemcisi _sunucu;
        private readonly IEkranOkuyucu _ekranOkuyucu;
        private readonly IKaplama _kaplama;
        private readonly MenuAlgilayici _algilayici;
        private readonly Action<string> _hata;     // sunucu hata kodu → çalıştırıcı
        private readonly Func<bool> _durdu;
        private readonly ITaniKaydi _tani;
        private readonly Func<double> _saat;

        // Süreç içi menü çeviri belleği (tam metin → çeviri; "" = çevrilmez). Yeniden açılan ekran anında dolar.
        private readonly Dictionary<string, string> _bellek = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _istekte = new HashSet<string>(StringComparer.Ordinal);
        private List<MenuKutusu> _menuKutular = new List<MenuKutusu>();
        private List<MenuKutusu>[] _ekKutular = new List<MenuKutusu>[0];
        private long[] _ekImza = new long[0];
        private int _nesil;

        public MenuHatti(string oyun, string oyunAdi, SunucuIstemcisi sunucu, IEkranOkuyucu ekranOkuyucu, IKaplama kaplama,
                         MenuAlgilayici algilayici, Action<string> hata, Func<bool> durdu,
                         ITaniKaydi tani = null, Func<double> saatMs = null)
        {
            _oyun = oyun; _oyunAdi = oyunAdi; _sunucu = sunucu; _ekranOkuyucu = ekranOkuyucu;
            _kaplama = kaplama; _algilayici = algilayici; _hata = hata; _durdu = durdu;
            _tani = tani ?? BosTani.Ornek;
            _saat = saatMs ?? (() => 0);
        }

        // decimal: JSON'da 12.3 olarak yazılır (double net48'de 12.299999999999999 olabilir).
        private static decimal Ms(double ms) { return (decimal)Math.Round(ms, 1); }

        /// <summary>Tam ekran OCR'ı sürüyor mu.</summary>
        public bool Okunuyor { get; private set; }

        /// <summary>Menü kutularını kaldırır; süren tam ekran okumasının sonucu da atılır.</summary>
        public void MenuKapat()
        {
            _nesil++;
            // Otomatik kaldırmada algılayıcı MenuAcik'i zaten kapatmıştır; kutu varsa da kapanış sayılır.
            if (_algilayici.MenuAcik || _menuKutular.Count > 0) _tani.Yaz("menu_kapandi");
            _algilayici.MenuAcik = false;
            if (_menuKutular.Count > 0) { _menuKutular = new List<MenuKutusu>(); Yenile(); }
        }

        /// <summary>Menü ve ek bölge kutularını unutur (oyun arkaya düştü ya da ekran değişti); kaplamayı çalıştırıcı gizler.</summary>
        public void Sifirla()
        {
            _nesil++;
            _algilayici.MenuAcik = false;
            _menuKutular = new List<MenuKutusu>();
            _ekKutular = new List<MenuKutusu>[0];
            _ekImza = new long[0];
        }

        /// <summary>Ekranı çevir. Elle (F7): menü açıksa kapatır, değilse satır sayısına bakmadan okur.
        /// Otomatik: en az 5 satır gerekir. Altyazı bölgesindeki satırlar atlanır.</summary>
        public async Task EkraniCevir(Rectangle ekran, Rectangle altyaziBolgesi, bool elle)
        {
            if (elle && _algilayici.MenuAcik) { MenuKapat(); return; }
            if (Okunuyor) return;
            int nesil = _nesil;
            IReadOnlyList<OcrSatiri> satirlar;
            double t0 = _saat();
            Okunuyor = true;
            try
            {
                Bitmap tam = _ekranOkuyucu.Al(ekran);
                satirlar = await Task.Run(() => { using (tam) return _ekranOkuyucu.Oku(tam); });
            }
            catch (Exception) { return; }
            finally { Okunuyor = false; }
            if (_durdu() || nesil != _nesil) return;

            int bolgeUst = altyaziBolgesi.Y - ekran.Y, bolgeAlt = bolgeUst + altyaziBolgesi.Height;
            var secilen = satirlar
                .Where(s => IkiHarf.IsMatch(s.Metin) && !(s.Kutu.Y >= bolgeUst && s.Kutu.Bottom <= bolgeAlt))
                .ToList();
            double ocrMs = _saat() - t0;
            if (!elle && !_algilayici.YeterliSatir(secilen.Count))
            {
                _tani.Yaz("menu_atlandi", ("satir", secilen.Count), ("ocrMs", Ms(ocrMs)));
                return;
            }

            var eksik = new List<string>();
            _menuKutular = new List<MenuKutusu>();
            foreach (var s in secilen)
            {
                _bellek.TryGetValue(s.Metin, out string tr);
                _menuKutular.Add(new MenuKutusu { En = s.Metin, Kutu = s.Kutu, Tr = tr });
                if (tr == null && !_istekte.Contains(s.Metin) && !eksik.Contains(s.Metin)) eksik.Add(s.Metin);
            }
            _algilayici.MenuAcik = true;
            Yenile();
            int bellekten = _menuKutular.Count(m => m.Tr != null);
            _tani.Yaz("menu_okundu", ("elle", elle), ("satir", secilen.Count), ("bellekten", bellekten),
                      ("eksik", eksik.Count), ("ocrMs", Ms(ocrMs)), ("ilkMs", bellekten > 0 ? Ms(ocrMs) : (decimal?)null));
            if (eksik.Count > 0) await IsteAsync(eksik, "menu", t0);
        }

        /// <summary>Ek bölgeleri okur (saniyede bir; menü açıkken çağrılmaz). Görüntü değişmeyen bölge atlanır.</summary>
        public void EkBolgeleriOku(Rectangle ekran, double[][] bolgeler)
        {
            if (bolgeler == null || bolgeler.Length == 0) return;
            if (_ekImza.Length != bolgeler.Length)
            {
                _ekImza = new long[bolgeler.Length];
                _ekKutular = new List<MenuKutusu>[bolgeler.Length];
            }
            bool degisti = false;
            var eksik = new List<string>();
            for (int i = 0; i < bolgeler.Length; i++)
            {
                if (bolgeler[i] == null || bolgeler[i].Length != 4) continue;
                Rectangle r = EkranSecici.OranlariPiksele(ekran, bolgeler[i]);
                if (r.Width <= 0 || r.Height <= 0) continue;
                IReadOnlyList<OcrSatiri> satirlar;
                using (var ham = _ekranOkuyucu.Al(r))
                using (var temiz = RenkSuzgeci.Temizle(ham, RenkKipi.Ikisi, Esik, out long imza, out bool _))
                {
                    if (imza == _ekImza[i]) continue;
                    _ekImza[i] = imza;
                    double t0 = _saat();
                    satirlar = _ekranOkuyucu.Oku(temiz);
                    _tani.Yaz("ek_okundu", ("bolge", i), ("satir", satirlar.Count), ("ocrMs", Ms(_saat() - t0)));
                }
                degisti = true;
                var liste = new List<MenuKutusu>();
                foreach (var s in satirlar)
                {
                    if (s.Metin.Length < 6 || !UcHarf.IsMatch(s.Metin)) continue;
                    var kutu = new Rectangle(s.Kutu.X + r.X - ekran.X, s.Kutu.Y + r.Y - ekran.Y, s.Kutu.Width, s.Kutu.Height);
                    string tr = BellektenBul(s.Metin);
                    liste.Add(new MenuKutusu { En = s.Metin, Kutu = kutu, Tr = tr });
                    if (tr == null && !Yolda(s.Metin) && !eksik.Contains(s.Metin)) eksik.Add(s.Metin);
                }
                _ekKutular[i] = liste;
            }
            if (degisti) Yenile();
            if (eksik.Count > 0) { _ = IsteAsync(eksik, "ek", _saat()); }
        }

        private async Task IsteAsync(List<string> ogeler, string tur, double t0)
        {
            foreach (var o in ogeler) _istekte.Add(o);
            MenuSonucu s;
            double istekBasi = _saat();
            try { s = await _sunucu.MenuAsync(_oyun, _oyunAdi, ogeler); }
            finally { foreach (var o in ogeler) _istekte.Remove(o); }
            double simdi = _saat();
            _tani.Yaz("menu_ceviri", ("tur", tur), ("oge", ogeler.Count), ("cevrilen", s.Ceviriler.Count(c => c != null)),
                      ("istekMs", Ms(simdi - istekBasi)), ("toplamMs", Ms(simdi - t0)), ("hata", s.HataKodu), ("neden", s.Neden));
            if (_durdu()) return;

            for (int i = 0; i < ogeler.Count && i < s.Ceviriler.Length; i++)
            {
                if (s.Ceviriler[i] != null) _bellek[ogeler[i]] = s.Ceviriler[i];
                else if (s.HataKodu == null) _bellek[ogeler[i]] = "";   // sunucu çevirmedi: bir daha sorma
            }
            if (s.HataKodu != null) _hata(s.HataKodu);
            if (_durdu()) return;

            foreach (var m in TumKutular())
                if (m.Tr == null && _bellek.TryGetValue(m.En, out string tr)) m.Tr = tr;
            Yenile();
        }

        private string BellektenBul(string en)
        {
            if (_bellek.TryGetValue(en, out string tr)) return tr;
            foreach (var k in _bellek)
                if (Benzerlik.Oran(k.Key, en) >= EkBellekEsigi) return k.Value;
            return null;
        }

        private bool Yolda(string en)
        {
            foreach (var k in _istekte)
                if (Benzerlik.Oran(k, en) >= EkBellekEsigi) return true;
            return false;
        }

        private IEnumerable<MenuKutusu> TumKutular()
        {
            foreach (var m in _menuKutular) yield return m;
            foreach (var l in _ekKutular)
                if (l != null) foreach (var m in l) yield return m;
        }

        private void Yenile()
        {
            if (_durdu()) return;
            _kaplama.MenuGoster(TumKutular().ToList());
        }
    }
}
