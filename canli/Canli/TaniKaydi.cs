using System;
using System.IO;
using System.Text;

namespace Canli
{
    /// <summary>--tani ile açılan ölçüm kaydı: her olay bir JSON satırı (alanlar Protokol.Olay biçiminde).</summary>
    public interface ITaniKaydi
    {
        void Yaz(string tur, params (string anahtar, object deger)[] alanlar);
    }

    /// <summary>Tanı kapalıyken kullanılır; hiçbir şey yazmaz.</summary>
    public sealed class BosTani : ITaniKaydi
    {
        public static readonly BosTani Ornek = new BosTani();
        public void Yaz(string tur, params (string anahtar, object deger)[] alanlar) { }
    }

    /// <summary>
    /// %LOCALAPPDATA%\TurkcesiVar\canli\tani\&lt;yyyyMMdd-HHmmss&gt;.jsonl dosyasına yazar.
    /// Her satıra "t" (oturum başından ms) eklenir. Yazma hatası çeviriyi asla durdurmaz.
    /// </summary>
    public sealed class DosyaTani : ITaniKaydi, IDisposable
    {
        private readonly object _kilit = new object();
        private readonly Func<double> _saat;
        private StreamWriter _yazici;

        public string Yol { get; }

        public DosyaTani(string klasor, Func<double> saatMs)
        {
            _saat = saatMs;
            Directory.CreateDirectory(klasor);
            Yol = Path.Combine(klasor, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            _yazici = new StreamWriter(new FileStream(Yol, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
            {
                NewLine = "\n",
                AutoFlush = true
            };
        }

        public static string VarsayilanKlasor()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "TurkcesiVar", "canli", "tani");
        }

        public void Yaz(string tur, params (string anahtar, object deger)[] alanlar)
        {
            var hepsi = new (string, object)[alanlar.Length + 1];
            hepsi[0] = ("t", (decimal)Math.Round(_saat(), 1));
            Array.Copy(alanlar, 0, hepsi, 1, alanlar.Length);
            string satir = Protokol.Olay(tur, hepsi);
            lock (_kilit)
            {
                if (_yazici == null) return;
                try { _yazici.WriteLine(satir); }
                catch (IOException) { }
            }
        }

        public void Dispose()
        {
            lock (_kilit)
            {
                if (_yazici == null) return;
                try { _yazici.Dispose(); } catch (IOException) { }
                _yazici = null;
            }
        }
    }
}
