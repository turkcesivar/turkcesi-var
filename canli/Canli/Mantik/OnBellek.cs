using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Canli.Mantik
{
    /// <summary>
    /// Oturum başında sunucudan (ya da diskten) inen oyun belleği: önceden çevrilmiş satırlar sunucuya gitmeden bulunur.
    /// Önce sadeleştirilmiş tam eşleşme, yoksa üçlü harf dizininden seçilen adaylar arasında benzerlik (≥ 0,85).
    /// Yukle arka planda çağrılabilir; arama değişmeyen bir anlık görüntü üzerinde çalışır.
    /// </summary>
    public sealed class OnBellek
    {
        public const double Esik = 0.85;
        private const int EnCokAday = 40;
        private const int EnUzunListe = 4000;
        private const int EnUzunOnek = 24;
        private static readonly Regex Onek = new Regex(@"^\s*([^:]{1," + EnUzunOnek + @"}):\s+(.+)$", RegexOptions.Compiled);

        private sealed class Dizin
        {
            public readonly Dictionary<string, string> Tam = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> Anahtar = new List<string>();
            public readonly List<string> Ceviri = new List<string>();
            public readonly Dictionary<int, List<int>> Uclu = new Dictionary<int, List<int>>();

            public void Ekle(string norm, string tr)
            {
                if (norm.Length == 0 || Tam.ContainsKey(norm)) return;
                Tam[norm] = tr;
                int id = Anahtar.Count;
                Anahtar.Add(norm);
                Ceviri.Add(tr);
                foreach (int h in Ucluler(norm))
                {
                    if (!Uclu.TryGetValue(h, out var liste)) Uclu[h] = liste = new List<int>();
                    liste.Add(id);
                }
            }
        }

        private sealed class Anlik
        {
            public readonly Dizin Altyazi = new Dizin();
            public readonly Dizin Menu = new Dizin();
            public readonly Dictionary<string, string> MenuHarf = new Dictionary<string, string>(StringComparer.Ordinal);
            public int Sayi;
        }

        private volatile Anlik _anlik = new Anlik();

        public int Sayi { get { return _anlik.Sayi; } }

        /// <summary>Belleği verilen satırlarla değiştirir (aynı sadeleştirilmiş metin bir kez).</summary>
        public void Yukle(IEnumerable<(string en, string tr, bool menu)> satirlar)
        {
            var a = new Anlik();
            foreach (var (en, tr, menu) in satirlar)
            {
                if (string.IsNullOrWhiteSpace(en) || string.IsNullOrEmpty(tr)) continue;
                if (menu)
                {
                    string harfli = Sadele(en, false);
                    if (!a.MenuHarf.ContainsKey(harfli)) a.MenuHarf[harfli] = tr;
                    a.Menu.Ekle(Sadele(en, true), tr);
                }
                else a.Altyazi.Ekle(Sadele(en, true), tr);
            }
            a.Sayi = a.Altyazi.Anahtar.Count + a.Menu.Anahtar.Count;
            _anlik = a;
        }

        public string AltyaziBul(string ocr)
        {
            if (string.IsNullOrWhiteSpace(ocr)) return null;
            var a = _anlik;
            string tr = Ara(a.Altyazi, ocr);
            if (tr != null) return tr;
            // "AD: metin" — konuşmacı adı tabloda yoksa adsız aranır, ad çeviriye geri eklenir.
            var m = Onek.Match(ocr);
            if (!m.Success) return null;
            tr = Ara(a.Altyazi, m.Groups[2].Value);
            return tr == null ? null : m.Groups[1].Value.Trim() + ": " + tr;
        }

        public string MenuBul(string ocr)
        {
            if (string.IsNullOrWhiteSpace(ocr)) return null;
            var a = _anlik;
            if (a.MenuHarf.TryGetValue(Sadele(ocr, false), out string tr)) return tr;
            // Oyunlar menü yazısını çoğu kez büyük harfle çizer: harfe duyarsız ve bulanık arama.
            return Ara(a.Menu, ocr);
        }

        private static string Ara(Dizin d, string metin)
        {
            string q = Sadele(metin, true);
            if (q.Length == 0) return null;
            if (d.Tam.TryGetValue(q, out string tr)) return tr;
            if (q.Length < 4 || d.Anahtar.Count == 0) return null;

            // Sayım dizide: dokunulan kimlikler ayrıca tutulur (her aramada yalnız onlar gezilir).
            int[] sayac = new int[d.Anahtar.Count];
            var dokunulan = new List<int>();
            foreach (int h in Ucluler(q).Distinct())
            {
                if (!d.Uclu.TryGetValue(h, out var liste) || liste.Count > EnUzunListe) continue;
                foreach (int id in liste)
                {
                    if (sayac[id]++ == 0 && UzunlukUygun(d.Anahtar[id].Length, q.Length)) dokunulan.Add(id);
                }
            }
            if (dokunulan.Count > EnCokAday)
            {
                dokunulan.Sort((x, y) => sayac[y].CompareTo(sayac[x]));
                dokunulan.RemoveRange(EnCokAday, dokunulan.Count - EnCokAday);
            }
            string bulunan = null;
            double enIyi = 0;
            foreach (int id in dokunulan)
            {
                double o = Benzerlik.Oran(d.Anahtar[id], q);
                if (o >= Esik && o > enIyi) { enIyi = o; bulunan = d.Ceviri[id]; }
            }
            return bulunan;
        }

        private static bool UzunlukUygun(int a, int b)
        {
            int fark = Math.Abs(a - b);
            return fark <= Math.Max(2, (int)(0.2 * Math.Max(a, b)));
        }

        /// <summary>Boşluk sadeleşir, kıvrık tırnaklar düzleşir; istenirse küçük harf.</summary>
        public static string Sadele(string s, bool kucuk)
        {
            var sb = new StringBuilder(s.Length);
            bool bosluk = false;
            foreach (char c0 in s)
            {
                char c = c0;
                if (c == '‘' || c == '’' || c == '‛' || c == '′') c = '\'';
                else if (c == '“' || c == '”' || c == '„' || c == '″') c = '"';
                if (char.IsWhiteSpace(c)) { bosluk = sb.Length > 0; continue; }
                if (bosluk) { sb.Append(' '); bosluk = false; }
                if (c == '…') { sb.Append("..."); continue; }
                sb.Append(kucuk ? char.ToLowerInvariant(c) : c);
            }
            return sb.ToString();
        }

        private static IEnumerable<int> Ucluler(string s)
        {
            string p = " " + s + " ";
            for (int i = 0; i + 3 <= p.Length; i++)
                yield return (p[i] * 31 + p[i + 1]) * 31 + p[i + 2];
        }
    }

    /// <summary>Oyun belleğinin disk önbelleği: %LOCALAPPDATA%\TurkcesiVar\canli\bellek\&lt;oyun özeti&gt;.json.</summary>
    public sealed class OnBellekDiski
    {
        private readonly string _klasor;

        public OnBellekDiski(string klasor) { _klasor = klasor; }

        public static string VarsayilanKlasor()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TurkcesiVar", "canli", "bellek");
        }

        private string Yol(string oyun)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(oyun ?? ""));
                return Path.Combine(_klasor, string.Concat(h.Take(8).Select(b => b.ToString("x2"))) + ".json");
            }
        }

        /// <summary>Kayıt yoksa, bozuksa ya da başka oyuna aitse null.</summary>
        public (string surum, List<(string en, string tr, bool menu)> satirlar)? Oku(string oyun)
        {
            try
            {
                string yol = Yol(oyun);
                if (!File.Exists(yol)) return null;
                using (var belge = JsonDocument.Parse(File.ReadAllText(yol, Encoding.UTF8)))
                {
                    var k = belge.RootElement;
                    if (k.GetProperty("oyun").GetString() != oyun) return null;
                    string surum = k.GetProperty("surum").GetString();
                    return (surum, SatirlariOku(k.GetProperty("satirlar")));
                }
            }
            catch (Exception) { return null; }
        }

        public void Yaz(string oyun, string surum, List<(string en, string tr, bool menu)> satirlar)
        {
            try
            {
                Directory.CreateDirectory(_klasor);
                string yol = Yol(oyun);
                string gecici = yol + ".yeni";
                using (var akis = new FileStream(gecici, FileMode.Create, FileAccess.Write))
                using (var y = new Utf8JsonWriter(akis))
                {
                    y.WriteStartObject();
                    y.WriteString("oyun", oyun);
                    y.WriteString("surum", surum);
                    y.WriteStartArray("satirlar");
                    foreach (var (en, tr, menu) in satirlar)
                    {
                        y.WriteStartArray();
                        y.WriteStringValue(en);
                        y.WriteStringValue(tr);
                        y.WriteStringValue(menu ? "m" : "a");
                        y.WriteEndArray();
                    }
                    y.WriteEndArray();
                    y.WriteEndObject();
                }
                if (File.Exists(yol)) File.Delete(yol);
                File.Move(gecici, yol);
            }
            catch (Exception)
            {
                // Önbellek yazılamazsa bir sonraki oturumda yeniden indirilir.
            }
        }

        /// <summary>[[en, tr, "a"|"m"], …] dizisini okur (sunucu yanıtı ve disk aynı biçim).</summary>
        public static List<(string en, string tr, bool menu)> SatirlariOku(JsonElement dizi)
        {
            var liste = new List<(string, string, bool)>();
            foreach (var s in dizi.EnumerateArray())
            {
                if (s.ValueKind != JsonValueKind.Array || s.GetArrayLength() < 3) continue;
                string en = s[0].ValueKind == JsonValueKind.String ? s[0].GetString() : null;
                string tr = s[1].ValueKind == JsonValueKind.String ? s[1].GetString() : null;
                if (en == null || tr == null) continue;
                liste.Add((en, tr, s[2].ValueKind == JsonValueKind.String && s[2].GetString() == "m"));
            }
            return liste;
        }
    }
}
