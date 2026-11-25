using System.Linq;

namespace Canli.Mantik
{
    /// <summary>
    /// OCR okuması sunucuya gönderilecek kadar okunur mu? Yalnız açıkça çöp okumalar durdurulur; tek tük bozuk
    /// karakterli ama çoğu okunur satırlar gönderilir (model OCR hatalarını anlamdan düzeltir). Kelime bazlı karar:
    /// kelimelerin en az %60'ı düzgün olmalı; "�" harflerin %5'inden fazlaysa okuma çöptür.
    /// </summary>
    public static class MetinKalitesi
    {
        private const string OlaganNoktalama = ".,!?'\"-:;()…’‘“”";
        private const string Unluler = "aeiouyAEIOUYıİöÖüÜ";
        private const double DuzgunOrani = 0.6;
        private const double EnCokBozukKarakter = 0.05;

        public static bool Yeterli(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return false;
            int harf = metin.Count(char.IsLetter);
            if (harf < 2) return false;
            if ((double)metin.Count(c => c == '�') / harf > EnCokBozukKarakter) return false;

            int toplam = 0, duzgun = 0;
            foreach (var parca in metin.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string k = parca.Trim(OlaganNoktalama.ToCharArray());
                if (k.Length < 2) continue;
                toplam++;
                if (Duzgun(k)) duzgun++;
            }
            // Kısa ünlemler ("Hmm...", "Shh!") her zaman gönderilir.
            if (toplam <= 2) return true;
            return (double)duzgun / toplam >= DuzgunOrani;
        }

        /// <summary>Sayı ya da (harf, kelime içi tire/kesme, OCR'ın o/l yerine okuduğu 0/1) en az bir ünlü içeren kelime.</summary>
        private static bool Duzgun(string k)
        {
            if (k.All(char.IsDigit)) return true;
            bool harfVar = false, unluVar = false;
            foreach (char c in k)
            {
                if (char.IsLetter(c)) { harfVar = true; if (Unluler.IndexOf(c) >= 0) unluVar = true; continue; }
                if (c == '-' || c == '\'' || c == '’' || c == '0' || c == '1') continue;
                return false;
            }
            return harfVar && unluVar;
        }
    }
}
