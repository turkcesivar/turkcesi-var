import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { LisansDeposu, type Sifreleyici } from "../../src/canli/lisans-deposu.js";

const sahteSifre = (var_ = true): Sifreleyici => ({
  kullanilabilir: () => var_,
  sifrele: (m) => Buffer.from(m, "utf8").reverse(),
  coz: (b) => Buffer.from(b).reverse().toString("utf8"),
});

let klasor: string;
let dosya: string;
beforeEach(() => {
  klasor = mkdtempSync(join(tmpdir(), "canli-lisans-"));
  dosya = join(klasor, "alt", "canli-lisans.json");
});
afterEach(() => rmSync(klasor, { recursive: true, force: true }));

describe("LisansDeposu", () => {
  it("lisans_sifreli_yazilir_okunur", () => {
    const d = new LisansDeposu(dosya, sahteSifre());
    d.yaz("GIZLI-ANAHTAR-123");
    expect(readFileSync(dosya, "utf8")).not.toContain("GIZLI-ANAHTAR-123");
    expect(d.oku()).toBe("GIZLI-ANAHTAR-123");
    d.sil();
    expect(d.oku()).toBeNull();
  });

  it("bozuk_dosya_null", () => {
    const d = new LisansDeposu(dosya, sahteSifre());
    d.yaz("x");
    writeFileSync(dosya, "{bozuk");
    expect(d.oku()).toBeNull();
    expect(d.eslesmeOku()).toBeNull();
  });

  it("cozme_hatasi_null", () => {
    const s = sahteSifre();
    const d = new LisansDeposu(dosya, { ...s, coz: () => { throw new Error("kötü"); } });
    d.yaz("x");
    expect(d.oku()).toBeNull();
  });

  it("sifreleme_yoksa_null", () => {
    const d = new LisansDeposu(dosya, sahteSifre(false));
    d.yaz("GIZLI-ANAHTAR-123");
    expect(d.oku()).toBeNull();
    let icerik = "";
    try { icerik = readFileSync(dosya, "utf8"); } catch { /* dosya hiç yazılmamış olabilir */ }
    expect(icerik).not.toContain("GIZLI-ANAHTAR-123");
  });

  it("eslesme_yaz_oku_sil", () => {
    const d = new LisansDeposu(dosya, sahteSifre());
    expect(d.eslesmeOku()).toBeNull();
    d.yaz("L");
    d.eslesmeYaz({ numara: "7K2M", bitis: 99 });
    expect(d.eslesmeOku()).toEqual({ numara: "7K2M", bitis: 99 });
    expect(d.oku()).toBe("L");
    d.eslesmeYaz(null);
    expect(d.eslesmeOku()).toBeNull();
    expect(d.oku()).toBe("L");
  });
});
