namespace Canli
{
    /// <summary>Tek satır çevirisinin sonucu. Hata varsa HataKodu dolu, diğer alanlar boştur.</summary>
    public sealed class CeviriSonucu
    {
        public string Tr;
        public bool Okunamaz;
        public int? Kalan;
        public string HataKodu;
    }

    /// <summary>Menü çevirisinin sonucu; Ceviriler özgün öğe sırasındadır (çevrilemeyen = null).</summary>
    public sealed class MenuSonucu
    {
        public string[] Ceviriler;
        public string Neden;
        public string HataKodu;
    }

    /// <summary>GET /durum yanıtı: Durum "lisanssiz" | "etkin" | "hak_bitti" | "suresi_bitti".</summary>
    public sealed class LisansDurumu
    {
        public string Durum;
        public int? Kalan;
        public string HataKodu;
    }

    /// <summary>Sunucunun bir oyun için bildirdiği durum: "paketli", "yasakli" ya da "acik".</summary>
    public sealed class OyunBilgisi
    {
        public string Durum;
        public Profil Profil;
        public string HataKodu;
    }

    /// <summary>Sunucu hata kodlarını protokol olaylarına çevirir.</summary>
    public static class HataYonetimi
    {
        // Tablo (kod -> olayTuru, durdur):
        //   lisans_yok, cihaz_dustu, suresi_bitti, paketli, yasakli   -> hata,  durdur
        //   yapilandirma, cihaz_yok                                   -> hata,  durdur
        //   hak_bitti, gun_bitti, butce, hiz, gecici_hata, cevrimdisi -> uyari, devam
        //   deneme_siniri, cok_uzun, gecersiz                         -> uyari, devam
        //   bilinmeyen kod                                            -> uyari, devam
        public static (string olayTuru, bool durdur) Siniflandir(string hataKodu)
        {
            switch (hataKodu)
            {
                case "lisans_yok":
                case "cihaz_dustu":
                case "suresi_bitti":
                case "paketli":
                case "yasakli":
                case "yapilandirma":
                case "cihaz_yok":
                    return ("hata", true);
                default:
                    return ("uyari", false);
            }
        }

        /// <summary>Çevirmeyi durduran kodlar; tek kaynak Siniflandir tablosudur.</summary>
        public static bool DurduranMi(string hataKodu)
        {
            return Siniflandir(hataKodu).durdur;
        }
    }
}
