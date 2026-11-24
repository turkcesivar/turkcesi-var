// Arayüzün gördüğü tek API: preload `window.tv` olarak açar. Yalnızca düz veri taşır.
// Oyuncu sürümü: oyunu bul / seç, Türkçeyi kur / kaldır, uygulama güncellemesi. Çeviri yapma yok (o CLI'da).

import type { BulunanOyun } from "../oyunlar/steam.js";
import type { GuncellemeHatasiNedeni } from "../ortak/guncelleme-hata.js";
import type { CanliDurum } from "../canli/tipler.js";
import type { Ilerleme, OyunDurumu } from "./tipler.js";

/** Arayüze giden oyun: görseller `tv-gorsel://` adresleri olarak, tanım yerine kimlik. */
export interface ArayuzOyunu extends Omit<BulunanOyun, "tanim" | "gorseller"> {
  kimlik?: string;
  /** Kullanıcı klasörü elle seçtiyse true. */
  elle?: boolean;
  gorseller: { kapak?: string; afis?: string; logo?: string; baslik?: string };
}

/** Türkçesi hazır oyunların listesi (bilgisayarda bulunmasa da gösterilir). */
export interface DesteklenenOyun {
  kimlik: string;
  ad: string;
  klasorAdi: string;
  /** Doğru klasörü seçerken içinde aranacak dosyalar. */
  tanimaDosyalari: string[];
}

export interface TaramaSonucu {
  oyunlar: ArayuzOyunu[];
  desteklenenler: DesteklenenOyun[];
  steamBulundu: boolean;
}

export type GuncellemeDurumu =
  | { durum: "kapali" }
  | { durum: "denetleniyor" }
  | { durum: "guncel" }
  /** Yeni sürüm bulundu ama kullanıcı henüz "İndir" demedi; kendiliğinden inmez. */
  | { durum: "var"; surum: string }
  | { durum: "indiriliyor"; yuzde: number; surum: string }
  | { durum: "hazir"; surum: string }
  | { durum: "hata"; mesaj: string; neden: GuncellemeHatasiNedeni };

export interface TvApi {
  oyunlariTara(): Promise<TaramaSonucu>;
  /** Klasör seçme penceresi; iptalde undefined. */
  klasorSec(kimlik?: string): Promise<{ oyun: ArayuzOyunu } | { hata: string } | undefined>;
  oyunDurumu(kimlik: string, klasor: string): Promise<OyunDurumu>;
  turkceYap(kimlik: string, klasor: string, ilerleme?: (i: Ilerleme) => void): Promise<void>;
  kaldir(kimlik: string, klasor: string): Promise<void>;
  klasoruAc(kimlik: string, klasor: string): Promise<void>;
  guncellemeDurumu(): Promise<GuncellemeDurumu>;
  guncellemeDinle(f: (d: GuncellemeDurumu) => void): () => void;
  guncellemeKur(): Promise<void>;
  /** Kullanıcı "İndir" deyince yeni sürümü indirmeye başlar. */
  guncellemeIndir(): Promise<void>;
  /** Elle denetim: sonuç durumu döner (kurulu olmayan geliştirme sürümünde "kapali"). */
  guncellemeDenetle(): Promise<GuncellemeDurumu>;
  disBaglanti(url: string): Promise<void>;
  surum(): Promise<string>;
  canliDurum(): Promise<CanliDurum>;
  canliDinle(f: (d: CanliDurum) => void): () => void;
  canliYenile(): Promise<void>;
  canliDeneme(): Promise<{ hata?: string }>;
  canliEslestir(): Promise<{ hata?: string }>;
  canliBaslat(): Promise<void>;
  canliDurdur(): Promise<void>;
}

/** tv:cagir ile çağrılabilen servis yöntemleri (beyaz liste). */
export const SERVIS_YONTEMLERI = ["oyunDurumu", "turkceYap", "kaldir"] as const;
