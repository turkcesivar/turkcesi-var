using System.Linq;
using System.Text.RegularExpressions;

namespace Canli.Mantik
{
    /// <summary>
    /// OCR okuması sunucuya gönderilecek kadar temiz mi? Altyazı belirirken/kaybolurken gelen bozuk okumalar
    /// (Metro Exodus tanı kaydında satırların yarısından fazlası) gönderilmez; bir sonraki temiz okuma beklenir.
    /// </summary>
    public static class MetinKalitesi
    {
        private const string TuhafIsaretler = "*$%&^|\\~<>{}[]@#_=+";
        private const string OlaganNoktalama = ".,!?'\"-:;()…’‘“”";
        // 0 ve 1 harfe bitişik olabilir (OCR o/l karışıklığı: "Enz0", "t0"); diğer rakamlar bozulmadır ("ms4JC").
        private static readonly Regex HarfRakamKarisik = new Regex(@"\p{L}[2-9]|[2-9]\p{L}", RegexOptions.Compiled);

        public static bool Yeterli(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return false;
            if (metin.IndexOf('�') >= 0) return false;
            if (metin.Any(c => TuhafIsaretler.IndexOf(c) >= 0)) return false;
            // "ms4JC" gibi harf-rakam karışık kelimeler OCR bozulmasıdır.
            if (HarfRakamKarisik.IsMatch(metin)) return false;
            // Olağan noktalama ("NO! A-a-a-a! Artyom!!!") orana katılmaz; geriye kalan tuhaf karakterler sayılır.
            var dolu = metin.Where(c => !char.IsWhiteSpace(c) && OlaganNoktalama.IndexOf(c) < 0).ToList();
            int harf = dolu.Count(char.IsLetter), rakam = dolu.Count(char.IsDigit);
            if (harf < 2) return false;
            return (double)(harf + rakam) / dolu.Count >= 0.7;
        }
    }
}
