using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Canli
{
    /// <summary>Altyazı bölgesi ve renk ayarları (bölgeler ekran oranı olarak [x, y, genişlik, yükseklik]).</summary>
    public sealed class Profil
    {
        public double[] AltyaziBolgesi;
        public string Renk;
        public double[][] EkBolgeler;

        public static Profil Varsayilan
        {
            get
            {
                return new Profil
                {
                    AltyaziBolgesi = new[] { 0.12, 0.78, 0.76, 0.17 },
                    Renk = "ikisi",
                    EkBolgeler = new double[0][]
                };
            }
        }

        /// <summary>Bilinen renk adları: beyaz, sari, turuncu, ikisi (hepsi).</summary>
        public static bool RenkGecerli(string renk)
        {
            return renk == "beyaz" || renk == "sari" || renk == "turuncu" || renk == "ikisi";
        }

        /// <summary>Bölge [x, y, g, y2] ekranın içinde ve boyutu sıfırdan büyük mü.</summary>
        public static bool BolgeGecerli(double[] b)
        {
            if (b == null || b.Length != 4) return false;
            foreach (double d in b) if (double.IsNaN(d) || double.IsInfinity(d)) return false;
            const double Pay = 1e-6;
            return b[0] >= 0 && b[1] >= 0 && b[2] > 0 && b[3] > 0 && b[0] + b[2] <= 1 + Pay && b[1] + b[3] <= 1 + Pay;
        }
    }

    /// <summary>stdin'den ya da komut satırından gelen bir komut.</summary>
    public sealed class Komut
    {
        public string Tur;
        public string Lisans;
        public string Oyun;
        public string OyunAdi;
        public Profil Profil;
        /// <summary>Profil komutta açıkça verildi mi (verilmediyse sunucunun profili kullanılır).</summary>
        public bool ProfilVerildi;
        public string SurecAdi;
        /// <summary>Tanı kaydı açık mı (--tani ya da "tani": true).</summary>
        public bool Tani;
        /// <summary>Komut satırından geldi (sınama/tanı): süreç adı boş olabilir, stdin kapanınca çıkılmaz.</summary>
        public bool KomutSatiri;
    }

    /// <summary>Olay satırlarının yazıldığı tek kanal.</summary>
    public interface ICikis
    {
        void Yaz(string jsonSatiri);
    }

    /// <summary>stdout: UTF-8 (BOM'suz), kilitli, satır sonu "\n".</summary>
    public sealed class KonsolCikis : ICikis
    {
        private readonly object _kilit = new object();
        private readonly StreamWriter _yazici;

        public KonsolCikis()
        {
            _yazici = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
            {
                NewLine = "\n",
                AutoFlush = true
            };
        }

        public void Yaz(string jsonSatiri)
        {
            lock (_kilit) _yazici.WriteLine(jsonSatiri);
        }
    }

    public static class Protokol
    {
        public const string GecersizKomut = "gecersiz_komut";

        private static readonly string[] GecerliTurler = { "basla", "duraklat", "devam", "dur", "cik", "izle" };

        private static readonly JsonSerializerOptions YazmaSecenekleri = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>Bir stdin satırını komuta çevirir; olmazsa hata = "gecersiz_komut".</summary>
        public static bool KomutCoz(string satir, out Komut komut, out string hata)
        {
            komut = null;
            hata = GecersizKomut;
            try
            {
                using (JsonDocument belge = JsonDocument.Parse(satir ?? ""))
                {
                    JsonElement kok = belge.RootElement;
                    if (kok.ValueKind != JsonValueKind.Object) return false;
                    string tur = Metin(kok, "komut");
                    if (tur == null || Array.IndexOf(GecerliTurler, tur) < 0) return false;

                    var k = new Komut
                    {
                        Tur = tur,
                        Lisans = Metin(kok, "lisans"),
                        Oyun = Metin(kok, "oyun"),
                        OyunAdi = Metin(kok, "oyunAdi"),
                        SurecAdi = Metin(kok, "sureciAdi"),
                        Tani = kok.TryGetProperty("tani", out JsonElement t) && t.ValueKind == JsonValueKind.True,
                        Profil = Profil.Varsayilan
                    };
                    if (kok.TryGetProperty("profil", out JsonElement p) && p.ValueKind == JsonValueKind.Object)
                    {
                        k.Profil = ProfilOku(p);
                        k.ProfilVerildi = true;
                    }
                    komut = k;
                    hata = null;
                    return true;
                }
            }
            catch (Exception e) when (e is JsonException || e is InvalidOperationException || e is FormatException)
            {
                return false;
            }
        }

        /// <summary>{"olay":tur, ...alanlar} biçiminde tek satır JSON üretir (Türkçe karakterler kaçışsız).</summary>
        public static string Olay(string tur, params (string anahtar, object deger)[] alanlar)
        {
            var sozluk = new Dictionary<string, object>(StringComparer.Ordinal);
            var sira = new List<string> { "olay" };
            sozluk["olay"] = tur;
            foreach (var (anahtar, deger) in alanlar)
            {
                if (!sozluk.ContainsKey(anahtar)) sira.Add(anahtar);
                sozluk[anahtar] = deger;
            }
            var sb = new StringBuilder("{");
            for (int i = 0; i < sira.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(sira[i], YazmaSecenekleri)).Append(':')
                  .Append(JsonSerializer.Serialize(sozluk[sira[i]], YazmaSecenekleri));
            }
            return sb.Append('}').ToString();
        }

        /// <summary>--lisans K --oyun ID --oyunAdi AD [--renk r] [--bolge x,y,g,y2] [--ek x,y,g,y2]… [--surec Oyun.exe] [--tani] → basla komutu.</summary>
        public static Komut KomutSatirindan(string[] args)
        {
            var profil = Profil.Varsayilan;
            var ek = new List<double[]>();
            var k = new Komut { Tur = "basla", Profil = profil, KomutSatiri = true };
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--tani") { k.Tani = true; continue; }
                if (!a.StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Beklenmeyen argüman: " + a);
                if (i + 1 >= args.Length) throw new ArgumentException("Değer eksik: " + a);
                string deger = args[++i];
                switch (a)
                {
                    case "--lisans": k.Lisans = deger; break;
                    case "--oyun": k.Oyun = deger; break;
                    case "--oyunAdi": k.OyunAdi = deger; break;
                    case "--renk": profil.Renk = deger; k.ProfilVerildi = true; break;
                    case "--bolge": profil.AltyaziBolgesi = Bolge(deger); k.ProfilVerildi = true; break;
                    case "--ek": ek.Add(Bolge(deger)); k.ProfilVerildi = true; break;
                    case "--surec": k.SurecAdi = deger; break;
                    default: throw new ArgumentException("Bilinmeyen argüman: " + a);
                }
            }
            profil.EkBolgeler = ek.ToArray();
            return k;
        }

        private static double[] Bolge(string metin)
        {
            string[] parcalar = metin.Split(',');
            if (parcalar.Length != 4) throw new ArgumentException("Bölge x,y,g,y2 olmalı: " + metin);
            var sonuc = new double[4];
            for (int i = 0; i < 4; i++)
                sonuc[i] = double.Parse(parcalar[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            return sonuc;
        }

        private static string Metin(JsonElement kok, string ad)
        {
            if (kok.TryGetProperty(ad, out JsonElement e) && e.ValueKind == JsonValueKind.String)
                return e.GetString();
            return null;
        }

        private static Profil ProfilOku(JsonElement p)
        {
            var profil = Profil.Varsayilan;
            if (p.TryGetProperty("altyaziBolgesi", out JsonElement b) && b.ValueKind == JsonValueKind.Array)
                profil.AltyaziBolgesi = Dizi(b);
            string renk = Metin(p, "renk");
            if (Profil.RenkGecerli(renk)) profil.Renk = renk;
            if (p.TryGetProperty("ekBolgeler", out JsonElement e) && e.ValueKind == JsonValueKind.Array)
            {
                var liste = new List<double[]>();
                foreach (JsonElement oge in e.EnumerateArray()) liste.Add(Dizi(oge));
                profil.EkBolgeler = liste.ToArray();
            }
            return profil;
        }

        private static double[] Dizi(JsonElement dizi)
        {
            if (dizi.ValueKind != JsonValueKind.Array || dizi.GetArrayLength() != 4)
                throw new FormatException("Bölge 4 sayı olmalı");
            var sonuc = new double[4];
            int i = 0;
            foreach (JsonElement s in dizi.EnumerateArray()) sonuc[i++] = s.GetDouble();
            return sonuc;
        }
    }
}
