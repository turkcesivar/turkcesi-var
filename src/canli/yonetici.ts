// Canlı Çeviri yöneticisi (ana süreç): lisans durumunu tutar, eşleştirmeyi yoklar, canli.exe'yi sürer.
// Akış: calistir → canli.exe'ye "izle" → ön plana gelen süreç bir oyunsa "basla" → oyun kapanınca "dur".

import type { ArayuzOyunu } from "../servis/api.js";
import { oyunBul, oyunKimligi } from "./eslestir.js";
import type { Eslesme, LisansDeposu } from "./lisans-deposu.js";
import type { CanliSunucu } from "./sunucu.js";
import type { CanliDurum, LisansBilgisi, SunucuSonucu } from "./tipler.js";

/** canli.exe süreci (ana süreç bunu child_process ile sağlar). */
export interface CanliSurec {
  yaz(satir: string): void;
  /** stdout parçaları (satır sınırında bölünmüş olmayabilir). */
  satirlar(f: (s: string) => void): void;
  cikinca(f: (kod: number | null) => void): void;
  oldur(): void;
}

export interface Zaman {
  setInterval(f: () => void, ms: number): unknown;
  clearInterval(id: unknown): void;
  setTimeout?(f: () => void, ms: number): unknown;
  clearTimeout?(id: unknown): void;
}

export interface YoneticiSecenekleri {
  sunucu: CanliSunucu;
  depo: LisansDeposu;
  baslat: () => CanliSurec;
  oyunlar: () => ArayuzOyunu[];
  surecAcikMi: (ad: string) => Promise<boolean>;
  zaman?: Zaman;
  /** Yalnız geliştirmede (TV_CANLI_TEST_OYUNU): paketli engeli atlanır, oyun kimliği bu olur. */
  testOyunu?: string;
}

const YOKLAMA_MS = 5000;
const UYARI_MS = 15000;
// Bu sunucu hatalarında lisans geçersizdir (sunucuya ulaşıldı); diğerlerinde durum bilinmez (null).
const LISANS_HATALARI = new Set(["lisans_yok", "cihaz_dustu"]);

type Olay = { olay: string; [alan: string]: unknown };

export class CanliYonetici {
  private readonly o: YoneticiSecenekleri;
  private d: CanliDurum = { yukleniyor: false, lisans: null, calisma: "kapali" };
  private readonly dinleyiciler = new Set<(d: CanliDurum) => void>();

  private surec: CanliSurec | null = null;
  private tampon = "";
  /** Süren basla oturumu: yoklanan oyun süreci. */
  private oturum: { sureciAdi: string; yol: string; oyunAdi: string; kimlik?: string } | null = null;
  /** Oturumda canli.exe'nin bildirdiği son hata kodu (paketli/yasakli ayrımı için). */
  private oturumHatasi: string | null = null;
  /** Sunucunun reddettiği oyunun yolu: başka pencere öne gelene kadar yeniden başlatılmaz. */
  private engelliYol: string | null = null;
  private surecYoklama: unknown = null;
  private surecYoklaniyor = false;
  private eslesmeYoklama: unknown = null;
  private eslesmeYoklaniyor = false;
  private uyariSayaci: unknown = null;
  /** kapat() sonrası hiçbir zamanlayıcı ya da süreç başlatılmaz. */
  private kapandi = false;

  constructor(o: YoneticiSecenekleri) {
    this.o = o;
  }

  // ---- durum ----

  durum(): CanliDurum {
    const k: CanliDurum = structuredClone(this.d);
    for (const a of Object.keys(k) as (keyof CanliDurum)[]) if (k[a] === undefined) delete k[a];
    return k;
  }

  dinle(f: (d: CanliDurum) => void): () => void {
    this.dinleyiciler.add(f);
    return () => {
      this.dinleyiciler.delete(f);
    };
  }

  private yay(): void {
    const d = this.durum();
    for (const f of [...this.dinleyiciler]) {
      try {
        f(d);
      } catch {
        // dinleyici hatası yöneticiyi bozmaz
      }
    }
  }

  // ---- zamanlayıcılar ----

  private aralik(f: () => void, ms: number): unknown {
    return this.o.zaman ? this.o.zaman.setInterval(f, ms) : setInterval(f, ms);
  }

  private araligiDurdur(id: unknown): void {
    if (id === null) return;
    if (this.o.zaman) this.o.zaman.clearInterval(id);
    else clearInterval(id as ReturnType<typeof setInterval>);
  }

  private gecikme(f: () => void, ms: number): unknown {
    return this.o.zaman?.setTimeout ? this.o.zaman.setTimeout(f, ms) : setTimeout(f, ms);
  }

  private gecikmeyiDurdur(id: unknown): void {
    if (id === null) return;
    if (this.o.zaman?.clearTimeout) this.o.zaman.clearTimeout(id);
    else clearTimeout(id as ReturnType<typeof setTimeout>);
  }

  // ---- depo (dosya hataları yöneticiyi çökertmez) ----

  private lisansOku(): string | null {
    try {
      return this.o.depo.oku();
    } catch {
      return null;
    }
  }

  /** Lisansı kaydeder; geri okunamıyorsa (şifreleme yok / yazma hatası) false. */
  private lisansYaz(lisans: string): boolean {
    try {
      this.o.depo.yaz(lisans);
    } catch {
      return false;
    }
    return this.lisansOku() === lisans;
  }

  private eslesmeOku(): Eslesme | null {
    try {
      return this.o.depo.eslesmeOku();
    } catch {
      return null;
    }
  }

  private eslesmeYaz(e: Eslesme | null): void {
    try {
      this.o.depo.eslesmeYaz(e);
    } catch {
      // eşleşme bu oturumda bellekte sürer
    }
  }

  // ---- lisans durumu ----

  private static bilgi(veri: LisansBilgisi & { lisans?: string }): LisansBilgisi {
    const { lisans: _lisans, ...b } = veri;
    return b;
  }

  private sonucUygula(r: SunucuSonucu<LisansBilgisi & { lisans?: string }>): void {
    if (r.tamam) {
      this.d.lisans = CanliYonetici.bilgi(r.veri);
      delete this.d.sunucuHatasi;
    } else {
      this.d.lisans = LISANS_HATALARI.has(r.hata) ? { durum: "lisanssiz" } : null;
      this.d.sunucuHatasi = r.hata;
    }
  }

  /** Numara yanıtında lisans geldiyse kaydeder ve eşleşmeyi kapatır. Lisans geldiyse true. */
  private lisansTeslimi(r: SunucuSonucu<LisansBilgisi & { lisans?: string }>): boolean {
    if (!r.tamam || typeof r.veri.lisans !== "string" || !r.veri.lisans) return false;
    this.eslesmeBitir();
    if (this.lisansYaz(r.veri.lisans)) {
      this.sonucUygula(r);
    } else {
      this.d.sunucuHatasi = "kayit";
    }
    return true;
  }

  private eslesmeBitir(): void {
    this.araligiDurdur(this.eslesmeYoklama);
    this.eslesmeYoklama = null;
    delete this.d.eslesme;
    this.eslesmeYaz(null);
  }

  async yenile(): Promise<void> {
    this.d.yukleniyor = true;
    this.yay();
    try {
      let e = this.d.eslesme ?? this.eslesmeOku();
      if (e && e.bitis <= Date.now()) {
        this.eslesmeBitir();
        e = null;
      }
      if (e) {
        this.d.eslesme = e;
        const r = await this.o.sunucu.durum(null, e.numara);
        // Teslim edilen lisans kapanıştan sonra da kaydedilir (sunucu bir kez verir).
        if (this.lisansTeslimi(r)) return;
        if (this.kapandi) return;
        this.eslesmeYoklamasiBaslat();
        const lisans = this.lisansOku();
        if (!lisans) {
          this.sonucUygula(r);
          return;
        }
      }
      const r = await this.o.sunucu.durum(this.lisansOku());
      if (this.kapandi) return;
      this.sonucUygula(r);
    } catch {
      this.d.lisans = null;
      this.d.sunucuHatasi = "gecici_hata";
    } finally {
      this.d.yukleniyor = false;
      this.yay();
    }
  }

  async deneme(): Promise<{ hata?: string }> {
    const r = await this.o.sunucu.deneme();
    if (!r.tamam) return { hata: r.hata };
    if (!this.lisansYaz(r.veri.lisans)) return { hata: "kayit" };
    this.d.lisans = CanliYonetici.bilgi(r.veri);
    delete this.d.sunucuHatasi;
    this.yay();
    return {};
  }

  async eslestirBaslat(): Promise<{ hata?: string }> {
    const r = await this.o.sunucu.eslestir();
    if (!r.tamam) return { hata: r.hata };
    if (this.kapandi) return { hata: "kapandi" };
    const e: Eslesme = { numara: r.veri.numara, bitis: r.veri.bitis };
    this.d.eslesme = e;
    this.eslesmeYaz(e);
    this.eslesmeYoklamasiBaslat();
    this.yay();
    return {};
  }

  private eslesmeYoklamasiBaslat(): void {
    if (this.kapandi || this.eslesmeYoklama !== null) return;
    this.eslesmeYoklama = this.aralik(() => void this.eslesmeYokla(), YOKLAMA_MS);
  }

  private async eslesmeYokla(): Promise<void> {
    const e = this.d.eslesme;
    if (!e) {
      this.eslesmeBitir();
      return;
    }
    if (e.bitis <= Date.now()) {
      this.eslesmeBitir();
      this.yay();
      return;
    }
    if (this.eslesmeYoklaniyor) return;
    this.eslesmeYoklaniyor = true;
    try {
      const r = await this.o.sunucu.durum(null, e.numara);
      if (this.d.eslesme?.numara === e.numara && this.lisansTeslimi(r)) this.yay();
    } catch {
      // bir sonraki yoklamada yeniden denenir
    } finally {
      this.eslesmeYoklaniyor = false;
    }
  }

  // ---- canli.exe ----

  calistir(): void {
    if (this.kapandi || this.surec) return;
    if (!this.lisansOku()) return;
    const ld = this.d.lisans?.durum;
    if (ld !== "etkin" && ld !== "hak_bitti") return;
    delete this.d.hata;
    delete this.d.uyari;
    delete this.d.engel;
    delete this.d.oyunAdi;
    let s: CanliSurec;
    try {
      s = this.o.baslat();
    } catch {
      this.d.calisma = "kapali";
      this.d.hata = "beklenmeyen";
      this.yay();
      return;
    }
    this.surec = s;
    this.tampon = "";
    s.satirlar((p) => {
      if (s === this.surec) this.parcaAl(p);
    });
    s.cikinca(() => {
      if (s === this.surec) this.beklenmeyenCikis();
    });
    this.komut({ komut: "izle" });
    this.d.calisma = "bekleniyor";
    this.yay();
  }

  durdur(): void {
    if (this.surec) {
      if (this.oturum) this.komut({ komut: "dur" });
      this.komut({ komut: "cik" });
      this.surecBirak();
    }
    this.d.calisma = "kapali";
    delete this.d.oyunAdi;
    delete this.d.engel;
    delete this.d.uyari;
    delete this.d.hata;
    this.yay();
  }

  /** Uygulama kapanırken: süreci kapatır, yoklamaları durdurur, dinleyicileri bırakır. */
  kapat(): void {
    this.kapandi = true;
    const s = this.surec;
    this.durdur();
    try {
      s?.oldur();
    } catch {
      // süreç zaten kapanmış olabilir
    }
    this.araligiDurdur(this.eslesmeYoklama);
    this.eslesmeYoklama = null;
    this.gecikmeyiDurdur(this.uyariSayaci);
    this.uyariSayaci = null;
    this.dinleyiciler.clear();
  }

  private komut(k: Record<string, unknown>): void {
    try {
      this.surec?.yaz(JSON.stringify(k));
    } catch {
      // kapanmakta olan sürece yazılamaz; çıkış olayı durumu düzeltir
    }
  }

  /** Süreç bağlantısını ve oturumu bırakır (eski süreçten gelen olaylar artık yok sayılır). */
  private surecBirak(): void {
    this.surec = null;
    this.tampon = "";
    this.engelliYol = null;
    this.oturumBirak();
  }

  private oturumBirak(): void {
    this.oturum = null;
    this.oturumHatasi = null;
    this.araligiDurdur(this.surecYoklama);
    this.surecYoklama = null;
  }

  private beklenmeyenCikis(): void {
    this.surecBirak();
    this.d.calisma = "kapali";
    this.d.hata = "beklenmeyen";
    delete this.d.oyunAdi;
    this.yay();
  }

  private parcaAl(parca: string): void {
    this.tampon += parca;
    let i: number;
    while ((i = this.tampon.indexOf("\n")) >= 0) {
      const satir = this.tampon.slice(0, i).trim();
      this.tampon = this.tampon.slice(i + 1);
      if (!satir) continue;
      let j: unknown;
      try {
        j = JSON.parse(satir);
      } catch {
        continue; // bozuk satır yok sayılır
      }
      if (typeof j !== "object" || j === null || typeof (j as Olay).olay !== "string") continue;
      try {
        this.olayIsle(j as Olay);
      } catch {
        // tek bir olay yöneticiyi bozmaz
      }
      if (!this.surec) return; // olay süreci kapattıysa kalan satırlar eski sürece ait
    }
  }

  private olayIsle(o: Olay): void {
    switch (o.olay) {
      case "onplan":
        if (typeof o.yol === "string" || o.yol === null) this.onPlan(o.yol);
        return;
      case "basladi":
        if (!this.oturum) return;
        this.d.calisma = "cevriliyor";
        this.yay();
        return;
      case "durdu":
        // "komut": kendi dur/basla komutumuzun yanıtı; durum zaten ayarlı.
        if (o.neden === "komut") return;
        if (o.neden === "lisans" && this.oturum && (this.oturumHatasi === "paketli" || this.oturumHatasi === "yasakli")) {
          // Sunucu bu oyunu reddetti: canli.exe ve izleme sürer, oyun engel olarak gösterilir.
          const ot = this.oturum;
          this.d.engel = {
            tur: this.oturumHatasi === "paketli" ? "paketli" : "hile",
            oyunAdi: ot.oyunAdi,
            ...(ot.kimlik ? { kimlik: ot.kimlik } : {}),
          };
          this.engelliYol = ot.yol;
          this.oturumBirak();
          this.d.calisma = "bekleniyor";
          delete this.d.oyunAdi;
          delete this.d.hata;
          this.yay();
          return;
        }
        if (o.neden === "lisans") {
          this.komut({ komut: "cik" });
          this.surecBirak();
          this.d.calisma = "kapali";
          delete this.d.oyunAdi;
          this.yay();
          void this.yenile();
          return;
        }
        // "hata" (ya da bilinmeyen neden): oturum bitti, yeni oyun beklenir
        if (!this.oturum) return;
        this.oturumBirak();
        this.d.calisma = "bekleniyor";
        delete this.d.oyunAdi;
        this.yay();
        return;
      case "kalan":
        if (typeof o.kalan !== "number" || !Number.isFinite(o.kalan) || !this.d.lisans) return;
        this.d.lisans.kalan = o.kalan;
        this.yay();
        return;
      case "uyari":
        if (typeof o.kod !== "string") return;
        this.d.uyari = o.kod;
        this.gecikmeyiDurdur(this.uyariSayaci);
        this.uyariSayaci = this.gecikme(() => {
          this.uyariSayaci = null;
          delete this.d.uyari;
          this.yay();
        }, UYARI_MS);
        this.yay();
        return;
      case "hata":
        if (typeof o.kod !== "string") return;
        if (this.oturum) this.oturumHatasi = o.kod;
        this.d.hata = o.kod;
        this.yay();
        return;
    }
  }

  private onPlan(yol: string | null): void {
    if (!this.surec || this.oturum) return;
    // Sunucunun reddettiği oyun hâlâ öndeyse engel kalır; başka pencere gelince unutulur.
    if (yol !== null && yol === this.engelliYol) return;
    this.engelliYol = null;
    const oyun = oyunBul(yol, this.o.oyunlar());
    if (!oyun || !yol) {
      // Engelli olmayan bir pencere öne geldi: eski engel kalkar.
      if (this.d.engel) {
        delete this.d.engel;
        this.yay();
      }
      return;
    }
    if (oyun.durum === "hile-korumali") {
      this.d.engel = { tur: "hile", oyunAdi: oyun.ad, ...(oyun.kimlik ? { kimlik: oyun.kimlik } : {}) };
      this.yay();
      return;
    }
    if (oyun.durum === "destekleniyor" && !this.o.testOyunu) {
      this.d.engel = { tur: "paketli", oyunAdi: oyun.ad, ...(oyun.kimlik ? { kimlik: oyun.kimlik } : {}) };
      this.yay();
      return;
    }
    const sureciAdi = yol.split(/[\\/]/).pop() ?? "";
    if (!sureciAdi) return;
    const lisans = this.lisansOku();
    if (!lisans) {
      this.d.hata = "lisans_yok";
      this.yay();
      return;
    }
    this.komut({
      komut: "basla",
      lisans,
      oyun: this.o.testOyunu ?? oyunKimligi(oyun),
      oyunAdi: oyun.ad,
      profil: null,
      sureciAdi,
    });
    this.oturum = { sureciAdi, yol, oyunAdi: oyun.ad, ...(oyun.kimlik ? { kimlik: oyun.kimlik } : {}) };
    this.oturumHatasi = null;
    this.d.oyunAdi = oyun.ad;
    delete this.d.engel;
    delete this.d.hata;
    this.surecYoklama = this.aralik(() => void this.surecYokla(), YOKLAMA_MS);
    this.yay();
  }

  private async surecYokla(): Promise<void> {
    const oturum = this.oturum;
    if (!oturum || this.surecYoklaniyor) return;
    this.surecYoklaniyor = true;
    let acik = true;
    try {
      acik = await this.o.surecAcikMi(oturum.sureciAdi);
    } catch {
      acik = true; // yoklama hatasında oturum sürer
    } finally {
      this.surecYoklaniyor = false;
    }
    if (acik || this.oturum !== oturum) return;
    this.komut({ komut: "dur" });
    this.oturumBirak();
    this.d.calisma = "bekleniyor";
    delete this.d.oyunAdi;
    this.yay();
  }
}
