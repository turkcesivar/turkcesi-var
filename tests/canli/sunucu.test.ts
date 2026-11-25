import { createHash } from "node:crypto";
import { describe, expect, it } from "vitest";
import { cihazOzeti, makineGuidOku } from "../../src/canli/cihaz.js";
import { CanliSunucu } from "../../src/canli/sunucu.js";

const TABAN = "https://api.turkcesivar.com";

function yanit(durum: number, govde: unknown): Response {
  return new Response(JSON.stringify(govde), { status: durum, headers: { "content-type": "application/json" } });
}

function sahte(cevap: () => Promise<Response>) {
  const cagrilar: { url: string; init: RequestInit }[] = [];
  const f = (async (url: string, init: RequestInit) => {
    cagrilar.push({ url, init });
    return cevap();
  }) as unknown as typeof fetch;
  return { f, cagrilar };
}

describe("cihazOzeti", () => {
  it("cihaz_ozeti_canli_exe_ile_ayni", () => {
    const beklenen = createHash("sha256").update("turkcesivar:ABC", "utf8").digest("hex");
    expect(cihazOzeti("ABC")).toBe(beklenen);
    expect(cihazOzeti("ABC")).toBe("dbc4ac175eb51cb27b7620a14b04e998b90e28a250bb48f45a1f03b0cf9766c1");
    expect(cihazOzeti("ABC")).toMatch(/^[0-9a-f]{64}$/);
  });
});

describe("makineGuidOku", () => {
  it("kayit_yolu_dogru_ve_cikti_ayrisir", async () => {
    let arg: string[] = [];
    const guid = await makineGuidOku((komut, a, bitti) => {
      expect(komut).toBe("reg");
      arg = a;
      bitti(null, String.raw`
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography
    MachineGuid    REG_SZ    1234-abcd
`);
    });
    expect(arg).toEqual(["query", String.raw`HKLM\SOFTWARE\Microsoft\Cryptography`, "/v", "MachineGuid"]);
    expect(guid).toBe("1234-abcd");
  });

  it("hata_ve_bozuk_cikti_reddeder", async () => {
    await expect(makineGuidOku((_k, _a, b) => b(new Error("x"), ""))).rejects.toThrow();
    await expect(makineGuidOku((_k, _a, b) => b(null, "yok"))).rejects.toThrow();
  });
});

describe("CanliSunucu", () => {
  it("durum_bearer_ve_cihaz_basligi", async () => {
    const { f, cagrilar } = sahte(async () => yanit(200, { durum: "etkin", tur: "destekci" }));
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    const r = await s.durum("L");
    expect(r).toEqual({ tamam: true, veri: { durum: "etkin", tur: "destekci" } });
    expect(cagrilar[0]!.url).toBe(`${TABAN}/durum`);
    const b = cagrilar[0]!.init.headers as Record<string, string>;
    expect(b["X-Cihaz"]).toBe("C1");
    expect(b["Authorization"]).toBe("Bearer L");
    expect(cagrilar[0]!.init.method ?? "GET").toBe("GET");
  });

  it("durum_numara_ile", async () => {
    const { f, cagrilar } = sahte(async () => yanit(200, { durum: "etkin", lisans: "LIS" }));
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    const r = await s.durum(null, "7K2M");
    expect(cagrilar[0]!.url).toBe(`${TABAN}/durum?numara=7K2M`);
    expect((cagrilar[0]!.init.headers as Record<string, string>)["Authorization"]).toBeUndefined();
    expect(r.tamam && r.veri.lisans).toBe("LIS");
  });

  it("deneme_kullanildi_hatasi", async () => {
    const { f, cagrilar } = sahte(async () => yanit(409, { hata: "deneme_kullanildi" }));
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    expect(await s.deneme()).toEqual({ tamam: false, hata: "deneme_kullanildi" });
    expect(cagrilar[0]!.init.method).toBe("POST");
    expect(cagrilar[0]!.url).toBe(`${TABAN}/deneme`);
  });

  it("eslestir_basarili", async () => {
    const { f } = sahte(async () => yanit(200, { numara: "7K2M", bitis: 123 }));
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    expect(await s.eslestir()).toEqual({ tamam: true, veri: { numara: "7K2M", bitis: 123 } });
  });

  it("cihaz_ekle_bearer_ve_govdeyle_post", async () => {
    const { f, cagrilar } = sahte(async () => yanit(200, { tamam: true }));
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    expect(await s.cihazEkle("LIS", "TV-7K2M")).toEqual({ tamam: true, veri: { tamam: true } });
    expect(cagrilar[0]!.url).toBe(`${TABAN}/cihaz-ekle`);
    expect(cagrilar[0]!.init.method).toBe("POST");
    const h = cagrilar[0]!.init.headers as Record<string, string>;
    expect(h["Authorization"]).toBe("Bearer LIS");
    expect(h["content-type"]).toBe("application/json");
    expect(JSON.parse(String(cagrilar[0]!.init.body))).toEqual({ numara: "TV-7K2M" });
  });

  it("cihaz_ekle_hata_kodu", async () => {
    const { f } = sahte(async () => yanit(409, { hata: "numara_bagli" }));
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    expect(await s.cihazEkle("LIS", "TV-7K2M")).toEqual({ tamam: false, hata: "numara_bagli" });
  });

  it("bozuk_yanit_gecici_hata", async () => {
    const { f } = sahte(async () => new Response("<html>", { status: 502 }));
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    expect(await s.durum("L")).toEqual({ tamam: false, hata: "gecici_hata" });
  });

  it("ag_hatasi_cevrimdisi", async () => {
    const { f } = sahte(async () => {
      throw new TypeError("fetch failed");
    });
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f });
    expect(await s.durum("L")).toEqual({ tamam: false, hata: "cevrimdisi" });
  });

  it("zaman_asimi_cevrimdisi", async () => {
    const f = ((_u: string, init: RequestInit) =>
      new Promise((_, red) => {
        init.signal?.addEventListener("abort", () => red(new DOMException("abort", "AbortError")));
      })) as unknown as typeof fetch;
    const s = new CanliSunucu({ taban: TABAN, cihaz: "C1", fetch: f, zamanAsimiMs: 50 });
    expect(await s.eslestir()).toEqual({ tamam: false, hata: "cevrimdisi" });
  });
});
