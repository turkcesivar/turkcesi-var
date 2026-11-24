// Cihaz özeti: canli.exe ile birebir aynı olmalı (canli/Canli/CihazKimligi.cs).

import { execFile } from "node:child_process";
import { createHash } from "node:crypto";

export function cihazOzeti(machineGuid: string): string {
  return createHash("sha256").update("turkcesivar:" + machineGuid, "utf8").digest("hex");
}

export function makineGuidOku(): Promise<string> {
  return new Promise((coz, red) => {
    execFile(
      "reg",
      ["query", "HKLM\SOFTWARE\Microsoft\Cryptography", "/v", "MachineGuid"],
      { windowsHide: true },
      (hata, cikti) => {
        if (hata) return red(hata);
        const m = /MachineGuid\s+REG_SZ\s+(\S+)/i.exec(cikti);
        if (!m) return red(new Error("MachineGuid okunamadı"));
        coz(m[1]!);
      },
    );
  });
}
