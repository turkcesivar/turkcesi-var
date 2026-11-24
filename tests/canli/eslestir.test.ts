import { describe, expect, it } from "vitest";
import { oyunBul, oyunKimligi } from "../../src/canli/eslestir.js";
import type { ArayuzOyunu } from "../../src/servis/api.js";

function oyun(klasor: string, ek: Partial<ArayuzOyunu> = {}): ArayuzOyunu {
  return { ad: klasor, klasor, durum: "desteklenmiyor", gorseller: {}, ...ek };
}

describe("oyunBul", () => {
  it("buyuk_kucuk_ve_egik_cizgi", () => {
    const o = oyun("C:\\Steam\\steamapps\\common\\Oyun X");
    expect(oyunBul("c:/steam/steamapps/common/Oyun X/bin/x.exe", [o])).toBe(o);
  });

  it("klasor_oneki_eslesmez", () => {
    const mafia = oyun("C:\\Oyunlar\\Mafia");
    expect(oyunBul("C:\\Oyunlar\\Mafia 2\\mafia2.exe", [mafia])).toBeUndefined();
    const mafia2 = oyun("C:\\Oyunlar\\Mafia 2");
    expect(oyunBul("C:\\Oyunlar\\Mafia 2\\mafia2.exe", [mafia, mafia2])).toBe(mafia2);
  });

  it("en_uzun_klasor", () => {
    const dis = oyun("C:\\Oyunlar\\");
    const ic = oyun("C:\\Oyunlar\\Seri\\Oyun");
    const orta = oyun("C:\\Oyunlar\\Seri");
    expect(oyunBul("C:\\Oyunlar\\Seri\\Oyun\\bin\\a.exe", [dis, ic, orta])).toBe(ic);
  });

  it("null_yol", () => {
    expect(oyunBul(null, [oyun("C:\\A")])).toBeUndefined();
    expect(oyunBul("", [oyun("C:\\A")])).toBeUndefined();
  });
});

describe("oyunKimligi", () => {
  it("uc_platform", () => {
    expect(oyunKimligi(oyun("C:\\Steam\\common\\Oyun", { appId: 123, platform: "steam" }))).toBe("steam:123");
    expect(oyunKimligi(oyun("D:\\Epic Games\\Oyun Y\\", { platform: "epic" }))).toBe("epic:Oyun Y");
    expect(oyunKimligi(oyun("E:/Benim/Oyun Z", { platform: "elle", elle: true }))).toBe("elle:Oyun Z");
  });

  it("platform_yoksa_appId_ile_steam_degilse_elle", () => {
    expect(oyunKimligi(oyun("C:\\S\\Oyun", { appId: 7 }))).toBe("steam:7");
    expect(oyunKimligi(oyun("C:\\S\\Oyun"))).toBe("elle:Oyun");
  });
});
