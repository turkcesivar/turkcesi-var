using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Canli.Goruntu;
using Canli.Kaplama;
using Canli.Mantik;

namespace Canli
{
    /// <summary>
    /// Ana döngü: oyun öndeyken altyazı bölgesini yakalar, okur, sunucuya çevirtir ve kaplamalara çizer;
    /// menüleri (otomatik ya da F7) ve ek bölgeleri MenuHatti'na bırakır. Tüm çağrılar UI iş parçacığındandır.
    /// </summary>
    public sealed class Calistirici : IDisposable
    {
        public const int VkF7 = 0x76, VkF9 = 0x78;
        public const int RenkEsigi = 185;
        public const double KalanAraligiMs = 1000, UyariAraligiMs = 30000;
        public const string DuraklatildiMetni = "[duraklatıldı]";

        private static readonly HashSet<string> UyariKodlari = new HashSet<string>
            { "hak_bitti", "gun_bitti", "butce", "hiz", "gecici_hata", "cevrimdisi", "siyah_ekran" };
        private static readonly HashSet<string> LisansKodlari = new HashSet<string>
            { "lisans_yok", "cihaz_dustu", "suresi_bitti", "paketli", "yasakli" };

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int tus);

        private readonly Komut _komut;
        private readonly ICikis _cikis;
        private readonly SunucuIstemcisi _sunucu;
        private readonly IEkranOkuyucu _ekranOkuyucu;
        private readonly OnPlanBekcisi _bekci;
        private readonly IKaplama _kaplama;
        private readonly IZamanlayicilar _zaman;
        private readonly IEkranKaynagi _ekranlar;
        private readonly Func<double> _saat;
        private readonly Func<int, bool> _tusBasili;
        private readonly ITaniKaydi _tani;

        private readonly AltyaziDurumu _durum;
        private readonly MenuAlgilayici _menu;
        private readonly MenuHatti _menuHatti;
        private readonly SiyahKareSayaci _siyah = new SiyahKareSayaci();
        private readonly Dictionary<int, bool> _tuslar = new Dictionary<int, bool>();
        private readonly Dictionary<string, double> _uyariZamani = new Dictionary<string, double>();

        private Profil _profil;
        private RenkKipi _kip;
        private bool _basladi, _duraklat, _arkada;
        private Rectangle _ekran, _bolge;
        private long _sonImza;
        private byte[] _ekranImza;
        private double _sonKalanZamani = double.NegativeInfinity;
        private int? _bekleyenKalan;

        // Kaplamaya en son verilen (aynısı yeniden çizilmez). _cizildi false: bir sonraki yoklamada yeniden çiz.
        private bool _cizildi;
        private string _cizilenAna, _cizilenOnceki;
        private Rectangle _cizilenKutu;

        public Calistirici(Komut komut, ICikis cikis, SunucuIstemcisi sunucu, Okuyucu okuyucu, ITaniKaydi tani = null, Func<double> saatMs = null)
            : this(komut, cikis, sunucu, new GercekEkranOkuyucu(okuyucu), new OnPlanBekcisi(komut.SurecAdi),
                   new Kaplamalar(), new WinFormsZamanlayicilar(), new GercekEkranKaynagi(), saatMs ?? GercekSaat(), GercekTus, tani)
        {
        }

        public Calistirici(Komut komut, ICikis cikis, SunucuIstemcisi sunucu, IEkranOkuyucu ekranOkuyucu, OnPlanBekcisi bekci,
                           IKaplama kaplama, IZamanlayicilar zaman, IEkranKaynagi ekranlar, Func<double> saatMs, Func<int, bool> tusBasili,
                           ITaniKaydi tani = null)
        {
            _tani = tani ?? BosTani.Ornek;
            _komut = komut; _cikis = cikis; _sunucu = sunucu; _ekranOkuyucu = ekranOkuyucu; _bekci = bekci;
            _kaplama = kaplama; _zaman = zaman; _ekranlar = ekranlar; _saat = saatMs; _tusBasili = tusBasili;
            _durum = new AltyaziDurumu(saatMs);
            _menu = new MenuAlgilayici(saatMs);
            _menuHatti = new MenuHatti(komut.Oyun, komut.OyunAdi, sunucu, ekranOkuyucu, kaplama, _menu, HataIsle, () => Durdu,
                                       _tani, saatMs);
            _profil = komut.Profil ?? Profil.Varsayilan;
        }

        public bool Durdu { get; private set; }

        /// <summary>/oyun sorulur: paketli/yasakli ya da lisans hatasında başlamaz; yoksa "basladi" ve zamanlayıcılar.</summary>
        public async Task Basla()
        {
            if (_basladi || Durdu) return;
            _basladi = true;
            // /oyun lisans sormaz; lisans açılışta /durum ile denetlenir (ikisi birlikte istenir).
            Task<LisansDurumu> lisansGorevi = _sunucu.DurumAsync();
            Task<OyunBilgisi> oyunGorevi = _sunucu.OyunAsync(_komut.Oyun);
            LisansDurumu lisans = await lisansGorevi;
            OyunBilgisi bilgi = await oyunGorevi;
            if (Durdu) return;

            if (lisans.HataKodu != null) HataIsle(lisans.HataKodu);
            else if (lisans.Durum == "suresi_bitti") HataIsle("suresi_bitti");
            else if (lisans.Durum == "lisanssiz") HataIsle("lisans_yok");
            else if (lisans.Durum == "hak_bitti") Uyari("hak_bitti");
            if (Durdu) return;
            if (lisans.Kalan.HasValue) _bekleyenKalan = lisans.Kalan.Value;

            if (bilgi.HataKodu != null)
            {
                HataIsle(bilgi.HataKodu);
                if (Durdu) return;
            }
            else if (bilgi.Durum == "paketli" || bilgi.Durum == "yasakli")
            {
                HataIsle(bilgi.Durum);
                return;
            }
            _profil = ProfilSec(_komut, bilgi.Profil);
            _kip = _profil.Renk == "beyaz" ? RenkKipi.Beyaz : _profil.Renk == "sari" ? RenkKipi.Sari : RenkKipi.Ikisi;
            _tani.Yaz("basladi", ("oyun", _komut.Oyun), ("renk", _profil.Renk), ("ekBolge", _profil.EkBolgeler?.Length ?? 0),
                      ("lisans", lisans.Durum), ("kalan", lisans.Kalan));
            _cikis.Yaz(Protokol.Olay("basladi"));
            _zaman.Baslat(YakalamaTik, YoklamaTik, EkBolgeTik);
        }

        public void Duraklat()
        {
            if (Durdu || _duraklat) return;
            _duraklat = true;
            _tani.Yaz("duraklat");
            _menuHatti.Sifirla();
            _kaplama.GizleHepsi();
            DuraklatildiGoster();
            _cizildi = false;
        }

        // Duraklatma işareti yalnız oyun öndeyken ve ekran bilinirken görünür.
        private void DuraklatildiGoster()
        {
            if (!_arkada && !_ekran.IsEmpty) _kaplama.AnaGoster(DuraklatildiMetni, Rectangle.Empty);
        }

        public void Devam()
        {
            if (Durdu || !_duraklat) return;
            _duraklat = false;
            _tani.Yaz("devam");
            _kaplama.AnaGoster(null, Rectangle.Empty);
            _sonImza = 0;
            _ekranImza = null;
            _cizildi = false;
        }

        /// <summary>Zamanlayıcıları durdurur, kaplamaları gizler, bir kez "durdu" yazar.</summary>
        public void Dur(string neden)
        {
            if (Durdu) return;
            Durdu = true;
            _zaman.Durdur();
            _kaplama.GizleHepsi();
            _tani.Yaz("durdu", ("neden", neden));
            _cikis.Yaz(Protokol.Olay("durdu", ("neden", neden)));
        }

        public void Dispose()
        {
            Durdu = true;   // süren Basla/çeviri devam etmesin (olay yazılmaz)
            _zaman.Dispose();
            _kaplama.Dispose();
        }

        /// <summary>Komutta profil verildiyse o geçerlidir; yoksa sunucunun profili (eksik alanlar varsayılandan).</summary>
        public static Profil ProfilSec(Komut komut, Profil sunucu)
        {
            if (komut.ProfilVerildi || sunucu == null) return komut.Profil ?? Profil.Varsayilan;
            var v = Profil.Varsayilan;
            return new Profil
            {
                AltyaziBolgesi = sunucu.AltyaziBolgesi != null && sunucu.AltyaziBolgesi.Length == 4 ? sunucu.AltyaziBolgesi : v.AltyaziBolgesi,
                Renk = sunucu.Renk == "beyaz" || sunucu.Renk == "sari" || sunucu.Renk == "ikisi" ? sunucu.Renk : v.Renk,
                EkBolgeler = sunucu.EkBolgeler != null ? sunucu.EkBolgeler.Where(b => b != null && b.Length == 4).ToArray() : v.EkBolgeler
            };
        }

        // ---- Zamanlayıcı gövdeleri ----

        /// <summary>200 ms: ön plan, kısayollar, otomatik menü, altyazı yakalama + OCR.</summary>
        public void YakalamaTik()
        {
            if (Durdu || !_basladi) return;
            try
            {
                if (!_bekci.OyunOnde(out IntPtr pencere)) { ArkayaDustu(); return; }
                bool geriGeldi = _arkada;
                _arkada = false;
                bool ekranDegisti = EkranGuncelle(pencere);
                if ((geriGeldi || ekranDegisti) && _duraklat) DuraklatildiGoster();

                if (TusBasildi(VkF9)) { if (_duraklat) Devam(); else Duraklat(); }
                if (_duraklat) return;
                if (TusBasildi(VkF7)) { _ = _menuHatti.EkraniCevir(_ekran, _bolge, true); return; }

                byte[] imza = _ekranOkuyucu.KucukImza(_ekran);
                double oran = Yakalayici.Degisim(imza, _ekranImza);
                _ekranImza = imza;
                var karar = _menu.Imza(oran);
                if (karar == MenuAlgilayici.Karar.Kaldir || (oran > MenuAlgilayici.KaldirmaEsigi && _menuHatti.Okunuyor))
                    _menuHatti.MenuKapat();
                else if (karar == MenuAlgilayici.Karar.Oku)
                { _ = _menuHatti.EkraniCevir(_ekran, _bolge, false); }

                // Tam ekran menü OCR'ı sürerken altyazı OCR'ı atlanır (Okuyucu kilidinde UI beklemesin).
                if (!_menuHatti.Okunuyor) AltyaziOku();
            }
            catch (Exception)
            {
                // Yakalama geçici olarak olanaksız olabilir (kilit ekranı, UAC); bir sonraki tıkta yeniden denenir.
            }
        }

        /// <summary>30 ms: gösterim durumunu kaplamalara yansıtır, bekleyen "kalan"ı yazar.</summary>
        public void YoklamaTik()
        {
            if (Durdu) return;
            KalanBosalt();
            if (_arkada || _duraklat || !_basladi) return;
            try
            {
                var g = _durum.Simdi();
                bool anaDegisti = !_cizildi || g.AnaMetin != _cizilenAna || g.AnaKutu != _cizilenKutu;
                if (anaDegisti) _kaplama.AnaGoster(g.AnaMetin, g.AnaKutu);
                if (anaDegisti || g.Onceki != _cizilenOnceki) _kaplama.OncekiGoster(g.Onceki);
                _cizilenAna = g.AnaMetin; _cizilenKutu = g.AnaKutu; _cizilenOnceki = g.Onceki; _cizildi = true;
            }
            catch (Exception) { }
        }

        /// <summary>1 sn: ek bölgeler (menü açıkken okunmaz).</summary>
        public void EkBolgeTik()
        {
            if (Durdu || !_basladi || _duraklat || _menu.MenuAcik || _menuHatti.Okunuyor) return;
            if (_profil.EkBolgeler == null || _profil.EkBolgeler.Length == 0) return;
            try
            {
                // Ön plan her okumadan hemen önce yeniden sorulur: odak az önce başka uygulamaya geçmiş olabilir.
                if (!_bekci.OyunOnde(out _)) { ArkayaDustu(); return; }
                if (_arkada || _ekran.IsEmpty) return;
                _menuHatti.EkBolgeleriOku(_ekran, _profil.EkBolgeler);
            }
            catch (Exception) { }
        }

        // ---- Altyazı ----

        private void AltyaziOku()
        {
            IReadOnlyList<OcrSatiri> satirlar;
            double t0 = _saat();
            using (var ham = _ekranOkuyucu.Al(_bolge))
            using (var temiz = RenkSuzgeci.Temizle(ham, _kip, RenkEsigi, out long imza, out bool siyah))
            {
                if (_siyah.Ekle(siyah)) Uyari("siyah_ekran");
                if (imza == _sonImza) return;   // görüntü değişmedi
                _sonImza = imza;
                satirlar = _ekranOkuyucu.Oku(temiz);
            }
            double ocrMs = _saat() - t0;
            string metin = Regex.Replace(string.Join(" ", satirlar.Select(s => s.Metin)), @"\s+", " ").Trim();
            var karar = _durum.Okundu(metin, Birlesim(satirlar));
            if (karar.Tur != KararTuru.YeniSatir) return;

            string bellekte = _durum.YerelBellekBul(karar.Metin);
            if (bellekte != null)
            {
                _durum.CeviriGeldi(karar.Metin, bellekte);
                _tani.Yaz("altyazi", ("en", karar.Metin), ("ocrMs", Ms(ocrMs)), ("istekMs", null),
                          ("toplamMs", Ms(_saat() - t0)), ("kaynak", "yerel"), ("hata", null));
            }
            else { _ = CevirAsync(karar.Metin, t0, ocrMs); }
        }

        // decimal: JSON'da 12.3 olarak yazılır (double net48'de 12.299999999999999 olabilir).
        private static decimal Ms(double ms) { return (decimal)Math.Round(ms, 1); }

        private async Task CevirAsync(string en, double t0, double ocrMs)
        {
            IReadOnlyList<string> baglam = _durum.Baglam;
            double istekBasi = _saat();
            CeviriSonucu s = await _sunucu.CeviriAsync(_komut.Oyun, _komut.OyunAdi, en, baglam);
            double simdi = _saat();
            _tani.Yaz("altyazi", ("en", en), ("ocrMs", Ms(ocrMs)), ("istekMs", Ms(simdi - istekBasi)),
                      ("toplamMs", Ms(simdi - t0)), ("kaynak", s.HataKodu != null ? null : s.Okunamaz ? "okunamaz" : s.Kaynak),
                      ("hata", s.HataKodu));
            if (Durdu) return;
            if (s.HataKodu != null)
            {
                HataIsle(s.HataKodu);
                if (!Durdu) _durum.CeviriGeldi(en, "###");   // yer tutucu hemen kalksın
                return;
            }
            string tr = s.Okunamaz ? "###" : s.Tr;
            _durum.YerelBellekYaz(en, tr);
            _durum.CeviriGeldi(en, tr);
            if (s.Kalan.HasValue) { _bekleyenKalan = s.Kalan.Value; KalanBosalt(); }
        }

        private static Rectangle Birlesim(IReadOnlyList<OcrSatiri> satirlar)
        {
            Rectangle r = Rectangle.Empty;
            foreach (var s in satirlar)
            {
                if (s.Kutu.Width <= 0 || s.Kutu.Height <= 0) continue;
                r = r.IsEmpty ? s.Kutu : Rectangle.Union(r, s.Kutu);
            }
            return r;
        }

        // ---- Durum geçişleri ----

        private void ArkayaDustu()
        {
            _tuslar.Clear();
            if (_arkada) return;
            _arkada = true;
            _tani.Yaz("arkada");
            _menuHatti.Sifirla();
            _kaplama.GizleHepsi();
            _cizildi = false;
            _ekranImza = null;
            _sonImza = 0;
        }

        private bool EkranGuncelle(IntPtr pencere)
        {
            Rectangle ekran = EkranSecici.OyunEkrani(pencere, _ekranlar);
            if (ekran == _ekran) return false;
            _ekran = ekran;
            _bolge = EkranSecici.OranlariPiksele(ekran, _profil.AltyaziBolgesi);
            _menuHatti.Sifirla();
            _kaplama.MenuGoster(null);
            _kaplama.Konumla(_ekran, _bolge);
            _ekranImza = null;
            _sonImza = 0;
            _cizildi = false;
            return true;
        }

        private bool TusBasildi(int tus)
        {
            bool bas = _tusBasili(tus);
            _tuslar.TryGetValue(tus, out bool once);
            _tuslar[tus] = bas;
            return bas && !once;
        }

        // ---- Olaylar ----

        private void HataIsle(string kod)
        {
            if (Durdu) return;
            _tani.Yaz("sunucu_hatasi", ("kod", kod));
            if (HataYonetimi.DurduranMi(kod))
            {
                _cikis.Yaz(Protokol.Olay("hata", ("kod", kod)));
                Dur(LisansKodlari.Contains(kod) ? "lisans" : "hata");
            }
            else Uyari(kod);
        }

        // Protokolde olmayan kodlar (cok_uzun, gecersiz, deneme_siniri…) olay olarak yazılmaz.
        private void Uyari(string kod)
        {
            if (!UyariKodlari.Contains(kod)) return;
            double simdi = _saat();
            if (_uyariZamani.TryGetValue(kod, out double son) && simdi - son < UyariAraligiMs) return;
            _uyariZamani[kod] = simdi;
            _cikis.Yaz(Protokol.Olay("uyari", ("kod", kod)));
        }

        private void KalanBosalt()
        {
            if (_bekleyenKalan == null) return;
            double simdi = _saat();
            if (simdi - _sonKalanZamani < KalanAraligiMs) return;
            _sonKalanZamani = simdi;
            _cikis.Yaz(Protokol.Olay("kalan", ("kalan", _bekleyenKalan.Value)));
            _bekleyenKalan = null;
        }

        private static Func<double> GercekSaat()
        {
            var sw = Stopwatch.StartNew();
            return () => sw.Elapsed.TotalMilliseconds;
        }

        private static bool GercekTus(int tus) { return (GetAsyncKeyState(tus) & 0x8000) != 0; }
    }
}
