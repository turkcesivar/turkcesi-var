using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace Canli
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            SetProcessDPIAware();
            ICikis cikis = new KonsolCikis();
            cikis.Yaz(Protokol.Olay("hazir"));

            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
            {
                try
                {
                    Komut k = Protokol.KomutSatirindan(args);
                    Isle(k, cikis);
                }
                catch (Exception e) when (e is ArgumentException || e is FormatException)
                {
                    cikis.Yaz(Protokol.Olay("hata", ("kod", Protokol.GecersizKomut)));
                    return 1;
                }
            }

            // stdin komutları ayrı iş parçacığında okunur; "cik" ya da stdin kapanınca çıkılır.
            var bitti = new ManualResetEventSlim(false);
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
                            cikis.Yaz(Protokol.Olay("hata", ("kod", hata)));
                            continue;
                        }
                        if (komut.Tur == "cik") break;
                        Isle(komut, cikis);
                    }
                }
                finally
                {
                    bitti.Set();
                }
            });
            okuyucu.IsBackground = true;
            okuyucu.Start();
            bitti.Wait();
            return 0;
        }

        // Çeviri motoru sonraki görevlerde bağlanır; iskelette komutlar yalnız kabul edilir.
        private static void Isle(Komut komut, ICikis cikis)
        {
        }
    }
}
