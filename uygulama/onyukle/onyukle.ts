// Güvenli köprü: arayüze yalnızca tipli `window.tv` API'si açılır (Node erişimi yok).

import { contextBridge, ipcRenderer } from "electron";
import type { GuncellemeDurumu, TvApi } from "../../src/servis/api.js";
import type { CanliDurum } from "../../src/canli/tipler.js";
import type { Ilerleme } from "../../src/servis/tipler.js";

let sayac = 0;
const dinleyiciler = new Map<number, (i: Ilerleme) => void>();
ipcRenderer.on("tv:ilerleme", (_e, kimlik: number, i: Ilerleme) => dinleyiciler.get(kimlik)?.(i));

const guncellemeDinleyicileri = new Set<(d: GuncellemeDurumu) => void>();
ipcRenderer.on("tv:guncelleme", (_e, d: GuncellemeDurumu) => guncellemeDinleyicileri.forEach((f) => f(d)));

const canliDinleyicileri = new Set<(d: CanliDurum) => void>();
ipcRenderer.on("tv:canli", (_e, d: CanliDurum) => canliDinleyicileri.forEach((f) => f(d)));

async function cagir<T>(yontem: string, argumanlar: unknown[], ilerleme?: (i: Ilerleme) => void): Promise<T> {
  const kimlik = ilerleme ? ++sayac : undefined;
  if (kimlik !== undefined) dinleyiciler.set(kimlik, ilerleme!);
  try {
    return (await ipcRenderer.invoke("tv:cagir", yontem, argumanlar, kimlik)) as T;
  } finally {
    if (kimlik !== undefined) dinleyiciler.delete(kimlik);
  }
}

const tv: TvApi = {
  oyunlariTara: () => ipcRenderer.invoke("tv:oyunlariTara"),
  klasorSec: (k) => ipcRenderer.invoke("tv:klasorSec", k),
  oyunDurumu: (k, klasor) => cagir("oyunDurumu", [k, klasor]),
  turkceYap: (k, klasor, il) => cagir("turkceYap", [k, klasor], il),
  kaldir: (k, klasor) => cagir("kaldir", [k, klasor]),
  klasoruAc: (k, klasor) => ipcRenderer.invoke("tv:klasoruAc", k, klasor),
  guncellemeDurumu: () => ipcRenderer.invoke("tv:guncellemeDurumu"),
  guncellemeDinle: (f) => {
    guncellemeDinleyicileri.add(f);
    return () => guncellemeDinleyicileri.delete(f);
  },
  guncellemeKur: () => ipcRenderer.invoke("tv:guncellemeKur"),
  guncellemeIndir: () => ipcRenderer.invoke("tv:guncellemeIndir"),
  guncellemeDenetle: () => ipcRenderer.invoke("tv:guncellemeDenetle"),
  disBaglanti: (url) => ipcRenderer.invoke("tv:disBaglanti", url),
  surum: () => ipcRenderer.invoke("tv:surum"),
  canliDurum: () => ipcRenderer.invoke("tv:canliDurum"),
  canliDinle: (f) => {
    canliDinleyicileri.add(f);
    return () => canliDinleyicileri.delete(f);
  },
  canliYenile: (sessiz?: boolean) => ipcRenderer.invoke("tv:canliYenile", sessiz === true),
  canliDeneme: () => ipcRenderer.invoke("tv:canliDeneme"),
  canliEslestir: () => ipcRenderer.invoke("tv:canliEslestir"),
  canliCihazEkle: (numara: string) => ipcRenderer.invoke("tv:canliCihazEkle", numara),
  canliDestekOl: (seviye: string) => ipcRenderer.invoke("tv:canliDestekOl", seviye),
  canliBaslat: () => ipcRenderer.invoke("tv:canliBaslat"),
  canliDurdur: () => ipcRenderer.invoke("tv:canliDurdur"),
  canliDilDenetle: () => ipcRenderer.invoke("tv:canliDilDenetle"),
  canliDilAyarlari: () => ipcRenderer.invoke("tv:canliDilAyarlari"),
};

contextBridge.exposeInMainWorld("tv", tv);
