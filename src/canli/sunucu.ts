// Canlı Çeviri sunucusuyla konuşan istemci. Ağ sorunu "cevrimdisi", bozuk yanıt "gecici_hata" olur.

import type { LisansBilgisi, SunucuSonucu } from "./tipler.js";

export interface SunucuSecenekleri {
  taban: string;
  cihaz: string;
  fetch?: typeof fetch;
  zamanAsimiMs?: number;
}

export class CanliSunucu {
  private readonly taban: string;
  private readonly cihaz: string;
  private readonly getir: typeof fetch;
  private readonly zamanAsimiMs: number;

  constructor(o: SunucuSecenekleri) {
    this.taban = o.taban.replace(/\/+$/, "");
    this.cihaz = o.cihaz;
    this.getir = o.fetch ?? fetch;
    this.zamanAsimiMs = o.zamanAsimiMs ?? 10000;
  }

  durum(lisans: string | null, numara?: string): Promise<SunucuSonucu<LisansBilgisi & { lisans?: string }>> {
    const yol = numara ? `/durum?numara=${encodeURIComponent(numara)}` : "/durum";
    return this.istek("GET", yol, numara ? null : lisans);
  }

  deneme(): Promise<SunucuSonucu<{ lisans: string } & LisansBilgisi>> {
    return this.istek("POST", "/deneme", null);
  }

  eslestir(): Promise<SunucuSonucu<{ numara: string; bitis: number }>> {
    return this.istek("POST", "/eslestir", null);
  }

  private async istek<T>(yontem: string, yol: string, lisans: string | null): Promise<SunucuSonucu<T>> {
    const basliklar: Record<string, string> = { "X-Cihaz": this.cihaz };
    if (lisans) basliklar["Authorization"] = `Bearer ${lisans}`;
    const durdur = new AbortController();
    const sayac = setTimeout(() => durdur.abort(), this.zamanAsimiMs);
    try {
      const yanit = await this.getir(this.taban + yol, { method: yontem, headers: basliklar, signal: durdur.signal });
      let govde: unknown;
      try {
        govde = await yanit.json();
      } catch {
        return { tamam: false, hata: "gecici_hata" };
      }
      if (typeof govde !== "object" || govde === null) return { tamam: false, hata: "gecici_hata" };
      const hata = (govde as { hata?: unknown }).hata;
      if (typeof hata === "string" && hata) return { tamam: false, hata };
      if (!yanit.ok) return { tamam: false, hata: "gecici_hata" };
      return { tamam: true, veri: govde as T };
    } catch {
      return { tamam: false, hata: "cevrimdisi" };
    } finally {
      clearTimeout(sayac);
    }
  }
}
