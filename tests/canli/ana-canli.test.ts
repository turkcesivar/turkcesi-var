import { join } from "node:path";
import { beforeEach, describe, expect, it, vi } from "vitest";

type ExecGeriDonus = (hata: Error | null, cikti: string) => void;
const execFileSahte = vi.hoisted(() => vi.fn());
vi.mock("node:child_process", async (asil) => ({
  ...(await asil<typeof import("node:child_process")>()),
  execFile: execFileSahte,
}));

const { canliExeYolu, surecAcikMiAsenkron, tasklistteVar, yenidenDenenirSoz, yoneticiyiAc } = await import("../../uygulama/ana/canli.js");
const { makineGuidOku } = await import("../../src/canli/cihaz.js");

beforeEach(() => {
  execFileSahte.mockReset();
});

describe("canliExeYolu", () => {
  it("geliştirmede depo içindeki çıktı klasörünü verir", () => {
    expect(canliExeYolu(true, String.raw`D:\depo`, String.raw`D:\kaynak`)).toBe(join(String.raw`D:\depo`, "canli", "cikti", "canli.exe"));
  });
  it("pakette resources\\canli klasörünü verir", () => {
    expect(canliExeYolu(false, String.raw`D:\depo`, String.raw`D:\kaynak`)).toBe(join(String.raw`D:\kaynak`, "canli", "canli.exe"));
  });
});

describe("tasklistteVar", () => {
  const UZUN = "Oyun-Win64-Shipping.exe";
  it("25 karakterden uzun adı CSV çıktısında bulur", () => {
    const ad = "Uzun-Bir-Oyun-Win64-Shipping.exe";
    expect(ad.length).toBeGreaterThan(25);
    const cikti = `"${ad}","12345","Console","1","1.234.567 K"\r\n`;
    expect(tasklistteVar(cikti, ad)).toBe(true);
    expect(tasklistteVar(cikti, ad.toUpperCase())).toBe(true);
  });
  it("kısa adı bulur", () => {
    expect(tasklistteVar(`"x.exe","42","Console","1","10.000 K"\r\n`, "x.exe")).toBe(true);
  });
  it("'INFO: No tasks' çıktısında yok", () => {
    expect(tasklistteVar("INFO: No tasks are running which match the specified criteria.\r\n", UZUN)).toBe(false);
  });
  it("adın parçası eşleşmez", () => {
    expect(tasklistteVar(`"ax.exe","42","Console","1","10.000 K"\r\n`, "x.exe")).toBe(false);
  });
});

describe("surecAcikMiAsenkron", () => {
  it("tasklist'i CSV biçiminde çağırır, uzun adı bulur", async () => {
    execFileSahte.mockImplementation((_k: string, _a: string[], _o: unknown, f: ExecGeriDonus) =>
      f(null, `"Uzun-Bir-Oyun-Win64-Shipping.exe","1","Console","1","1 K"`),
    );
    expect(await surecAcikMiAsenkron("Uzun-Bir-Oyun-Win64-Shipping.exe")).toBe(true);
    const [komut, argumanlar, secenek] = execFileSahte.mock.calls[0]!;
    expect(komut).toBe("tasklist");
    expect(argumanlar).toEqual(["/FI", "IMAGENAME eq Uzun-Bir-Oyun-Win64-Shipping.exe", "/FO", "CSV", "/NH"]);
    expect(secenek).toMatchObject({ timeout: 5000 });
  });
  it("süreç yoksa false", async () => {
    execFileSahte.mockImplementation((_k: string, _a: string[], _o: unknown, f: ExecGeriDonus) =>
      f(null, "INFO: No tasks are running which match the specified criteria."),
    );
    expect(await surecAcikMiAsenkron("x.exe")).toBe(false);
  });
  it("yoklama hatası/zaman aşımında true (oturum sürer)", async () => {
    execFileSahte.mockImplementation((_k: string, _a: string[], _o: unknown, f: ExecGeriDonus) => f(new Error("ETIMEDOUT"), ""));
    expect(await surecAcikMiAsenkron("x.exe")).toBe(true);
  });
});

describe("makineGuidOku (varsayılan çalıştırıcı)", () => {
  it("reg query'ye zaman aşımı verir", async () => {
    execFileSahte.mockImplementation((_k: string, _a: string[], _o: unknown, f: ExecGeriDonus) => f(null, "    MachineGuid    REG_SZ    abc-123\r\n"));
    expect(await makineGuidOku()).toBe("abc-123");
    const [komut, , secenek] = execFileSahte.mock.calls[0]!;
    expect(komut).toBe("reg");
    expect(secenek).toMatchObject({ timeout: 5000, windowsHide: true });
  });
});

describe("yoneticiyiAc", () => {
  it("dinleyiciyi bağlar ve açılışta yenile çağırır", () => {
    const y = { dinle: vi.fn(), yenile: vi.fn(() => Promise.resolve()) };
    const yay = vi.fn();
    expect(yoneticiyiAc(y, yay)).toBe(y);
    expect(y.dinle).toHaveBeenCalledWith(yay);
    expect(y.yenile).toHaveBeenCalledTimes(1);
  });

  it("açılışta İngilizce metin tanımayı da denetler", () => {
    const y = { dinle: vi.fn(), yenile: vi.fn(() => Promise.resolve()), dilDenetle: vi.fn(() => Promise.resolve()) };
    yoneticiyiAc(y, vi.fn());
    expect(y.dilDenetle).toHaveBeenCalledTimes(1);
  });
});

describe("yenidenDenenirSoz", () => {
  it("başarılı kurulum bir kez yapılır", async () => {
    const kur = vi.fn(() => Promise.resolve(1));
    const al = yenidenDenenirSoz(kur);
    expect(await al()).toBe(1);
    expect(await al()).toBe(1);
    expect(kur).toHaveBeenCalledTimes(1);
  });
  it("reddedilen kurulum sonraki çağrıda yeniden denenir", async () => {
    const kur = vi.fn<() => Promise<number>>().mockRejectedValueOnce(new Error("cihaz")).mockResolvedValueOnce(7);
    const al = yenidenDenenirSoz(kur);
    await expect(al()).rejects.toThrow("cihaz");
    expect(await al()).toBe(7);
    expect(await al()).toBe(7);
    expect(kur).toHaveBeenCalledTimes(2);
  });
});
