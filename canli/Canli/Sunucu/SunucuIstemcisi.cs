using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Canli
{
    /// <summary>Çeviri sunucusuyla konuşur. Hiçbir çağrı istisna fırlatmaz; hatalar HataKodu ile döner.</summary>
    public sealed class SunucuIstemcisi
    {
        public const int EnCokSatirKarakter = 300;
        public const int EnCokBaglam = 5;
        public const int MenuParcaOge = 8;
        public const int MenuParcaKarakter = 1500;

        private static readonly JsonSerializerOptions JsonSecenekleri = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly HttpClient _istemci;
        private readonly string _lisans;
        private readonly string _cihazOzeti;

        /// <summary>İstek başına zaman aşımı (testler kısaltabilir).</summary>
        public TimeSpan ZamanAsimi = TimeSpan.FromSeconds(10);

        static SunucuIstemcisi()
        {
            ServicePointManager.DefaultConnectionLimit = 32;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
        }

        public SunucuIstemcisi(HttpMessageHandler isleyici, string tabanAdres, string lisans, string cihazOzeti)
        {
            _istemci = new HttpClient(isleyici, false)
            {
                BaseAddress = new Uri(tabanAdres.TrimEnd('/') + "/"),
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
            _lisans = lisans;
            _cihazOzeti = cihazOzeti;
        }

        public async Task<OyunBilgisi> OyunAsync(string kimlik)
        {
            var (hata, kok) = await IstekAsync(HttpMethod.Get, "oyun/" + Uri.EscapeDataString(kimlik ?? ""), null);
            using (kok)
            {
                if (hata != null) return new OyunBilgisi { HataKodu = hata };
                try
                {
                    var bilgi = new OyunBilgisi { Durum = kok.RootElement.GetProperty("durum").GetString() };
                    if (kok.RootElement.TryGetProperty("profil", out var p) && p.ValueKind == JsonValueKind.Object)
                        bilgi.Profil = ProfilOku(p);
                    return bilgi;
                }
                catch (Exception) { return new OyunBilgisi { HataKodu = "gecici_hata" }; }
            }
        }

        public async Task<CeviriSonucu> CeviriAsync(string oyun, string oyunAdi, string en, IReadOnlyList<string> baglam)
        {
            if (en != null && en.Length > EnCokSatirKarakter)
                return new CeviriSonucu { HataKodu = "cok_uzun" };

            var govde = new Dictionary<string, object> { ["oyun"] = oyun, ["oyunAdi"] = oyunAdi, ["en"] = en };
            if (baglam != null && baglam.Count > 0)
            {
                govde["baglam"] = baglam.Skip(Math.Max(0, baglam.Count - EnCokBaglam))
                    .Select(b => b != null && b.Length > EnCokSatirKarakter ? b.Substring(0, EnCokSatirKarakter) : b)
                    .ToArray();
            }

            var (hata, kok) = await IstekAsync(HttpMethod.Post, "ceviri", govde);
            using (kok)
            {
                if (hata != null) return new CeviriSonucu { HataKodu = hata };
                try
                {
                    var k = kok.RootElement;
                    if (k.TryGetProperty("okunamaz", out var o) && o.ValueKind == JsonValueKind.True)
                        return new CeviriSonucu { Okunamaz = true };
                    var sonuc = new CeviriSonucu { Tr = k.GetProperty("tr").GetString() };
                    if (k.TryGetProperty("kalan", out var kal) && kal.ValueKind == JsonValueKind.Number)
                        sonuc.Kalan = kal.GetInt32();
                    return sonuc;
                }
                catch (Exception) { return new CeviriSonucu { HataKodu = "gecici_hata" }; }
            }
        }

        public async Task<MenuSonucu> MenuAsync(string oyun, string oyunAdi, IReadOnlyList<string> ogeler)
        {
            int n = ogeler == null ? 0 : ogeler.Count;
            var sonuc = new MenuSonucu { Ceviriler = new string[n] };
            if (n == 0) return sonuc;

            // Parçalara böl: en çok 8 öğe VE en çok 1500 karakter; 300'den uzun öğeler atlanır (null).
            var parcalar = new List<List<int>>();
            var gecerli = new List<int>();
            int karakter = 0;
            for (int i = 0; i < n; i++)
            {
                string oge = ogeler[i];
                if (oge == null || oge.Length > EnCokSatirKarakter) continue;
                if (gecerli.Count > 0 && (gecerli.Count >= MenuParcaOge || karakter + oge.Length > MenuParcaKarakter))
                {
                    parcalar.Add(gecerli);
                    gecerli = new List<int>();
                    karakter = 0;
                }
                gecerli.Add(i);
                karakter += oge.Length;
            }
            if (gecerli.Count > 0) parcalar.Add(gecerli);

            if (parcalar.Count == 0)
            {
                bool uzunVar = Enumerable.Range(0, n).Any(i => ogeler[i] != null && ogeler[i].Length > EnCokSatirKarakter);
                if (uzunVar) sonuc.HataKodu = "cok_uzun";
                return sonuc;
            }

            var gorevler = parcalar.Select(p => ParcaAsync(oyun, oyunAdi, p.Select(i => ogeler[i]).ToArray())).ToArray();
            var yanitlar = await Task.WhenAll(gorevler);

            for (int pi = 0; pi < parcalar.Count; pi++)
            {
                var y = yanitlar[pi];
                if (y.HataKodu != null) continue;
                if (sonuc.Neden == null) sonuc.Neden = y.Neden;
                for (int j = 0; j < parcalar[pi].Count && j < y.Ceviriler.Length; j++)
                    sonuc.Ceviriler[parcalar[pi][j]] = y.Ceviriler[j];
            }

            // Durduran kod her şeyin önüne geçer; yoksa ilk hata kodu bildirilir.
            var kodlar = yanitlar.Select(y => y.HataKodu).Where(h => h != null).ToList();
            sonuc.HataKodu = kodlar.FirstOrDefault(HataYonetimi.DurduranMi) ?? kodlar.FirstOrDefault();
            return sonuc;
        }

        private async Task<MenuSonucu> ParcaAsync(string oyun, string oyunAdi, string[] ogeler)
        {
            var govde = new Dictionary<string, object> { ["oyun"] = oyun, ["oyunAdi"] = oyunAdi, ["ogeler"] = ogeler };
            var (hata, kok) = await IstekAsync(HttpMethod.Post, "menu", govde);
            using (kok)
            {
                if (hata != null) return new MenuSonucu { HataKodu = hata };
                try
                {
                    var k = kok.RootElement;
                    var liste = new string[ogeler.Length];
                    int j = 0;
                    foreach (var e in k.GetProperty("ceviriler").EnumerateArray())
                    {
                        if (j >= liste.Length) break;
                        liste[j++] = e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                    }
                    string neden = null;
                    if (k.TryGetProperty("neden", out var nd) && nd.ValueKind == JsonValueKind.String) neden = nd.GetString();
                    return new MenuSonucu { Ceviriler = liste, Neden = neden };
                }
                catch (Exception) { return new MenuSonucu { HataKodu = "gecici_hata" }; }
            }
        }

        /// <summary>Tek istek. Başarıda (null, gövde); hatada (hataKodu, null).</summary>
        private async Task<(string hata, JsonDocument kok)> IstekAsync(HttpMethod yontem, string yol, object govde)
        {
            try
            {
                using (var iptal = new CancellationTokenSource(ZamanAsimi))
                using (var istek = new HttpRequestMessage(yontem, yol))
                {
                    istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _lisans);
                    istek.Headers.TryAddWithoutValidation("X-Cihaz", _cihazOzeti);
                    if (govde != null)
                        istek.Content = new StringContent(JsonSerializer.Serialize(govde, JsonSecenekleri), Encoding.UTF8, "application/json");

                    using (var yanit = await _istemci.SendAsync(istek, HttpCompletionOption.ResponseHeadersRead, iptal.Token).ConfigureAwait(false))
                    {
                        // Gövde okuması da aynı süre sınırına bağlı: jeton akışı kesmezse yarışla bırakılır.
                        var okuma = yanit.Content.ReadAsStringAsync();
                        var bitenGorev = await Task.WhenAny(okuma, Task.Delay(Timeout.Infinite, iptal.Token)).ConfigureAwait(false);
                        if (bitenGorev != okuma) throw new OperationCanceledException(iptal.Token);
                        string metin = await okuma.ConfigureAwait(false);
                        JsonDocument belge = null;
                        try { belge = JsonDocument.Parse(metin); } catch (Exception) { }

                        if (yanit.IsSuccessStatusCode)
                        {
                            if (belge == null || belge.RootElement.ValueKind != JsonValueKind.Object)
                            {
                                if (belge != null) belge.Dispose();
                                return ("gecici_hata", null);
                            }
                            return (null, belge);
                        }

                        string kod = "gecici_hata";
                        if (belge != null)
                        {
                            using (belge)
                            {
                                if (belge.RootElement.ValueKind == JsonValueKind.Object
                                    && belge.RootElement.TryGetProperty("hata", out var h)
                                    && h.ValueKind == JsonValueKind.String
                                    && !string.IsNullOrEmpty(h.GetString()))
                                    kod = h.GetString();
                            }
                        }
                        return (kod, null);
                    }
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException || e is IOException || e is WebException)
            {
                return ("cevrimdisi", null);
            }
            catch (Exception)
            {
                return ("gecici_hata", null);
            }
        }

        private static Profil ProfilOku(JsonElement p)
        {
            var profil = new Profil();
            if (p.TryGetProperty("altyaziBolgesi", out var a) && a.ValueKind == JsonValueKind.Array)
                profil.AltyaziBolgesi = a.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            if (p.TryGetProperty("renk", out var r) && r.ValueKind == JsonValueKind.String)
                profil.Renk = r.GetString();
            if (p.TryGetProperty("ekBolgeler", out var e) && e.ValueKind == JsonValueKind.Array)
                profil.EkBolgeler = e.EnumerateArray().Select(b => b.EnumerateArray().Select(x => x.GetDouble()).ToArray()).ToArray();
            return profil;
        }
    }
}
