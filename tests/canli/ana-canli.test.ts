import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { canliExeYolu } from "../../uygulama/ana/canli.js";

describe("canliExeYolu", () => {
  it("geliştirmede depo içindeki çıktı klasörünü verir", () => {
    expect(canliExeYolu(true, "D:\depo", "D:\kaynak")).toBe(join("D:\depo", "canli", "cikti", "canli.exe"));
  });
  it("pakette resources\canli klasörünü verir", () => {
    expect(canliExeYolu(false, "D:\depo", "D:\kaynak")).toBe(join("D:\kaynak", "canli", "canli.exe"));
  });
});
