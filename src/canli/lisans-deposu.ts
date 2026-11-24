// Lisans anahtarı diskte yalnızca şifreli durur (Windows safeStorage); eşleştirme numarası gizli değildir.
// Dosya: %APPDATA%\turkcesi-var\canli-lisans.json  { lisans?: base64(şifreli), eslesme? }

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";

export interface Sifreleyici {
  kullanilabilir(): boolean;
  sifrele(m: string): Buffer;
  coz(b: Buffer): string;
}

export interface Eslesme {
  numara: string;
  bitis: number;
}

interface Icerik {
  lisans?: string;
  eslesme?: Eslesme;
}

export class LisansDeposu {
  constructor(
    private readonly dosya: string,
    private readonly sifreleyici: Sifreleyici,
  ) {}

  private okuIcerik(): Icerik {
    try {
      const j: unknown = JSON.parse(readFileSync(this.dosya, "utf8"));
      return typeof j === "object" && j !== null && !Array.isArray(j) ? (j as Icerik) : {};
    } catch {
      return {};
    }
  }

  private yazIcerik(i: Icerik): void {
    mkdirSync(dirname(this.dosya), { recursive: true });
    writeFileSync(this.dosya, JSON.stringify(i, null, 2), "utf8");
  }

  oku(): string | null {
    try {
      const b64 = this.okuIcerik().lisans;
      if (typeof b64 !== "string" || !b64 || !this.sifreleyici.kullanilabilir()) return null;
      const lisans = this.sifreleyici.coz(Buffer.from(b64, "base64"));
      return lisans || null;
    } catch {
      return null;
    }
  }

  // Şifreleme yoksa lisans hiçbir yere (düz olarak da) yazılmaz; uygulama lisanssız davranır.
  yaz(lisans: string): void {
    if (!this.sifreleyici.kullanilabilir()) return;
    const i = this.okuIcerik();
    i.lisans = this.sifreleyici.sifrele(lisans).toString("base64");
    this.yazIcerik(i);
  }

  sil(): void {
    const i = this.okuIcerik();
    if (!("lisans" in i)) return;
    delete i.lisans;
    this.yazIcerik(i);
  }

  eslesmeOku(): Eslesme | null {
    const e = this.okuIcerik().eslesme;
    return e && typeof e.numara === "string" && typeof e.bitis === "number" ? e : null;
  }

  eslesmeYaz(e: Eslesme | null): void {
    const i = this.okuIcerik();
    if (e) i.eslesme = e;
    else delete i.eslesme;
    this.yazIcerik(i);
  }
}
