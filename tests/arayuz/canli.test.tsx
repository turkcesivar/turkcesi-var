// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { CanliDurum } from "../../src/canli/tipler.js";
import { kodMetni } from "../../uygulama/arayuz/canli-metinler.js";
import { CanliCeviri } from "../../uygulama/arayuz/sayfalar/CanliCeviri.js";

afterEach(cleanup);

const taban: CanliDurum = { yukleniyor: false, lisans: { durum: "lisanssiz" }, calisma: "kapali" };

function kur(d: CanliDurum, ek: Record<string, unknown> = {}) {
  const tv = {
    canliDurum: vi.fn(() => Promise.resolve(d)),
    canliDinle: vi.fn(() => () => {}),
    canliYenile: vi.fn(() => Promise.resolve()),
    canliDeneme: vi.fn(() => Promise.resolve({})),
    canliEslestir: vi.fn(() => Promise.resolve({})),
    canliBaslat: vi.fn(() => Promise.resolve()),
    canliDurdur: vi.fn(() => Promise.resolve()),
    disBaglanti: vi.fn(() => Promise.resolve()),
    ...ek,
  };
  (window as unknown as { tv: unknown }).tv = tv;
  return tv;
}

describe("Canlı Çeviri sayfası", () => {
  it("lisanssiz_ekran_farki_anlatir", async () => {
    kur(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText(/Her şey çevrilmez/)).toBeTruthy();
    expect(screen.getByText(/Bazı çeviriler kusurlu olabilir/)).toBeTruthy();
    expect(screen.getByText("Canlı çeviri nasıl çalışır?")).toBeTruthy();
  });

  it("deneme_onaysiz_baslamaz", async () => {
    const tv = kur(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    fireEvent.click(await screen.findByRole("button", { name: /7 gün ücretsiz dene/ }));
    expect(tv.canliDeneme).not.toHaveBeenCalled();
    fireEvent.click(await screen.findByRole("button", { name: /Anladım, denemeyi başlat/ }));
    await waitFor(() => expect(tv.canliDeneme).toHaveBeenCalledTimes(1));
  });

  it("vazgec_denemeyi_baslatmaz", async () => {
    const tv = kur(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    fireEvent.click(await screen.findByRole("button", { name: /7 gün ücretsiz dene/ }));
    fireEvent.click(screen.getByRole("button", { name: "Vazgeç" }));
    expect(tv.canliDeneme).not.toHaveBeenCalled();
  });

  it("eslestirme_numarasi_gosterilir", async () => {
    const tv = kur(taban, { canliEslestir: vi.fn(() => Promise.resolve({})) });
    tv.canliDurum.mockResolvedValueOnce(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    const dugme = await screen.findByRole("button", { name: /Destek ol ve etkinleştir/ });
    tv.canliDurum.mockResolvedValue({ ...taban, eslesme: { numara: "482913", bitis: Date.now() + 1e6 } });
    fireEvent.click(dugme);
    expect(await screen.findByText("482913")).toBeTruthy();
    expect(screen.getByText(/Ödemen gelince canlı çeviri bu bilgisayarda kendiliğinden açılır/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /Kreosus'u aç/ }));
    expect(tv.disBaglanti).toHaveBeenCalledWith("https://kreosus.com/turkcesivar");
  });

  it("kontrol_ekrani_kalan_cubugu", async () => {
    const bitis = new Date(2026, 11, 1).getTime();
    kur({ ...taban, lisans: { durum: "etkin", tur: "deneme", donemBitis: bitis, kalan: 1858, gunKalan: 1858 } });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect((await screen.findAllByText(/1\.858/)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/2\.000/).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: /Canlı Çeviriyi Başlat/ })).toBeTruthy();
    expect(screen.getByText(/Deneme başladı: 7 gün, 2\.000 yeni satır/)).toBeTruthy();
  });

  it("baslat_ve_durdur", async () => {
    const tv = kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 100, gunKalan: 50 } });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    fireEvent.click(await screen.findByRole("button", { name: /Canlı Çeviriyi Başlat/ }));
    expect(tv.canliBaslat).toHaveBeenCalled();
  });

  it("paketli_engel_turkce_yap", async () => {
    const onOyunaGit = vi.fn();
    kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 5, gunKalan: 5 }, engel: { tur: "paketli", oyunAdi: "Mafia", kimlik: "mafiatheoldcountry" } });
    render(<CanliCeviri onOyunaGit={onOyunaGit} />);
    fireEvent.click(await screen.findByRole("button", { name: "Türkçe yap" }));
    expect(onOyunaGit).toHaveBeenCalledWith("mafiatheoldcountry");
  });

  it("bilinmeyen_kod_genel_metin", async () => {
    expect(kodMetni("xyz")).toBe("Beklenmeyen bir sorun oluştu (xyz).");
    expect(kodMetni("lisans_yok")).toBe("Lisans bulunamadı.");
    kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 5, gunKalan: 5 }, uyari: "tuhaf_kod" });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText("Beklenmeyen bir sorun oluştu (tuhaf_kod).")).toBeTruthy();
  });

  it("sunucuya_ulasilamiyor_yeniden_dene", async () => {
    const tv = kur({ ...taban, lisans: null });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText(/Sunucuya ulaşılamıyor/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Yeniden dene" }));
    expect(tv.canliYenile).toHaveBeenCalled();
  });

  it("canliDurum_reddedilirse_cihaz_hatasi_ve_yeniden_dene", async () => {
    kur(taban, { canliDurum: vi.fn(() => Promise.reject(new Error("x"))) });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText("Beklenmeyen bir sorun oluştu (cihaz).")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Yeniden dene" })).toBeTruthy();
  });

  it("kayit_hatasi_discord_dugmesi", async () => {
    const tv = kur({ ...taban, sunucuHatasi: "kayit" });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText(/Lisans bu bilgisayara kaydedilemedi/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /Discord/ }));
    expect(tv.disBaglanti).toHaveBeenCalledWith("https://discord.gg/nnNUCMUBJx");
  });

  it("bellek_kelimesi_gecmez", async () => {
    kur(taban);
    const { container } = render(<CanliCeviri onOyunaGit={vi.fn()} />);
    await screen.findByText(/Her şey çevrilmez/);
    const metin = (container.textContent ?? "").toLocaleLowerCase("tr");
    expect(metin).not.toContain("bellek");
    expect(metin).not.toContain("ücretsiz satır");
  });
});
