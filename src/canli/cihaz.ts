// Cihaz özeti: canli.exe ile birebir aynı olmalı (canli/Canli/CihazKimligi.cs).

import { execFile } from "node:child_process";
import { createHash } from "node:crypto";

export const KAYIT_YOLU = String.raw`HKLM\SOFTWARE\Microsoft\Cryptography`;

export type KomutCalistirici = (
  komut: string,
  argumanlar: string[],
  bitti: (hata: Error | null, cikti: string) => void,
) => void;

const varsayilanCalistirici: KomutCalistirici = (komut, argumanlar, bitti) => {
  execFile(komut, argumanlar, { windowsHide: true, timeout: 5000 }, (hata, cikti) => bitti(hata, String(cikti)));
};

export function cihazOzeti(machineGuid: string): string {
  return createHash("sha256").update("turkcesivar:" + machineGuid, "utf8").digest("hex");
}

export function makineGuidOku(calistir: KomutCalistirici = varsayilanCalistirici): Promise<string> {
  return new Promise((coz, red) => {
    calistir("reg", ["query", KAYIT_YOLU, "/v", "MachineGuid"], (hata, cikti) => {
      if (hata) return red(hata);
      const m = /MachineGuid\s+REG_SZ\s+(\S+)/i.exec(cikti);
      if (!m) return red(new Error("MachineGuid okunamadı"));
      coz(m[1]!);
    });
  });
}
