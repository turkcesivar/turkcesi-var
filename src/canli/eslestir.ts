// Ön plandaki sürecin yolunu kütüphanedeki oyunlarla eşleştirir.

import type { ArayuzOyunu } from "../servis/api.js";

// Küçük harf, "/" → "\", sonda tek "\" (klasör sınırında önek eşleşmesi için).
function klasorBicimi(yol: string): string {
  return yol.toLowerCase().replace(/\//g, "\\").replace(/\\+$/, "") + "\\";
}

/** Yol bir oyunun klasörünün içindeyse o oyun; birden çoğuna uyarsa en uzun (en içteki) klasör. */
export function oyunBul(yol: string | null, oyunlar: ArayuzOyunu[]): ArayuzOyunu | undefined {
  if (!yol) return undefined;
  const y = yol.toLowerCase().replace(/\//g, "\\");
  let bulunan: ArayuzOyunu | undefined;
  let uzunluk = -1;
  for (const o of oyunlar) {
    if (!o.klasor) continue;
    const k = klasorBicimi(o.klasor);
    if (k === "\\") continue;
    if (y.startsWith(k) && k.length > uzunluk) {
      bulunan = o;
      uzunluk = k.length;
    }
  }
  return bulunan;
}

function klasorAdi(klasor: string): string {
  const parcalar = klasor.replace(/\//g, "\\").replace(/\\+$/, "").split("\\");
  return parcalar[parcalar.length - 1] ?? "";
}

/** canli.exe'ye giden oyun kimliği: steam:<appId> | epic:<klasör adı> | elle:<klasör adı>. */
export function oyunKimligi(o: ArayuzOyunu): string {
  const platform = o.platform ?? (o.appId !== undefined ? "steam" : "elle");
  if (platform === "steam" && o.appId !== undefined) return `steam:${o.appId}`;
  if (platform === "epic") return `epic:${klasorAdi(o.klasor)}`;
  return `elle:${klasorAdi(o.klasor)}`;
}
