// Canlı Çeviri: arayüz metinleri (onaylı ürün metni; değiştirme).

export const NASIL_BASLIK = "Canlı çeviri nasıl çalışır?";

export const FARK = [
  {
    baslik: "Türkçe paket (ücretsiz)",
    metin: "Oyunun kendi dosyaları çevrilir. Her yazı oyunun kendi yazı tipiyle, yerli yerinde ve baştan sona Türkçedir. Türkçesi hazır oyunlarda her zaman bunu kullan.",
  },
  {
    baslik: "Canlı çeviri",
    metin: "Ekrandaki İngilizce yazıyı okur, internetten anında çevirtir ve Türkçesini İngilizcenin üstüne yazar. Oyunun dosyalarına dokunmaz.",
  },
] as const;

export const SINIRLAR_BASLIK = "Bilmen gerekenler";

export const SINIRLAR = [
  "Her şey çevrilmez. Çok küçük, süslü, hareketli ya da resmin içine gömülü yazılar okunamayabilir.",
  "Bazı çeviriler kusurlu olabilir. Yazı yanlış okunursa çeviri de bozuk ya da anlamsız çıkabilir; bağlamı bilinmeyen kısa satırlar yanlış anlaşılabilir.",
  "Türkçe, İngilizceden 1-2 saniye sonra gelir. Daha önce çevrilmiş satırlar anında görünür.",
  "Bir oyun ne kadar çok oynanırsa canlı çevirisi o kadar hızlanır: çevrilen satırlar ortak belleğe kaydedilir ve o oyunu sonra oynayan herkese anında gelir. Çok oynanan oyunlarda satırların çoğu beklemeden görünür.",
  "Türkçe yazı oyunun yazı tipiyle değil, İngilizcenin üstüne konan bir kutuda görünür.",
  "Oyun pencereli tam ekranda olmalı ve altyazılar açık olmalı. İnternet bağlantısı gerekir.",
  "Windows'ta İngilizce dil paketi yüklü olmalı (ekrandaki İngilizce yazıyı Windows okur). Yoksa uygulama sana nasıl kuracağını gösterir.",
  "Hile korumalı ve çevrimiçi oyunlarda çalışmaz.",
] as const;

export const KAPANIS =
  "Canlı çeviri, Türkçesi olmayan oyunları beklemeden oynayabilmen için var. Çok oynanan oyunlar zamanla herkese ücretsiz Türkçe pakete dönüşür.";

export const NEDEN_UCRETLI =
  "Her yeni satır o an ücretli bir çeviri servisinden geçer; bu masraf her oyuncuda tekrar eder. Destekler hem bu masrafı hem de yeni ücretsiz Türkçe paketleri karşılar. Türkçe paketler her zaman ücretsiz kalacak.";

export const HAK_BITTI = "Bu ayki çeviri hakkın doldu. Hakkın yenilenene kadar yalnızca daha önce çevrilmiş satırlar görünecek.";
export const GUN_BITTI = "Bugünkü çeviri hakkın doldu; yarın 00:00'da yenilenir. Bu arada yalnızca daha önce çevrilmiş satırlar görünür.";
export const SURE_BITTI = "Canlı çeviri lisansının süresi doldu. Yeniden destek olursan aynı bilgisayarda kaldığın yerden devam edersin.";

export function durumSatiri(calisma: string, oyunAdi?: string): string {
  switch (calisma) {
    case "bekleniyor":
      return "Oyun bekleniyor… Kütüphanendeki bir oyunu aç; oyun öne gelince çeviri başlar.";
    case "cevriliyor":
      return `${oyunAdi ?? "Oyun"} çevriliyor. F7: ekranı çevir · F9: duraklat`;
    case "duraklatildi":
      return `${oyunAdi ?? "Oyun"}: duraklatıldı (F9 ile devam)`;
    default:
      return "Canlı çeviri kapalı.";
  }
}

const KODLAR: Record<string, string> = {
  siyah_ekran: "Ekran okunamıyor. Oyun gerçek tam ekrandaysa ayarlarından pencereli tam ekrana al (yükleme ekranlarında bu uyarı kendiliğinden geçer).",
  cevrimdisi: "Sunucuya ulaşılamıyor; internet bağlantını kontrol et.",
  gecici_hata: "Çeviri servisi geçici olarak yanıt vermiyor; birazdan yeniden denenir.",
  hiz: "Çok hızlı istek gönderildi; birkaç saniye bekleniyor.",
  butce: "Bugünlük genel çeviri kapasitesi doldu; daha önce çevrilmiş satırlar görünmeye devam eder.",
  hak_bitti: HAK_BITTI,
  gun_bitti: GUN_BITTI,
  ocr_dil_yok: "Windows'ta İngilizce dil paketi yüklü değil. Yukarıdaki adımlarla kurup \"Yeniden denetle\"ye bas.",
  cihaz_dustu: "Bu lisans başka iki bilgisayarda kullanıldığı için bu bilgisayarın bağlantısı düştü.",
  lisans_yok: "Lisans bulunamadı.",
  deneme_kullanildi: "Bu bilgisayarda deneme daha önce kullanılmış.",
  deneme_siniri: "Bugün bu bağlantıdan çok fazla deneme başlatıldı; yarın yeniden dene.",
  cihaz: "Bu bilgisayarın kimliği okunamadı; canlı çeviri şu an kullanılamıyor.",
  kayit: "Lisans bu bilgisayara kaydedilemedi. Lütfen Discord'dan bize yaz; lisansını elle bağlayalım.",
};

export function kodMetni(kod: string): string {
  return KODLAR[kod] ?? `Beklenmeyen bir sorun oluştu (${kod}).`;
}

export const paketliEngel = (oyunAdi: string) =>
  `${oyunAdi} oyununun ücretsiz Türkçesi var; canlı çeviriye gerek yok. Türkçe paketler destekçilerin katkısıyla herkese ücretsiz yapılıyor; canlı çeviride desteklenen her oyun zamanla yeni bir ücretsiz pakete dönüşüyor.`;
export const hileEngel = (oyunAdi: string) => `${oyunAdi} hile korumalı olduğu için canlı çeviri bu oyunda çalışmaz.`;

export const DENEME_KARTI = "Deneme başladı: 7 gün, 2.000 yeni satır. Sorun, öneri ve yeni oyunlar için Discord'a katıl.";
export const DENEME_DAVETI = "Sorun, öneri ve yeni oyunlar için Discord'a katıl.";
export const ESLESTIRME_ACIKLAMA_SON = "Ödemen gelince canlı çeviri bu bilgisayarda kendiliğinden açılır.";

// ---- İngilizce dil paketi (Windows metin tanıma) ----

export const DIL_BASLIK = "İngilizce dil paketi gerekli";
export const DIL_ACIKLAMA =
  "Canlı çeviri, ekrandaki İngilizce yazıyı Windows'un kendi metin tanıma özelliğiyle okur. Bunun için Windows'ta İngilizce dil paketi yüklü olmalı ve bu bilgisayarda bulunamadı. Windows'un dili değişmez; yalnızca İngilizce yazıyı okuyabilmesi için gereken parça eklenir.";
export const DIL_ADIMLAR = [
  "Aşağıdaki \"Dil ayarlarını aç\" düğmesine bas (Ayarlar → Saat ve dil → Dil ve bölge açılır).",
  "\"Tercih edilen diller\" satırındaki \"Dil ekle\" düğmesine bas.",
  "Arama kutusuna English yaz, \"English (United States)\" seç ve \"İleri\"ye bas.",
  "\"Windows görüntüleme dili olarak ayarla\" işaretliyse kaldır (Windows Türkçe kalsın). Diğer seçenekleri olduğu gibi bırakıp \"Yükle\"ye bas.",
  "Yükleme bitince (birkaç dakika sürebilir) \"Yeniden denetle\"ye bas. Hâlâ bulunamazsa bilgisayarı yeniden başlatıp tekrar dene.",
] as const;
export const DIL_WIN10 = "Windows 10'da yol: Ayarlar → Saat ve dil → Dil → \"Tercih edilen dil ekle\".";
export const DIL_GELISMIS =
  "Olmazsa: Başlat menüsünde PowerShell'e sağ tıkla → \"Yönetici olarak çalıştır\", aşağıdaki komutu yapıştırıp Enter'a bas (internet bağlantısı gerekir; Windows Update'ten indirir, birkaç dakika sürebilir). Bitince \"Yeniden denetle\"ye bas.";
export const DIL_KOMUTU = 'Add-WindowsCapability -Online -Name "Language.OCR~~~en-US~0.0.1.0"';
export const DIL_GEREKLI_IPUCU = "Önce İngilizce dil paketini kur (yukarıdaki adımlar).";
