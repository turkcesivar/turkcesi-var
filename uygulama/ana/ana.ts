// Electron ana süreci (oyuncu sürümü): pencere, güvenli köprü (IPC), oyun bulma/elle seçme,
// oyun görselleri, arka plan servisi ve otomatik güncelleme.

import { app, BrowserWindow, dialog, ipcMain, net, protocol, safeStorage, shell } from "electron";
import { realpathSync, writeFileSync } from "node:fs";
import { dirname, join, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { epicOyunlariTara } from "../../src/oyunlar/epic.js";
import { steamKimligiAra } from "../../src/oyunlar/steam-magaza.js";
import { elleKlasorEkle, elleKlasorleriOku, klasorTani, secimHatasi } from "../../src/oyunlar/elle.js";
import { gorselleriBul, kutuphaneleriOku, oyunlariTara, steamCdnGorselleri, steamKlasoruBul, type BulunanOyun } from "../../src/oyunlar/steam.js";
import { OYUNLAR, uygulamaKlasoru, type OyunTanimi } from "../../src/oyunlar/tanim.js";
import { SERVIS_YONTEMLERI, type ArayuzOyunu, type GuncellemeDurumu, type TaramaSonucu } from "../../src/servis/api.js";
import { servisIstemcisi, type ServisIstemcisi } from "../../src/servis/is-parcacigi-istemci.js";
import workerYolu from "../../src/servis/is-parcacigi.ts?modulePath";
import { guncellemeyiBaslat } from "./guncelleme.js";
import { cihazOzeti, makineGuidOku } from "../../src/canli/cihaz.js";
import { LisansDeposu } from "../../src/canli/lisans-deposu.js";
import { CanliSunucu } from "../../src/canli/sunucu.js";
import { CanliYonetici } from "../../src/canli/yonetici.js";
import { canliExeYolu, canliSurecBaslat, gercekSifreleyici, surecAcikMiAsenkron } from "./canli.js";

const burasi = dirname(fileURLToPath(import.meta.url));
const KLASOR = uygulamaKlasoru();
const GORSEL = "tv-gorsel";
const gelistirme = !app.isPackaged;
/** Uygulamayla gelen Türkçe paketler: kurulumda resources\paketler, geliştirmede depo kökündeki paketler\. */
const PAKET_KLASORU = gelistirme ? join(app.getAppPath(), "paketler") : join(process.resourcesPath, "paketler");

protocol.registerSchemesAsPrivileged([{ scheme: GORSEL, privileges: { standard: true, secure: true, supportFetchAPI: true } }]);

let servis: ServisIstemcisi;
let pencere: BrowserWindow | undefined;
let steamOnbellegi: string | undefined;
let guncelleme: ReturnType<typeof guncellemeyiBaslat>;
/** Bulunan ya da elle seçilen desteklenen oyunlar: kimlik → klasörler. Servise yalnızca bunlar gider. */
const taranan = new Map<string, Set<string>>();
/** Oyun dosyasına yazan, yarıda kesilmemesi gereken işlemler. */
const YAZAN = new Set(["turkceYap", "kaldir"]);
let yaziyor = 0;
let kapanmaBekliyor = false;
/** Son tarama listesi (Canlı Çeviri yöneticisi oyunları buradan tanır). */
let sonListe: ArayuzOyunu[] = [];
/** Cihaz özeti açılışta bir kez hesaplanır; işleyiciler hazır olmadan gelen çağrıyı bu söz bekletir. */
let canliHazir: Promise<CanliYonetici>;
let canliYonetici: CanliYonetici | undefined;

function canliKur(): Promise<CanliYonetici> {
  return makineGuidOku().then((guid) => {
    const y = new CanliYonetici({
      sunucu: new CanliSunucu({ taban: "https://api.turkcesivar.com", cihaz: cihazOzeti(guid) }),
      depo: new LisansDeposu(join(KLASOR, "canli-lisans.json"), gercekSifreleyici(safeStorage)),
      baslat: () => canliSurecBaslat(canliExeYolu(gelistirme, app.getAppPath(), process.resourcesPath)),
      oyunlar: () => sonListe,
      surecAcikMi: surecAcikMiAsenkron,
      ...(!app.isPackaged && process.env.TV_CANLI_TEST_OYUNU ? { testOyunu: process.env.TV_CANLI_TEST_OYUNU } : {}),
    });
    y.dinle((d) => {
      if (pencere && !pencere.webContents.isDestroyed()) pencere.webContents.send("tv:canli", d);
    });
    canliYonetici = y;
    return y;
  });
}

/** Pencere ve görev çubuğu simgesi (kurulumda resources\simge.ico). */
const SIMGE = gelistirme ? join(app.getAppPath(), "build", "simge.ico") : join(process.resourcesPath, "simge.ico");
// Görev çubuğunda gruplama, sabitleme ve bildirimlerde doğru ad/simge için (kurulumdaki appId ile aynı).
app.setAppUserModelId("com.turkcesivar.uygulama");

// Geliştirme: TV_AYRI_PROFIL=<klasör> → kurulu uygulama açıkken de deneme sürümü açılabilsin (ayrı tek örnek kilidi).
if (gelistirme && process.env.TV_AYRI_PROFIL) app.setPath("userData", process.env.TV_AYRI_PROFIL);
if (!app.requestSingleInstanceLock()) app.exit(0);
app.on("second-instance", () => {
  if (pencere?.isMinimized()) pencere.restore();
  pencere?.focus();
});

/** Yalnızca bulunmuş/seçilmiş, desteklenen bir oyunun klasörü kabul edilir (hile korumalı ya da rastgele klasör olamaz). */
function oyunKlasoruDenetle(kimlik: unknown, klasor: unknown): void {
  if (typeof kimlik !== "string" || typeof klasor !== "string" || !taranan.get(kimlik)?.has(klasor)) {
    throw new Error("Bu oyun klasörü tanınmıyor. Oyunlarım sayfasını yenileyip tekrar deneyin.");
  }
}
function tarananaEkle(kimlik: string, klasor: string) {
  if (!taranan.has(kimlik)) taranan.set(kimlik, new Set());
  taranan.get(kimlik)!.add(klasor);
}

/** Yazan işlemler sürerken pencere kapanmaz; bitince kapanır. */
async function yazanIs<T>(yontem: string, is: () => Promise<T>): Promise<T> {
  if (!YAZAN.has(yontem)) return is();
  yaziyor++;
  try {
    return await is();
  } finally {
    yaziyor--;
    if (yaziyor === 0 && kapanmaBekliyor) pencere?.destroy();
  }
}

/** Steam önbelleğindeki bir dosyayı arayüze `tv-gorsel://g/<yol>` olarak sunar; başka yer sunulmaz. */
function gorselAdresi(yol?: string): string | undefined {
  return yol ? `${GORSEL}://g/${encodeURIComponent(yol)}` : undefined;
}

function arayuzOyunu({ tanim, gorseller, ...o }: BulunanOyun, elle = false): ArayuzOyunu {
  // Önce Steam'in yerel önbelleği; orada yoksa (oyun numarası biliniyorsa) Steam'in görsel sunucusu.
  const cdn = o.appId !== undefined ? steamCdnGorselleri(o.appId) : undefined;
  return {
    ...o,
    ...(tanim ? { kimlik: tanim.kimlik } : {}),
    ...(elle ? { elle } : {}),
    gorseller: {
      kapak: gorselAdresi(gorseller.kapak) ?? cdn?.kapak,
      afis: gorselAdresi(gorseller.afis) ?? cdn?.afis,
      logo: gorselAdresi(gorseller.logo) ?? cdn?.logo,
      baslik: gorselAdresi(gorseller.baslik) ?? cdn?.baslik,
    },
  };
}

function elleOyun(t: OyunTanimi, klasor: string): BulunanOyun {
  return { appId: t.steamAppId, ad: t.ad, klasor, durum: "destekleniyor", tanim: t, gorseller: steamOnbellegi ? gorselleriBul(steamOnbellegi, t.steamAppId) : {}, platform: "elle" };
}

/** Epic'te bulunan Türkçesi hazır oyun: görselleri aynı oyunun Steam görselleridir (yerel önbellek ya da Steam sunucusu). */
/**
 * Epic'te bulunan oyun: Epic'in yerel görsel önbelleği olmadığı için aynı oyunun Steam görselleri kullanılır.
 * Türkçesi hazır oyunun numarası tanımdan bilinir; diğerleri adıyla Steam mağazasında aranır (sonuç saklanır).
 */
async function epicOyun(o: BulunanOyun): Promise<BulunanOyun> {
  const id = o.tanim?.steamAppId ?? (await steamKimligiAra(o.ad, KLASOR));
  if (id === undefined) return o;
  return { ...o, appId: id, gorseller: steamOnbellegi ? gorselleriBul(steamOnbellegi, id) : {} };
}

async function tara(): Promise<TaramaSonucu> {
  steamOnbellegi = steamKlasoruBul();
  // Tarama sürerken (Epic görselleri ağda aranırken) eski liste geçerli kalır; yeni liste sonunda tek seferde yerine geçer,
  // böylece aynı anda süren otomatik onarım "klasör tanınmıyor" hatasına düşmez.
  const yeni = new Map<string, Set<string>>();
  const ekle = (kimlik: string, klasor: string) => {
    if (!yeni.has(kimlik)) yeni.set(kimlik, new Set());
    yeni.get(kimlik)!.add(klasor);
  };
  const bulunan = steamOnbellegi ? oyunlariTara(kutuphaneleriOku(steamOnbellegi), steamOnbellegi).map((o) => arayuzOyunu(o)) : [];
  // Epic Games: aynı oyun hem Steam'de hem Epic'te kuruluysa iki ayrı kart olur (ayrı klasör, ayrı yedek).
  for (const o of await Promise.all(epicOyunlariTara().map(epicOyun))) bulunan.push(arayuzOyunu(o));
  for (const o of bulunan) if (o.durum === "destekleniyor" && o.kimlik) ekle(o.kimlik, o.klasor);
  // Elle seçilmiş klasörler: hâlâ yerinde ve hâlâ aynı oyunsa eklenir.
  for (const e of elleKlasorleriOku(KLASOR)) {
    if (yeni.get(e.kimlik)?.has(e.klasor)) continue;
    const t = klasorTani(e.klasor);
    if (!t || t.tanim.kimlik !== e.kimlik || t.klasor !== e.klasor) continue;
    ekle(e.kimlik, e.klasor);
    bulunan.unshift(arayuzOyunu(elleOyun(t.tanim, e.klasor), true));
  }
  taranan.clear();
  for (const [k, v] of yeni) taranan.set(k, v);
  sonListe = bulunan;
  return {
    oyunlar: bulunan,
    desteklenenler: OYUNLAR.map((t) => ({ kimlik: t.kimlik, ad: t.ad, klasorAdi: t.klasorAdi, tanimaDosyalari: t.tanimaDosyalari })),
    steamBulundu: steamOnbellegi !== undefined,
  };
}

function pencereAc() {
  pencere = new BrowserWindow({
    width: 1200,
    height: 800,
    minWidth: 960,
    minHeight: 640,
    title: "Türkçesi Var",
    icon: SIMGE,
    backgroundColor: "#0c0d10",
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: join(burasi, "../preload/index.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });
  pencere.setMenu(null);
  pencere.on("close", (e) => {
    if (yaziyor === 0) return;
    e.preventDefault();
    if (kapanmaBekliyor) return;
    kapanmaBekliyor = true;
    void dialog.showMessageBox(pencere!, {
      type: "info",
      title: "Türkçesi Var",
      message: "Bir işlem bitmek üzere",
      detail: "Oyun dosyaları yazılıyor. Yarıda kesilirse oyun bozulabilir; işlem biter bitmez uygulama kendiliğinden kapanacak.",
    });
  });
  pencere.once("ready-to-show", () => pencere?.show());
  // Uygulama içinden başka siteye gidilmez; https bağlantıları tarayıcıda açılır.
  pencere.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith("https://")) void shell.openExternal(url);
    return { action: "deny" };
  });
  pencere.webContents.on("will-navigate", (e) => e.preventDefault());

  // Geliştirme: TV_ACILIS="oyun=metro2033redux" → doğrudan o sayfada açılır.
  const hash = gelistirme ? (process.env.TV_ACILIS ?? "") : "";
  if (gelistirme && process.env.ELECTRON_RENDERER_URL) void pencere.loadURL(`${process.env.ELECTRON_RENDERER_URL}#${hash}`);
  else void pencere.loadFile(join(burasi, "../renderer/index.html"), { hash });

  // Geliştirme: TV_EKRAN_GORUNTUSU=dosya.png → yüklenince ekran görüntüsü alıp kapanır.
  const hedef = gelistirme ? process.env.TV_EKRAN_GORUNTUSU : undefined;
  if (hedef) {
    pencere.webContents.once("did-finish-load", () => {
      setTimeout(async () => {
        const g = await pencere!.webContents.capturePage();
        writeFileSync(hedef, g.toPNG());
        app.quit();
      }, Number(process.env.TV_BEKLE ?? 2500));
    });
  }
}

function ipcKur() {
  ipcMain.handle("tv:oyunlariTara", () => tara());

  ipcMain.handle("tv:klasorSec", async (_e, kimlik?: string) => {
    const t = OYUNLAR.find((x) => x.kimlik === kimlik);
    const r = await dialog.showOpenDialog(pencere!, {
      title: t ? `${t.ad} klasörünü seç` : "Oyun klasörünü seç",
      properties: ["openDirectory"],
    });
    if (r.canceled || !r.filePaths[0]) return undefined;
    const bulunan = klasorTani(r.filePaths[0]);
    if (!bulunan || (t && bulunan.tanim.kimlik !== t.kimlik)) return { hata: secimHatasi(r.filePaths[0], t) };
    elleKlasorEkle(KLASOR, { kimlik: bulunan.tanim.kimlik, klasor: bulunan.klasor });
    tarananaEkle(bulunan.tanim.kimlik, bulunan.klasor);
    return { oyun: arayuzOyunu(elleOyun(bulunan.tanim, bulunan.klasor), true) };
  });

  ipcMain.handle("tv:cagir", (e, yontem: string, argumanlar: unknown[], ilerlemeKimligi?: number) => {
    if (!(SERVIS_YONTEMLERI as readonly string[]).includes(yontem) || !Array.isArray(argumanlar)) throw new Error("İzin verilmeyen işlem");
    oyunKlasoruDenetle(argumanlar[0], argumanlar[1]);
    const ilerleme =
      ilerlemeKimligi === undefined ? undefined : (i: unknown) => !e.sender.isDestroyed() && e.sender.send("tv:ilerleme", ilerlemeKimligi, i);
    return yazanIs(yontem, () => servis.cagir(yontem, argumanlar, ilerleme));
  });

  ipcMain.handle("tv:klasoruAc", async (_e, kimlik: string, klasor: string) => {
    oyunKlasoruDenetle(kimlik, klasor);
    await shell.openPath(klasor);
  });
  ipcMain.handle("tv:guncellemeDurumu", () => guncelleme.durum());
  ipcMain.handle("tv:guncellemeKur", () => {
    // Oyun dosyaları yazılırken yeniden başlatılırsa kurulum yarım kalır; önce bitmesi beklenir.
    if (yaziyor > 0) throw new Error("Bir oyuna Türkçe kuruluyor ya da kaldırılıyor. Bitince yeniden başlatabilirsin.");
    guncelleme.kur();
  });
  ipcMain.handle("tv:guncellemeDenetle", () => guncelleme.denetle());
  ipcMain.handle("tv:guncellemeIndir", async () => {
    try {
      await guncelleme.indir();
    } catch {
      throw new Error("Güncelleme indirilemedi. İnternet bağlantını kontrol edip yeniden \"İndir\"e basabilirsin.");
    }
  });
  ipcMain.handle("tv:disBaglanti", (_e, url: string) => (url.startsWith("https://") ? shell.openExternal(url) : undefined));
  ipcMain.handle("tv:surum", () => app.getVersion());

  ipcMain.handle("tv:canliDurum", async () => (await canliHazir).durum());
  ipcMain.handle("tv:canliYenile", async () => (await canliHazir).yenile());
  ipcMain.handle("tv:canliDeneme", async () => (await canliHazir).deneme());
  ipcMain.handle("tv:canliEslestir", async () => (await canliHazir).eslestirBaslat());
  ipcMain.handle("tv:canliBaslat", async () => (await canliHazir).calistir());
  ipcMain.handle("tv:canliDurdur", async () => (await canliHazir).durdur());
}

void app.whenReady().then(() => {
  // Yalnızca Steam'in görsel önbelleğindeki dosyalar sunulur.
  protocol.handle(GORSEL, (istek) => {
    if (!steamOnbellegi) return new Response("izin yok", { status: 403 });
    let yol: string;
    let kok: string;
    try {
      // Kısayol/bağlantılar çözülür; kök klasörün kardeşleri (librarycache_x) ayırıcıyla dışlanır.
      const ham = decodeURIComponent(new URL(istek.url).pathname.slice(1));
      if (/^[\\/]{2}/.test(ham)) return new Response("izin yok", { status: 403 });
      yol = realpathSync(resolve(ham));
      kok = realpathSync(resolve(steamOnbellegi, "appcache", "librarycache"));
    } catch {
      return new Response("yok", { status: 404 });
    }
    if (!yol.toLowerCase().startsWith((kok + sep).toLowerCase())) return new Response("izin yok", { status: 403 });
    return net.fetch(pathToFileURL(yol).toString());
  });
  servis = servisIstemcisi({ uygulamaKlasoru: KLASOR, paketKlasoru: PAKET_KLASORU, workerYolu });
  guncelleme = guncellemeyiBaslat(app.isPackaged, (d: GuncellemeDurumu) => {
    if (pencere && !pencere.webContents.isDestroyed()) pencere.webContents.send("tv:guncelleme", d);
  });
  canliHazir = canliKur();
  canliHazir.catch(() => {
    // cihaz kimliği okunamazsa Canlı Çeviri kullanılamaz; uygulamanın geri kalanı etkilenmez
  });
  ipcKur();
  pencereAc();
});

app.on("before-quit", () => {
  canliYonetici?.kapat();
});

app.on("window-all-closed", () => {
  void servis?.kapat();
  app.quit();
});
