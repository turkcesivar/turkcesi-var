using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Canli.Goruntu;

namespace Canli
{
    internal static class Program
    {
        private const string SunucuAdresi = "https://api.turkcesivar.com";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static ICikis _cikis;
        private static Okuyucu _okuyucu;
        private static string _cihazOzeti;
        private static readonly HttpClientHandler Isleyici = new HttpClientHandler();
        private static Calistirici _calistirici;

        [STAThread]
        private static int Main(string[] args)
        {
            SetProcessDPIAware();
            // UI iş parçacığında beklenmeyen bir hata pencere açmasın; stdout'a da bir şey yazılmaz.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Beklenmeyen();

            _cikis = new KonsolCikis();
            var ui = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ui);
            _cikis.Yaz(Protokol.Olay("hazir"));

            _okuyucu = Okuyucu.Olustur(out string ocrHata);
            if (_okuyucu == null) _cikis.Yaz(Protokol.Olay("hata", ("kod", ocrHata)));

            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
            {
                try
                {
                    Komut k = Protokol.KomutSatirindan(args);
                    ui.Post(_ => Isle(k), null);
                }
                catch (Exception e) when (e is ArgumentException || e is FormatException)
                {
                    _cikis.Yaz(Protokol.Olay("hata", ("kod", Protokol.GecersizKomut)));
                    return 1;
                }
            }

            // stdin komutları ayrı iş parçacığında okunur ve UI iş parçacığına aktarılır; "cik" ya da stdin kapanınca çıkılır.
            var okuyucu = new Thread(() =>
            {
                try
                {
                    var girdi = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
                    string satir;
                    while ((satir = girdi.ReadLine()) != null)
                    {
                        if (satir.Trim().Length == 0) continue;
                        if (!Protokol.KomutCoz(satir, out Komut komut, out string hata))
                        {
                            _cikis.Yaz(Protokol.Olay("hata", ("kod", hata)));
                            continue;
                        }
                        if (komut.Tur == "cik") break;
                        ui.Post(_ => Isle(komut), null);
                    }
                }
                catch (IOException) { }
                finally
                {
                    ui.Post(_ => Cik(), null);
                }
            });
            okuyucu.IsBackground = true;
            okuyucu.Start();
            Application.Run();
            return 0;
        }

        // UI iş parçacığında çalışır.
        private static void Isle(Komut komut)
        {
            switch (komut.Tur)
            {
                case "basla":
                    if (string.IsNullOrEmpty(komut.Lisans) || string.IsNullOrEmpty(komut.Oyun))
                    {
                        _cikis.Yaz(Protokol.Olay("hata", ("kod", Protokol.GecersizKomut)));
                        return;
                    }
                    Birak("komut");
                    if (_okuyucu == null)
                    {
                        _cikis.Yaz(Protokol.Olay("hata", ("kod", Okuyucu.DilYok)));
                        _cikis.Yaz(Protokol.Olay("durdu", ("neden", "hata")));
                        return;
                    }
                    if (_cihazOzeti == null) _cihazOzeti = CihazKimligi.Ozet(CihazKimligi.MakineGuidOku());
                    var sunucu = new SunucuIstemcisi(Isleyici, SunucuAdresi, komut.Lisans, _cihazOzeti);
                    _calistirici = new Calistirici(komut, _cikis, sunucu, _okuyucu);
                    _calistirici.Basla().ContinueWith(t => { var _ = t.Exception; Beklenmeyen(); }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                case "duraklat":
                    if (_calistirici != null) _calistirici.Duraklat();
                    break;
                case "devam":
                    if (_calistirici != null) _calistirici.Devam();
                    break;
                case "dur":
                    if (_calistirici != null && !_calistirici.Durdu) _calistirici.Dur("komut");
                    else _cikis.Yaz(Protokol.Olay("durdu", ("neden", "komut")));
                    break;
            }
        }

        private static bool _beklenmeyenYazildi;

        // Beklenmeyen bir hata: çalışan oturum varsa "hata beklenmeyen" + "durdu hata" (oturum başına bir kez);
        // oturum yoksa aynı iki olay süreç boyunca bir kez yazılır.
        private static void Beklenmeyen()
        {
            var c = _calistirici;
            if (c != null)
            {
                if (c.Durdu) return;
                _cikis.Yaz(Protokol.Olay("hata", ("kod", "beklenmeyen")));
                c.Dur("hata");
                return;
            }
            if (_beklenmeyenYazildi) return;
            _beklenmeyenYazildi = true;
            _cikis.Yaz(Protokol.Olay("hata", ("kod", "beklenmeyen")));
            _cikis.Yaz(Protokol.Olay("durdu", ("neden", "hata")));
        }

        // Çalışan bir oturum varsa durdurup kaynaklarını bırakır.
        private static void Birak(string neden)
        {
            if (_calistirici == null) return;
            _calistirici.Dur(neden);
            _calistirici.Dispose();
            _calistirici = null;
        }

        private static void Cik()
        {
            if (_calistirici != null) { _calistirici.Dispose(); _calistirici = null; }
            Application.ExitThread();
        }
    }
}
