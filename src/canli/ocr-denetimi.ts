// Windows'ta İngilizce metin tanıma (OCR) var mı? canli.exe açılışta "hazir" yazar; en-US OCR yoksa hemen ardından
// {"olay":"hata","kod":"ocr_dil_yok"} gelir. Ekran okunmaz: süreç komut almadan kapatılır.

import type { CanliSurec, Zaman } from "./yonetici.js";

/** "hazir"dan sonra hata gelmesi için beklenen süre. */
export const HAZIR_SONRASI_MS = 800;
/** Hiç yanıt gelmezse vazgeçme süresi. */
export const DENETIM_SURESI_MS = 5000;

/** true: yüklü · false: yüklü değil · null: anlaşılamadı (uyarı gösterilmez). */
export function ocrDenetle(baslat: () => CanliSurec, zaman: Zaman = globalThis as unknown as Zaman): Promise<boolean | null> {
  return new Promise((coz) => {
    let bitti = false;
    let tampon = "";
    let hazirSayaci: unknown = null;
    let surec: CanliSurec | null = null;
    const bitir = (sonuc: boolean | null) => {
      if (bitti) return;
      bitti = true;
      zaman.clearTimeout?.(genelSayac);
      if (hazirSayaci !== null) zaman.clearTimeout?.(hazirSayaci);
      if (surec) {
        try {
          surec.yaz('{"komut":"cik"}');
          surec.oldur();
        } catch {
          // süreç zaten kapanmış olabilir
        }
      }
      coz(sonuc);
    };
    const genelSayac = zaman.setTimeout?.(() => bitir(null), DENETIM_SURESI_MS);
    try {
      surec = baslat();
    } catch {
      bitir(null);
      return;
    }
    surec.satirlar((parca) => {
      tampon += parca;
      if (tampon.includes('"ocr_dil_yok"')) return bitir(false);
      if (hazirSayaci === null && tampon.includes('"olay":"hazir"')) {
        hazirSayaci = zaman.setTimeout?.(() => bitir(true), HAZIR_SONRASI_MS) ?? null;
      }
    });
    surec.cikinca(() => {
      if (tampon.includes('"ocr_dil_yok"')) bitir(false);
      else bitir(null);
    });
  });
}
