// Canlı Çeviri: sunucu ve lisans için ortak tipler.

export type LisansTuru = "deneme" | "destekci" | "ust";

export interface LisansBilgisi {
  durum: "lisanssiz" | "etkin" | "hak_bitti" | "suresi_bitti";
  tur?: LisansTuru;
  donemBitis?: number;
  kalan?: number;
  gunKalan?: number;
  bekleyenEk?: number;
  /** Destekçi kodu (XXXX-XXXX): destek isterken söylenir, panelde bununla bulunur. */
  kod?: string;
}

// hata: sunucunun döndürdüğü kod ya da ağ yoksa "cevrimdisi"
export type SunucuSonucu<T> = { tamam: true; veri: T } | { tamam: false; hata: string };

// Ekrandaki çubuğun tavanı (seviye hakları)
export const SEVIYE_TAVAN: Record<LisansTuru, { ay: number; gun: number }> = {
  deneme: { ay: 2000, gun: 2000 },
  destekci: { ay: 20000, gun: 2000 },
  ust: { ay: 50000, gun: 5000 },
};

// Uygulamanın canlı çeviri durumu (arayüze giden düz veri).
export type CanliCalisma = "kapali" | "bekleniyor" | "cevriliyor" | "duraklatildi";

export interface CanliDurum {
  yukleniyor: boolean;
  lisans: LisansBilgisi | null /* null: sunucuya ulaşılamadı */;
  sunucuHatasi?: string;
  eslesme?: { numara: string; bitis: number };
  calisma: CanliCalisma;
  oyunAdi?: string;
  uyari?: string;
  hata?: string;
  engel?: { tur: "paketli" | "hile"; oyunAdi: string; kimlik?: string };
  /** Windows'ta İngilizce metin tanıma: true yüklü, false yüklü değil, yoksa henüz bilinmiyor. */
  ocrHazir?: boolean;
}
