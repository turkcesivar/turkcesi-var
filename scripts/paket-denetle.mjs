// Paketlenmiş uygulamada (win-unpacked) her modülün bağımlılıkları da var mı? Eksikse uygulama açılışta
// "Cannot find module" ile çöker (0.6.0'da fs-extra eksik kaldı). Kullanım: node scripts/paket-denetle.mjs [resources klasörü]
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const pkg = JSON.parse(readFileSync(new URL("../package.json", import.meta.url), "utf8"));
const kaynaklar = process.argv[2] ?? join("dist", pkg.version, "win-unpacked", "resources");
const app = join(kaynaklar, "app");
if (!existsSync(join(app, "package.json"))) {
  console.error(`Paket bulunamadı: ${app}`);
  process.exit(2);
}

if (!existsSync(join(kaynaklar, "canli", "canli.exe"))) {
  console.error(`canli.exe pakette yok: ${join(kaynaklar, "canli", "canli.exe")}`);
  process.exit(2);
}

// Node'un çözümleme kuralı: modülün kendi node_modules'ı, sonra üst klasörlerinkiler.
function bul(ad, klasor) {
  for (let k = klasor; k.startsWith(app); k = join(k, "..", "..")) {
    const aday = join(k, "node_modules", ad);
    if (existsSync(join(aday, "package.json"))) return aday;
    if (k === app) break;
  }
  return undefined;
}

const eksik = [];
const gezildi = new Set();
function gez(klasor) {
  if (gezildi.has(klasor)) return;
  gezildi.add(klasor);
  const p = JSON.parse(readFileSync(join(klasor, "package.json"), "utf8"));
  for (const ad of Object.keys(p.dependencies ?? {})) {
    const yer = bul(ad, klasor);
    if (yer) gez(yer);
    else eksik.push(`${p.name ?? "(uygulama)"} → ${ad}`);
  }
}
gez(app);

const say = existsSync(join(app, "node_modules")) ? readdirSync(join(app, "node_modules")).length : 0;
if (eksik.length) {
  console.error(`PAKET EKSİK (${say} modül): ${eksik.join(", ")}`);
  process.exit(1);
}
console.log(`Paket tamam: ${gezildi.size - 1} modül, eksik bağımlılık yok.`);
