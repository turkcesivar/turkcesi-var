import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { DENETIM_SURESI_MS, HAZIR_SONRASI_MS, ocrDenetle } from "../../src/canli/ocr-denetimi.js";
import type { CanliSurec } from "../../src/canli/yonetici.js";

function sahteSurec() {
  let veri: (s: string) => void = () => {};
  let cikis: (k: number | null) => void = () => {};
  const yazilan: string[] = [];
  const s: CanliSurec & { yazilan: string[]; olduruldu: boolean; gonder(p: string): void; kapan(): void } = {
    yazilan,
    olduruldu: false,
    yaz: (x) => yazilan.push(x),
    satirlar: (f) => (veri = f),
    cikinca: (f) => (cikis = f),
    oldur: () => {
      s.olduruldu = true;
    },
    gonder: (p) => veri(p),
    kapan: () => cikis(0),
  };
  return s;
}

describe("ocrDenetle", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("hazir_ve_hata_yoksa_yuklu", async () => {
    const s = sahteSurec();
    const p = ocrDenetle(() => s);
    s.gonder('{"olay":"ha');
    s.gonder('zir"}\n');
    await vi.advanceTimersByTimeAsync(HAZIR_SONRASI_MS + 10);
    expect(await p).toBe(true);
    expect(s.yazilan).toEqual(['{"komut":"cik"}']);
    expect(s.olduruldu).toBe(true);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("bosluklu_hazir_satiri_da_taninir", async () => {
    const s = sahteSurec();
    const p = ocrDenetle(() => s);
    s.gonder('{"olay": "hazir"}\n');
    await vi.advanceTimersByTimeAsync(HAZIR_SONRASI_MS + 10);
    expect(await p).toBe(true);
  });

  it("ocr_dil_yok_gelirse_yuklu_degil", async () => {
    const s = sahteSurec();
    const p = ocrDenetle(() => s);
    s.gonder('{"olay":"hazir"}\n{"olay":"hata","kod":"ocr_dil_yok"}\n');
    expect(await p).toBe(false);
    expect(s.olduruldu).toBe(true);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("yanit_gelmezse_null", async () => {
    const s = sahteSurec();
    const p = ocrDenetle(() => s);
    await vi.advanceTimersByTimeAsync(DENETIM_SURESI_MS + 10);
    expect(await p).toBeNull();
  });

  it("surec_erken_kapanirsa_null", async () => {
    const s = sahteSurec();
    const p = ocrDenetle(() => s);
    s.kapan();
    expect(await p).toBeNull();
  });

  it("baslatilamazsa_null", async () => {
    const p = ocrDenetle(() => {
      throw new Error("exe yok");
    });
    expect(await p).toBeNull();
    expect(vi.getTimerCount()).toBe(0);
  });
});
