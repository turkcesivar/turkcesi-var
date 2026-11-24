// Canlı Çeviri: ana süreç tarafı. canli.exe'yi başlatır, süreç açık mı diye bakar, lisansı safeStorage ile saklar.
// Bu dosya electron'u içe aktarmaz (test edilebilsin diye); safeStorage dışarıdan verilir.

import { execFile, spawn } from "node:child_process";
import { join } from "node:path";
import type { CanliSurec } from "../../src/canli/yonetici.js";
import type { Sifreleyici } from "../../src/canli/lisans-deposu.js";

export function canliExeYolu(gelistirme: boolean, uygulamaYolu: string, kaynaklar: string): string {
  return gelistirme ? join(uygulamaYolu, "canli", "cikti", "canli.exe") : join(kaynaklar, "canli", "canli.exe");
}

/** canli.exe'yi başlatır; başlatma hatası (dosya yok vb.) kodsuz (null) çıkış olarak bildirilir. */
export function canliSurecBaslat(exeYolu: string): CanliSurec {
  const cocuk = spawn(exeYolu, [], { stdio: "pipe", windowsHide: true });
  cocuk.stdout.setEncoding("utf8");
  cocuk.stdin.on("error", () => {
    // süreç kapanmışken yazma hatası: çıkış olayı zaten bildirilir
  });
  let bitti = false;
  return {
    yaz: (satir) => {
      if (!cocuk.stdin.destroyed && cocuk.stdin.writable) cocuk.stdin.write(satir + "\n", "utf8");
    },
    satirlar: (f) => {
      cocuk.stdout.on("data", (p: string) => f(p));
    },
    cikinca: (f) => {
      const bildir = (kod: number | null) => {
        if (bitti) return;
        bitti = true;
        f(kod);
      };
      cocuk.on("exit", (kod) => bildir(kod));
      cocuk.on("error", () => bildir(null));
    },
    oldur: () => {
      cocuk.kill();
    },
  };
}

/** Süreç adı çalışıyor mu (asenkron: arayüz donmaz). Hata ya da zaman aşımında false. */
export function surecAcikMiAsenkron(ad: string): Promise<boolean> {
  return new Promise((coz) => {
    execFile("tasklist", ["/FI", `IMAGENAME eq ${ad}`, "/NH"], { windowsHide: true, timeout: 5000 }, (hata, cikti) => {
      if (hata) return coz(false);
      coz(String(cikti).toLowerCase().includes(ad.toLowerCase()));
    });
  });
}

export interface SafeStorageBenzeri {
  isEncryptionAvailable(): boolean;
  encryptString(s: string): Buffer;
  decryptString(b: Buffer): string;
}

export function gercekSifreleyici(s: SafeStorageBenzeri): Sifreleyici {
  return {
    kullanilabilir: () => s.isEncryptionAvailable(),
    sifrele: (m) => s.encryptString(m),
    coz: (b) => s.decryptString(b),
  };
}
