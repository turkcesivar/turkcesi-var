import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { LisansDeposu } from "../../src/canli/lisans-deposu.js";
import type { CanliSunucu } from "../../src/canli/sunucu.js";
import type { LisansBilgisi, SunucuSonucu } from "../../src/canli/tipler.js";
import { CanliYonetici, type CanliSurec } from "../../src/canli/yonetici.js";
import type { ArayuzOyunu } from "../../src/servis/api.js";

class SahteSurec implements CanliSurec {
  yazilan: string[] = [];
  olduruldu = false;
  private satirF: ((s: string) => void) | undefined;
  private cikisF: ((k: number | null) => void) | undefined;
  yaz(satir: string): void {
    this.yazilan.push(satir);
  }
  satirlar(f: (s: string) => void): void {
    this.satirF = f;
  }
  cikinca(f: (k: number | null) => void): void {
    this.cikisF = f;
  }
  oldur(): void {
    this.olduruldu = true;
  }
  ver(parca: string): void {
    this.satirF?.(parca);
  }
  olay(o: object): void {
    this.ver(JSON.stringify(o) + "\n");
  }
  cik(kod: number | null): void {
    this.cikisF?.(kod);
  }
  komutlar(): { komut: string; [k: string]: unknown }[] {
    return this.yazilan.map((s) => JSON.parse(s) as { komut: string });
  }
}

class SahteDepo {
  lisans: string | null = "L";
  eslesme: { numara: string; bitis: number } | null = null;
  oku(): string | null {
    return this.lisans;
  }
  yaz(l: string): void {
    this.lisans = l;
  }
  sil(): void {
    this.lisans = null;
  }
  eslesmeOku(): { numara: string; bitis: number } | null {
    return this.eslesme;
  }
  eslesmeYaz(e: { numara: string; bitis: number } | null): void {
    this.eslesme = e;
  }
}

type DurumYaniti = SunucuSonucu<LisansBilgisi & { lisans?: string }>;

function sahteSunucu(durum: (lisans: string | null, numara?: string) => DurumYaniti) {
  return {
    durum: vi.fn(async (l: string | null, n?: string) => durum(l, n)),
    deneme: vi.fn(async (): Promise<SunucuSonucu<{ lisans: string } & LisansBilgisi>> => ({ tamam: false, hata: "deneme_kullanildi" })),
    eslestir: vi.fn(async (): Promise<SunucuSonucu<{ numara: string; bitis: number }>> => ({
      tamam: true,
      veri: { numara: "123456", bitis: Date.now() + 3600_000 },
    })),
    odemeBaglantisi: vi.fn(async (_s: string): Promise<SunucuSonucu<{ url: string; numara: string; bitis: number }>> => ({
      tamam: true,
      veri: { url: "https://kreosus.com/pay/x", numara: "TV-7K2M", bitis: Date.now() + 3600_000 },
    })),
    cihazEkle: vi.fn(async (_l: string, _n: string): Promise<SunucuSonucu<{ tamam: boolean }>> => ({ tamam: true, veri: { tamam: true } })),
  };
}

const ETKIN: DurumYaniti = { tamam: true, veri: { durum: "etkin", tur: "destekci", kalan: 1000 } };

function oyun(klasor: string, ek: Partial<ArayuzOyunu> = {}): ArayuzOyunu {
  return { ad: "Oyun", klasor, durum: "desteklenmiyor", gorseller: {}, ...ek };
}

interface KurSecenekleri {
  oyunlar?: ArayuzOyunu[];
  durum?: (l: string | null, n?: string) => DurumYaniti;
  acik?: () => boolean;
  testOyunu?: string;
  ocr?: () => Promise<boolean | null>;
  tani?: boolean;
}

function kur(o: KurSecenekleri = {}) {
  const surec = new SahteSurec();
  const depo = new SahteDepo();
  const sunucu = sahteSunucu(o.durum ?? (() => ETKIN));
  const acik = vi.fn(async (_ad: string) => (o.acik ? o.acik() : true));
  const baslat = vi.fn(() => surec);
  const y = new CanliYonetici({
    sunucu: sunucu as unknown as CanliSunucu,
    depo: depo as unknown as LisansDeposu,
    baslat,
    oyunlar: () => o.oyunlar ?? [],
    surecAcikMi: acik,
    testOyunu: o.testOyunu,
    ocrDenetle: o.ocr,
    tani: o.tani,
  });
  return { y, surec, depo, sunucu, acik, baslat };
}

const STEAM = "C:\\Steam\\steamapps\\common\\Oyun X";

beforeEach(() => {
  vi.useFakeTimers();
});
afterEach(() => {
  vi.useRealTimers();
});

describe("CanliYonetici", () => {
  it("destek_ol_baglantiyi_doner_numarayi_eslesme_olarak_yoklar", async () => {
    const { y, depo, sunucu } = kur();
    expect(await y.destekOl("destekci")).toEqual({ url: "https://kreosus.com/pay/x" });
    expect(sunucu.odemeBaglantisi).toHaveBeenCalledWith("destekci");
    expect(y.durum().eslesme?.numara).toBe("TV-7K2M");
    expect(depo.eslesme?.numara).toBe("TV-7K2M");
    sunucu.durum.mockClear();
    await vi.advanceTimersByTimeAsync(5000);
    expect(sunucu.durum).toHaveBeenCalledWith(null, "TV-7K2M");
    y.kapat();
  });

  it("cihaz_ekle_kayitli_lisansla_sunucuya_gider", async () => {
    const { y, depo, sunucu } = kur();
    depo.lisans = null;
    expect(await y.cihazEkle("TV-7K2M")).toEqual({ hata: "lisans_yok" });
    depo.lisans = "LIS";
    expect(await y.cihazEkle("TV-7K2M")).toEqual({});
    expect(sunucu.cihazEkle).toHaveBeenCalledWith("LIS", "TV-7K2M");
    sunucu.cihazEkle.mockResolvedValueOnce({ tamam: false, hata: "numara_bagli" });
    expect(await y.cihazEkle("TV-7K2M")).toEqual({ hata: "numara_bagli" });
  });

  it("dil_denetimi_sonucu_duruma_yazilir", async () => {
    const yok = kur({ ocr: async () => false });
    expect(yok.y.durum().ocrHazir).toBeUndefined();
    await yok.y.dilDenetle();
    expect(yok.y.durum().ocrHazir).toBe(false);
    const var_ = kur({ ocr: async () => true });
    await var_.y.dilDenetle();
    expect(var_.y.durum().ocrHazir).toBe(true);
  });

  it("dil_denetimi_anlasilamazsa_onceki_bilgi_kalir", async () => {
    let sonuc: boolean | null = true;
    const k = kur({ ocr: async () => sonuc });
    await k.y.dilDenetle();
    sonuc = null;
    await k.y.dilDenetle();
    expect(k.y.durum().ocrHazir).toBe(true);
    const hatali = kur({ ocr: async () => { throw new Error("x"); } });
    await hatali.y.dilDenetle();
    expect(hatali.y.durum().ocrHazir).toBeUndefined();
  });

  it("calisirken_ocr_dil_yok_gelirse_durum_guncellenir", async () => {
    const k = kur({ ocr: async () => true });
    await k.y.yenile();
    await k.y.dilDenetle();
    k.y.calistir();
    k.surec.olay({ olay: "hata", kod: "ocr_dil_yok" });
    expect(k.y.durum().ocrHazir).toBe(false);
    expect(k.y.durum().hata).toBe("ocr_dil_yok");
  });

  it("dil_paketi_kurulunca_eski_ocr_hatasi_kalkar", async () => {
    let sonuc = true;
    const k = kur({ ocr: async () => sonuc });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "hata", kod: "ocr_dil_yok" });
    expect(k.y.durum().hata).toBe("ocr_dil_yok");
    sonuc = true;
    await k.y.dilDenetle();
    expect(k.y.durum().ocrHazir).toBe(true);
    expect(k.y.durum().hata).toBeUndefined();
  });

  it("dil_denetimi_ayni_anda_tek_surec", async () => {
    let coz: (v: boolean) => void = () => {};
    const ocr = vi.fn(() => new Promise<boolean>((r) => (coz = r)));
    const k = kur({ ocr });
    const a = k.y.dilDenetle();
    const b = k.y.dilDenetle();
    expect(b).toBe(a);
    coz(true);
    await a;
    expect(ocr).toHaveBeenCalledTimes(1);
  });

  it("dil_denetimi_kapattiktan_sonra_yazmaz", async () => {
    let coz: (v: boolean) => void = () => {};
    const k = kur({ ocr: () => new Promise<boolean>((r) => (coz = r)) });
    const p = k.y.dilDenetle();
    k.y.kapat();
    coz(false);
    await p;
    expect(k.y.durum().ocrHazir).toBeUndefined();
  });

  it("ilk_durum_yukleniyor_ilk_yenileye_kadar", async () => {
    const k = kur();
    expect(k.y.durum().yukleniyor).toBe(true);
    await k.y.yenile();
    expect(k.y.durum().yukleniyor).toBe(false);
  });

  it("yenile_es_zamanli_cagrilarda_tek_istek", async () => {
    const k = kur();
    const a = k.y.yenile();
    const b = k.y.yenile();
    expect(b).toBe(a);
    await Promise.all([a, b]);
    expect(k.sunucu.durum).toHaveBeenCalledTimes(1);
    await k.y.yenile();
    expect(k.sunucu.durum).toHaveBeenCalledTimes(2);
  });

  it("deneme_sonrasi_durum_yenilenir", async () => {
    const k = kur({ durum: () => ({ tamam: true, veri: { durum: "etkin", tur: "deneme", kalan: 2000, gunKalan: 2000 } }) });
    k.depo.lisans = null;
    k.sunucu.deneme.mockResolvedValueOnce({ tamam: true, veri: { lisans: "D", durum: "etkin", tur: "deneme", kalan: 2000 } });
    expect(await k.y.deneme()).toEqual({});
    await vi.advanceTimersByTimeAsync(0);
    expect(k.sunucu.durum).toHaveBeenCalledWith("D");
    expect(k.y.durum().lisans?.gunKalan).toBe(2000);
  });

  it("calistir_lisanssiz_noop", async () => {
    const k = kur({ durum: () => ({ tamam: true, veri: { durum: "lisanssiz" } }) });
    await k.y.yenile();
    k.y.calistir();
    expect(k.baslat).not.toHaveBeenCalled();
    expect(k.y.durum().calisma).toBe("kapali");
  });

  it("calistir_depoda_lisans_yoksa_noop", async () => {
    const k = kur();
    await k.y.yenile();
    k.depo.lisans = null;
    k.y.calistir();
    expect(k.baslat).not.toHaveBeenCalled();
  });

  it("calistir_izle_yazar_bekleniyor", async () => {
    const k = kur();
    await k.y.yenile();
    k.y.calistir();
    k.y.calistir();
    expect(k.baslat).toHaveBeenCalledTimes(1);
    expect(k.surec.komutlar()).toEqual([{ komut: "izle" }]);
    expect(k.y.durum().calisma).toBe("bekleniyor");
  });

  it("paketli_oyunda_baslamaz", async () => {
    const k = kur({ oyunlar: [oyun(STEAM, { ad: "Paketli", durum: "destekleniyor", kimlik: "mafia", appId: 1 })] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.surec.komutlar().some((c) => c.komut === "basla")).toBe(false);
    expect(k.y.durum().engel).toEqual({ tur: "paketli", oyunAdi: "Paketli", kimlik: "mafia" });
    expect(k.y.durum().calisma).toBe("bekleniyor");
  });

  it("paketli_engel_test_oyunu_ile_atlanir", async () => {
    const k = kur({ oyunlar: [oyun(STEAM, { durum: "destekleniyor", appId: 1 })], testOyunu: "test:mafia" });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    const basla = k.surec.komutlar().find((c) => c.komut === "basla");
    expect(basla?.oyun).toBe("test:mafia");
    expect(k.y.durum().engel).toBeUndefined();
  });

  it("hile_korumali_engel", async () => {
    const k = kur({ oyunlar: [oyun(STEAM, { ad: "Hileli", durum: "hile-korumali" })] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.surec.komutlar().some((c) => c.komut === "basla")).toBe(false);
    expect(k.y.durum().engel?.tur).toBe("hile");
    expect(k.y.durum().engel?.oyunAdi).toBe("Hileli");
  });

  it("tani_secenegi_basla_komutuna_eklenir", async () => {
    const k = kur({ oyunlar: [oyun(STEAM, { appId: 123, platform: "steam", ad: "Oyun X" })], tani: true });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.surec.komutlar().find((c) => c.komut === "basla")?.tani).toBe(true);
  });

  it("desteklenmeyen_oyunda_basla_surecAdi_ile", async () => {
    const k = kur({ oyunlar: [oyun(STEAM, { ad: "Oyun X", appId: 123, platform: "steam" })] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: "c:\\steam\\steamapps\\common\\oyun x\\bin\\x.exe" });
    const basla = k.surec.komutlar().find((c) => c.komut === "basla");
    expect(basla).toEqual({ komut: "basla", lisans: "L", oyun: "steam:123", oyunAdi: "Oyun X", profil: null, sureciAdi: "x.exe" });
    expect(k.y.durum().calisma).toBe("bekleniyor");
    k.surec.olay({ olay: "basladi" });
    expect(k.y.durum().calisma).toBe("cevriliyor");
    expect(k.y.durum().oyunAdi).toBe("Oyun X");
    // oturum sürerken aynı sürecin onplan'ı (harf farkıyla da) ikinci basla göndermez
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\bin\\X.EXE" });
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(1);
  });

  it("oyun_disi_onplan_yok_sayilir", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: "C:\\Windows\\explorer.exe" });
    k.surec.olay({ olay: "onplan", yol: null });
    expect(k.surec.komutlar()).toEqual([{ komut: "izle" }]);
  });

  it("oyun_kapaninca_dur_ve_bekle", async () => {
    let acik = true;
    const k = kur({ oyunlar: [oyun(STEAM, { appId: 5 })], acik: () => acik });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "basladi" });
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.acik).toHaveBeenCalledWith("x.exe");
    expect(k.surec.komutlar().some((c) => c.komut === "dur")).toBe(false);
    acik = false;
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.surec.komutlar().filter((c) => c.komut === "dur")).toHaveLength(1);
    expect(k.y.durum().calisma).toBe("bekleniyor");
    expect(k.y.durum().oyunAdi).toBeUndefined();
    // canli.exe'nin "durdu komut" yanıtı durumu bozmaz
    k.surec.olay({ olay: "durdu", neden: "komut" });
    expect(k.y.durum().calisma).toBe("bekleniyor");
    // yoklama durdu (3 sn sonra kapanan oyun bir kez daha yoklanır; kapalı kaldığı için başlamaz)
    const sayi = k.acik.mock.calls.length;
    await vi.advanceTimersByTimeAsync(20000);
    expect(k.acik.mock.calls.length).toBe(sayi + 1);
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(1);
    // oyun yeniden açılınca yeni oturum
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(2);
  });

  it("surec_cokunce_kapali_ve_hata", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "basladi" });
    k.surec.cik(3);
    expect(k.y.durum().calisma).toBe("kapali");
    expect(k.y.durum().hata).toBe("beklenmeyen");
    expect(k.y.durum().oyunAdi).toBeUndefined();
    // yoklama da durmuş olmalı
    await vi.advanceTimersByTimeAsync(20000);
    expect(k.acik).not.toHaveBeenCalled();
    // yeniden çalıştırınca yeni süreç başlar, eski hata silinir
    k.y.calistir();
    expect(k.baslat).toHaveBeenCalledTimes(2);
    expect(k.y.durum().hata).toBeUndefined();
  });

  it("bolunmus_ve_bozuk_satir", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.ver('{"olay":"bas');
    expect(k.y.durum().calisma).toBe("bekleniyor");
    expect(() => k.surec.ver('ladi"}\n{bozuk\n')).not.toThrow();
    expect(k.y.durum().calisma).toBe("cevriliyor");
    expect(() => k.surec.ver('null\n42\n"x"\r\n{"olay":"kalan","kalan":"cok"}\n{"olay":"onplan","yol":5}\n')).not.toThrow();
    expect(k.y.durum().calisma).toBe("cevriliyor");
    expect(k.y.durum().lisans?.kalan).toBe(1000);
  });

  it("kalan_olayi_lisansi_gunceller", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "kalan", kalan: 742 });
    expect(k.y.durum().lisans?.kalan).toBe(742);
  });

  it("kalan_olayi_gunKalani_da_dusurur", async () => {
    const k = kur({ durum: () => ({ tamam: true, veri: { durum: "etkin", tur: "destekci", kalan: 1000, gunKalan: 300 } }) });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "kalan", kalan: 900 });
    expect(k.y.durum().lisans).toMatchObject({ kalan: 900, gunKalan: 200 });
    k.surec.olay({ olay: "kalan", kalan: 500 });
    expect(k.y.durum().lisans).toMatchObject({ kalan: 500, gunKalan: 0 });
  });

  it("uyari_15_sn_sonra_kalkar", async () => {
    const k = kur();
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "uyari", kod: "siyah_ekran" });
    expect(k.y.durum().uyari).toBe("siyah_ekran");
    await vi.advanceTimersByTimeAsync(10000);
    k.surec.olay({ olay: "uyari", kod: "hiz" });
    await vi.advanceTimersByTimeAsync(10000);
    expect(k.y.durum().uyari).toBe("hiz");
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.y.durum().uyari).toBeUndefined();
  });

  it("durdu_lisans_yenile_cagirir", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "basladi" });
    const once = k.sunucu.durum.mock.calls.length;
    k.surec.olay({ olay: "hata", kod: "cihaz_dustu" });
    expect(k.y.durum().hata).toBe("cihaz_dustu");
    k.surec.olay({ olay: "durdu", neden: "lisans" });
    expect(k.y.durum().calisma).toBe("kapali");
    await vi.advanceTimersByTimeAsync(0);
    expect(k.sunucu.durum.mock.calls.length).toBe(once + 1);
    // süreci kendimiz kapattık: beklenmeyen sayılmaz, lisans hatası kalır
    expect(k.surec.komutlar().some((c) => c.komut === "cik")).toBe(true);
    k.surec.cik(0);
    expect(k.y.durum().hata).toBe("cihaz_dustu");
  });

  it.each([
    ["paketli", "paketli"],
    ["yasakli", "hile"],
  ] as const)("sunucu_%s_derse_surec_kalir_engel_ve_bekler", async (kod, tur) => {
    const k = kur({ oyunlar: [oyun(STEAM, { ad: "Oyun X", kimlik: "oyun-x" })] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "hata", kod });
    k.surec.olay({ olay: "durdu", neden: "lisans" });
    const d = k.y.durum();
    expect(d.calisma).toBe("bekleniyor");
    expect(d.engel).toEqual({ tur, oyunAdi: "Oyun X", kimlik: "oyun-x" });
    expect(d.hata).toBeUndefined();
    expect(k.surec.komutlar().some((c) => c.komut === "cik")).toBe(false);
    expect(k.sunucu.durum).toHaveBeenCalledTimes(1); // yenile çağrılmadı
    // yoklama durdu
    await vi.advanceTimersByTimeAsync(20000);
    expect(k.acik).not.toHaveBeenCalled();
    // aynı oyun önde kaldıkça yeniden başlamaz
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(1);
    expect(k.y.durum().engel?.tur).toBe(tur);
    // başka pencere öne gelince engel kalkar, oyun dönünce yeniden denenir
    k.surec.olay({ olay: "onplan", yol: "C:\\Windows\\explorer.exe" });
    expect(k.y.durum().engel).toBeUndefined();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(2);
    // süreç hiç kapanmadı
    k.surec.olay({ olay: "basladi" });
    expect(k.y.durum().calisma).toBe("cevriliyor");
  });

  it.each(["lisans_yok", "cihaz_dustu", "suresi_bitti"])("durdu_lisans_%s_kapatir", async (kod) => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "hata", kod });
    k.surec.olay({ olay: "durdu", neden: "lisans" });
    expect(k.y.durum().calisma).toBe("kapali");
    expect(k.y.durum().hata).toBe(kod);
    expect(k.y.durum().engel).toBeUndefined();
    expect(k.surec.komutlar().some((c) => c.komut === "cik")).toBe(true);
    await vi.advanceTimersByTimeAsync(0);
    expect(k.sunucu.durum).toHaveBeenCalledTimes(2);
  });

  it("eski_engel_oyun_disi_pencerede_kalkar", async () => {
    const k = kur({ oyunlar: [oyun(STEAM, { ad: "Paketli", durum: "destekleniyor" })] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.y.durum().engel?.tur).toBe("paketli");
    k.surec.olay({ olay: "onplan", yol: "C:\\Windows\\explorer.exe" });
    expect(k.y.durum().engel).toBeUndefined();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    expect(k.y.durum().engel?.tur).toBe("paketli");
    k.surec.olay({ olay: "onplan", yol: null });
    expect(k.y.durum().engel).toBeUndefined();
  });

  it("kapat_sonrasi_eslesme_yoklamasi_ve_surec_baslamaz", async () => {
    const k = kur({ durum: () => ({ tamam: true, veri: { durum: "lisanssiz" } }) });
    let coz: (v: SunucuSonucu<{ numara: string; bitis: number }>) => void = () => {};
    k.sunucu.eslestir.mockImplementationOnce(() => new Promise((r) => (coz = r)));
    const p = k.y.eslestirBaslat();
    k.y.kapat();
    coz({ tamam: true, veri: { numara: "1", bitis: Date.now() + 3600_000 } });
    await p;
    await vi.advanceTimersByTimeAsync(20000);
    expect(k.sunucu.durum).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
    k.y.calistir();
    expect(k.baslat).not.toHaveBeenCalled();
  });

  it("kapat_sonrasi_yenile_yoklama_baslatmaz", async () => {
    const k = kur({ durum: () => ({ tamam: true, veri: { durum: "lisanssiz" } }) });
    k.depo.eslesme = { numara: "9", bitis: Date.now() + 3600_000 };
    const p = k.y.yenile();
    k.y.kapat();
    await p;
    const sayi = k.sunucu.durum.mock.calls.length;
    await vi.advanceTimersByTimeAsync(20000);
    expect(k.sunucu.durum.mock.calls.length).toBe(sayi);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("durdu_hata_bekler", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "basladi" });
    k.surec.olay({ olay: "hata", kod: "yapilandirma" });
    k.surec.olay({ olay: "durdu", neden: "hata" });
    expect(k.y.durum().calisma).toBe("bekleniyor");
    expect(k.y.durum().hata).toBe("yapilandirma");
    // yoklama durdu; oyun önde kaldığı için 3 sn sonra yeniden denenir
    await vi.advanceTimersByTimeAsync(2999);
    expect(k.acik).not.toHaveBeenCalled();
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(2);
    // aynı hata tekrar ederse yine beklenir (sıkı döngü yok)
    k.surec.olay({ olay: "hata", kod: "yapilandirma" });
    k.surec.olay({ olay: "durdu", neden: "hata" });
    await vi.advanceTimersByTimeAsync(2999);
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(2);
    await vi.advanceTimersByTimeAsync(1);
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(3);
  });

  it("durdu_hata_sonrasi_baska_pencere_onde_ise_baslamaz", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "onplan", yol: "C:\\Windows\\explorer.exe" });
    k.surec.olay({ olay: "durdu", neden: "hata" });
    await vi.advanceTimersByTimeAsync(10000);
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(1);
    expect(k.y.durum().calisma).toBe("bekleniyor");
  });

  it("durdur_bekleyen_yeniden_degerlendirmeyi_iptal_eder", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "durdu", neden: "hata" });
    k.y.durdur();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("baslaticidan_acilan_oyun_one_gelince_oturum_gecer", async () => {
    const k = kur({ oyunlar: [oyun(STEAM, { appId: 7 })] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\Launcher.exe" });
    k.surec.olay({ olay: "basladi" });
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\Binaries\\Oyun-Win64-Shipping.exe" });
    const komutlar = k.surec.komutlar();
    expect(komutlar.map((c) => c.komut)).toEqual(["izle", "basla", "dur", "basla"]);
    expect(komutlar[3]?.sureciAdi).toBe("Oyun-Win64-Shipping.exe");
    expect(k.y.durum().calisma).toBe("bekleniyor");
    // yoklama yeni süreci izler
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.acik).toHaveBeenCalledTimes(1);
    expect(k.acik).toHaveBeenLastCalledWith("Oyun-Win64-Shipping.exe");
  });

  it("oturum_surerken_oyun_disi_ya_da_engelli_pencere_oturumu_bitirmez", async () => {
    const PAKETLI = "C:\\Steam\\steamapps\\common\\Paketli";
    const k = kur({ oyunlar: [oyun(STEAM), oyun(PAKETLI, { durum: "destekleniyor" })] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "onplan", yol: "C:\\Windows\\explorer.exe" });
    k.surec.olay({ olay: "onplan", yol: PAKETLI + "\\p.exe" });
    k.surec.olay({ olay: "onplan", yol: null });
    expect(k.surec.komutlar().map((c) => c.komut)).toEqual(["izle", "basla"]);
  });

  it("surec_kapaninca_kapali_kalirsa_yeniden_baslamaz", async () => {
    let acik = true;
    const k = kur({ oyunlar: [oyun(STEAM)], acik: () => acik });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    acik = false;
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.surec.komutlar().map((c) => c.komut)).toEqual(["izle", "basla", "dur"]);
    await vi.advanceTimersByTimeAsync(20000);
    expect(k.surec.komutlar().map((c) => c.komut)).toEqual(["izle", "basla", "dur"]);
  });

  it("surec_kapanip_ayni_yolla_yeniden_acilirsa_baslar", async () => {
    let acik = true;
    const k = kur({ oyunlar: [oyun(STEAM)], acik: () => acik });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    acik = false;
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.surec.komutlar().map((c) => c.komut)).toEqual(["izle", "basla", "dur"]);
    acik = true; // oyun kendini yeniden başlattı, hâlâ önde
    await vi.advanceTimersByTimeAsync(3000);
    expect(k.surec.komutlar().map((c) => c.komut)).toEqual(["izle", "basla", "dur", "basla"]);
  });

  it("surec_kapaninca_ondeki_baska_oyun_3_sn_sonra_baslar", async () => {
    const DIGER = "C:\\Oyunlar\\Diger";
    let acik = true;
    const k = kur({ oyunlar: [oyun(STEAM), oyun(DIGER, { ad: "Diğer", durum: "hile-korumali" })], acik: () => acik });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    // Öne hile korumalı bir oyun geldi: oturum sürer (başlatılamaz), ama son ön plan o.
    k.surec.olay({ olay: "onplan", yol: DIGER + "\\d.exe" });
    acik = false;
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.surec.komutlar().map((c) => c.komut)).toEqual(["izle", "basla", "dur"]);
    await vi.advanceTimersByTimeAsync(3000);
    // yeniden değerlendirme öndeki pencereye bakar: hile korumalı → engel gösterilir, başlamaz
    expect(k.y.durum().engel).toEqual({ tur: "hile", oyunAdi: "Diğer" });
    expect(k.surec.komutlar().filter((c) => c.komut === "basla")).toHaveLength(1);
  });

  it("durdur_dur_ve_cik_kapali", async () => {
    const k = kur({ oyunlar: [oyun(STEAM)] });
    await k.y.yenile();
    k.y.calistir();
    k.surec.olay({ olay: "onplan", yol: STEAM + "\\x.exe" });
    k.surec.olay({ olay: "basladi" });
    k.y.durdur();
    const komutlar = k.surec.komutlar().map((c) => c.komut);
    expect(komutlar.slice(-2)).toEqual(["dur", "cik"]);
    expect(k.y.durum().calisma).toBe("kapali");
    k.surec.cik(0);
    expect(k.y.durum().hata).toBeUndefined();
  });

  it("kapat_sureci_oldurur", async () => {
    const k = kur();
    await k.y.yenile();
    k.y.calistir();
    await k.y.eslestirBaslat();
    k.surec.olay({ olay: "uyari", kod: "hiz" });
    expect(vi.getTimerCount()).toBe(2); // eşleşme yoklaması + uyarı sayacı
    k.y.kapat();
    expect(k.surec.olduruldu).toBe(true);
    expect(k.y.durum().calisma).toBe("kapali");
    expect(vi.getTimerCount()).toBe(0);
  });

  it("dinle_degisiklikleri_bildirir", async () => {
    const k = kur();
    const gorulen: string[] = [];
    const birak = k.y.dinle((d) => gorulen.push(d.calisma));
    await k.y.yenile();
    k.y.calistir();
    birak();
    k.y.durdur();
    expect(gorulen).toContain("bekleniyor");
    expect(gorulen[gorulen.length - 1]).toBe("bekleniyor");
  });

  it("eslesme_lisans_teslim_eder", async () => {
    let teslim = false;
    const k = kur({
      durum: (_l, n) =>
        n
          ? { tamam: true, veri: teslim ? { durum: "etkin", tur: "ust", kalan: 50000, lisans: "K" } : { durum: "lisanssiz" } }
          : { tamam: true, veri: { durum: "lisanssiz" } },
    });
    k.depo.lisans = null;
    expect(await k.y.eslestirBaslat()).toEqual({});
    expect(k.y.durum().eslesme?.numara).toBe("123456");
    expect(k.depo.eslesme?.numara).toBe("123456");
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.sunucu.durum).toHaveBeenCalledWith(null, "123456");
    expect(k.depo.lisans).toBeNull();
    teslim = true;
    await vi.advanceTimersByTimeAsync(5000);
    expect(k.depo.lisans).toBe("K");
    expect(k.depo.eslesme).toBeNull();
    expect(k.y.durum().eslesme).toBeUndefined();
    expect(k.y.durum().lisans?.durum).toBe("etkin");
    expect(k.y.durum().lisans).not.toHaveProperty("lisans");
    // yoklama durdu
    const sayi = k.sunucu.durum.mock.calls.length;
    await vi.advanceTimersByTimeAsync(20000);
    expect(k.sunucu.durum.mock.calls.length).toBe(sayi);
  });

  it("yenile_eslesme_varsa_numarayla", async () => {
    const k = kur({ durum: (_l, n) => (n ? { tamam: true, veri: { durum: "etkin", lisans: "K2" } } : ETKIN) });
    k.depo.lisans = null;
    k.depo.eslesme = { numara: "999", bitis: Date.now() + 60_000 };
    await k.y.yenile();
    expect(k.sunucu.durum).toHaveBeenCalledWith(null, "999");
    expect(k.depo.lisans).toBe("K2");
    expect(k.depo.eslesme).toBeNull();
  });

  it("suresi_dolmus_eslesme_silinir", async () => {
    const k = kur();
    k.depo.eslesme = { numara: "999", bitis: Date.now() - 1 };
    await k.y.yenile();
    expect(k.sunucu.durum).not.toHaveBeenCalledWith(null, "999");
    expect(k.depo.eslesme).toBeNull();
    expect(k.y.durum().eslesme).toBeUndefined();
  });

  it("sunucu_ulasilamaz_lisans_null_sunucuHatasi", async () => {
    const k = kur({ durum: () => ({ tamam: false, hata: "cevrimdisi" }) });
    await k.y.yenile();
    const d = k.y.durum();
    expect(d.lisans).toBeNull();
    expect(d.sunucuHatasi).toBe("cevrimdisi");
    expect(d.yukleniyor).toBe(false);
  });

  it("deneme_lisansi_depoya_yazar", async () => {
    const k = kur({ durum: () => ({ tamam: true, veri: { durum: "etkin", tur: "deneme", kalan: 2000 } }) });
    k.depo.lisans = null;
    k.sunucu.deneme.mockResolvedValueOnce({ tamam: true, veri: { lisans: "D", durum: "etkin", tur: "deneme", kalan: 2000 } });
    expect(await k.y.deneme()).toEqual({});
    expect(k.depo.lisans).toBe("D");
    expect(k.y.durum().lisans?.tur).toBe("deneme");
    expect(k.y.durum().lisans).not.toHaveProperty("lisans");
    expect(await k.y.deneme()).toEqual({ hata: "deneme_kullanildi" });
  });

  it("depo_yazma_hatasi_cokertmez", async () => {
    const k = kur();
    k.depo.lisans = null;
    k.depo.yaz = () => {
      throw new Error("EACCES");
    };
    k.depo.eslesmeYaz = () => {
      throw new Error("EACCES");
    };
    k.sunucu.deneme.mockResolvedValueOnce({ tamam: true, veri: { lisans: "D", durum: "etkin", tur: "deneme" } });
    expect((await k.y.deneme()).hata).toBe("kayit");
    expect(await k.y.eslestirBaslat()).toEqual({});
  });
});
