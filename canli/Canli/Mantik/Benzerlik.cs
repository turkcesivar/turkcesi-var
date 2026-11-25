using System;

namespace Canli.Mantik
{
    /// <summary>İki metnin benzerlik oranı (Levenshtein, büyük/küçük harfe duyarsız). 0..1.</summary>
    public static class Benzerlik
    {
        /// <summary>
        /// Yalnız harf ve rakamlar üzerinden benzerlik (noktalama ve boşluk OCR gürültüsünde en çok bozulan kısımdır):
        /// "Did thyrget you? NO? Good." ile "Did they get you? No? Good." neredeyse aynı çıkar.
        /// </summary>
        public static double HarfOrani(string a, string b)
        {
            return Oran(Harfler(a), Harfler(b));
        }

        public static string Harfler(string s)
        {
            var sb = new System.Text.StringBuilder((s ?? "").Length);
            foreach (char c in s ?? "") if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        public static double Oran(string a, string b)
        {
            a = (a ?? "").ToLowerInvariant();
            b = (b ?? "").ToLowerInvariant();
            if (a.Length == 0 && b.Length == 0) return 1.0;
            int uzun = Math.Max(a.Length, b.Length);
            if (a.Length == 0 || b.Length == 0) return 0.0;

            var onceki = new int[b.Length + 1];
            var simdiki = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) onceki[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                simdiki[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int maliyet = a[i - 1] == b[j - 1] ? 0 : 1;
                    simdiki[j] = Math.Min(Math.Min(simdiki[j - 1] + 1, onceki[j] + 1), onceki[j - 1] + maliyet);
                }
                var t = onceki; onceki = simdiki; simdiki = t;
            }
            return 1.0 - (double)onceki[b.Length] / uzun;
        }
    }
}
