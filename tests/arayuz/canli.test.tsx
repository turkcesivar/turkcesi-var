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
    canliCihazEkle: vi.fn(() => Promise.resolve({})),
    canliDestekOl: vi.fn(() => Promise.resolve({})),
    canliBaslat: vi.fn(() => Promise.resolve()),
    canliDurdur: vi.fn(() => Promise.resolve()),
    canliDilDenetle: vi.fn(() => Promise.resolve()),
    canliDilAyarlari: vi.fn(() => Promise.resolve()),
    disBaglanti: vi.fn(() => Promise.resolve()),
    ...ek,
  };
  (window as unknown as { tv: unknown }).tv = tv;
  return tv;
}

describe("Canlı Çeviri sayfası", () => {
  it("yenile_dugmesi_lisansi_ve_dil_paketini_yeniden_denetler", async () => {
    const tv = kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 100, gunKalan: 50 } });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    const dugme = await screen.findByRole("button", { name: /Yenile/ });
    // Sayfa açılışında lisans sessizce yenilenir.
    await waitFor(() => expect(tv.canliYenile).toHaveBeenCalledWith(true));
    tv.canliDurum.mockClear();
    tv.canliYenile.mockClear();
    fireEvent.click(dugme);
    await waitFor(() => expect(tv.canliYenile).toHaveBeenCalledTimes(1));
    expect(tv.canliYenile).not.toHaveBeenCalledWith(true);
    expect(tv.canliDilDenetle).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(tv.canliDurum).toHaveBeenCalled());
  });

  it("yenile_dugmesi_lisans_ekraninda_da_var", async () => {
    kur(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByRole("button", { name: /Yenile/ })).toBeTruthy();
  });

  it("dil_paketi_yoksa_kart_adimlar_ve_deneme_kapali", async () => {
    const tv = kur({ ...taban, ocrHazir: false });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText("İngilizce dil paketi gerekli")).toBeTruthy();
    expect(screen.getByText(/English \(United States\)/)).toBeTruthy();
    expect(screen.getByText(/Windows görüntüleme dili olarak ayarla/)).toBeTruthy();
    const deneme = screen.getByRole("button", { name: /7 gün ücretsiz dene/ }) as HTMLButtonElement;
    expect(deneme.disabled).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: /Dil ayarlarını aç/ }));
    expect(tv.canliDilAyarlari).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole("button", { name: /Yeniden denetle/ }));
    await waitFor(() => expect(tv.canliDilDenetle).toHaveBeenCalledTimes(1));
  });

  it("dil_paketi_varsa_ya_da_bilinmiyorsa_kart_yok", async () => {
    kur({ ...taban, ocrHazir: true });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    await screen.findByText(/Her şey çevrilmez/);
    expect(screen.queryByText("İngilizce dil paketi gerekli")).toBeNull();
    cleanup();
    kur(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    const deneme = (await screen.findByRole("button", { name: /7 gün ücretsiz dene/ })) as HTMLButtonElement;
    expect(deneme.disabled).toBe(false);
    expect(screen.queryByText("İngilizce dil paketi gerekli")).toBeNull();
  });

  it("lisansliyken_dil_paketi_yoksa_baslat_kapali_ve_hata_tekrarlanmaz", async () => {
    kur({ yukleniyor: false, lisans: { durum: "etkin", tur: "destekci", kalan: 100, gunKalan: 100 }, calisma: "kapali", ocrHazir: false, hata: "ocr_dil_yok" });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText("İngilizce dil paketi gerekli")).toBeTruthy();
    const baslat = screen.getByRole("button", { name: /Canlı Çeviriyi Başlat/ }) as HTMLButtonElement;
    expect(baslat.disabled).toBe(true);
    expect(screen.queryByText(kodMetni("ocr_dil_yok"))).toBeNull();
  });

  it("lisanssiz_ekran_farki_anlatir", async () => {
    kur(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText(/Her şey çevrilmez/)).toBeTruthy();
    expect(screen.getByText(/Bazı çeviriler kusurlu olabilir/)).toBeTruthy();
    expect(screen.getByText(/ne kadar çok oynanırsa canlı çevirisi o kadar hızlanır/)).toBeTruthy();
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

  it("lisanssiz_seviye_secince_odeme_baglantisi_ve_bekleme_metni", async () => {
    const tv = kur(taban);
    tv.canliDurum.mockResolvedValueOnce(taban);
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    const dugme = await screen.findByRole("button", { name: /Destekçi ol · aylık 75/ });
    tv.canliDurum.mockResolvedValue({ ...taban, eslesme: { numara: "TV-482A", bitis: Date.now() + 1e6 } });
    fireEvent.click(dugme);
    await waitFor(() => expect(tv.canliDestekOl).toHaveBeenCalledWith("destekci"));
    expect(await screen.findByText("TV-482A")).toBeTruthy();
    expect(screen.getByText(/Ödeme sayfası tarayıcında açıldı/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /Üst seviye · aylık 200/ }));
    await waitFor(() => expect(tv.canliDestekOl).toHaveBeenCalledWith("ust"));
    // Mesaja numara yazma talimatı artık yok (Kreosus ödeme ekranında mesaj alanı yok).
    expect(screen.queryByText(/mesaj kısmına yaz/)).toBeNull();
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
    const tv = kur(taban, { canliDurum: vi.fn().mockRejectedValueOnce(new Error("x")).mockResolvedValue(taban) });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText("Bu bilgisayarın kimliği okunamadı; canlı çeviri şu an kullanılamıyor.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Yeniden dene" }));
    expect(tv.canliYenile).toHaveBeenCalled();
    // yeniden deneme başarılı: lisans ekranı gelir
    expect(await screen.findByRole("button", { name: /7 gün ücretsiz dene/ })).toBeTruthy();
  });

  it("ilk_yukleniyorken_iskelet_sunucu_hatasi_degil", async () => {
    const tv = kur({ yukleniyor: true, lisans: null, calisma: "kapali" });
    const { container } = render(<CanliCeviri onOyunaGit={vi.fn()} />);
    await waitFor(() => expect(tv.canliDurum).toHaveBeenCalled());
    await waitFor(() => expect(container.querySelector(".iskelet")).toBeTruthy());
    expect(screen.queryByText(/Sunucuya ulaşılamıyor/)).toBeNull();
  });

  it("cevrimdisi_tek_cumle", async () => {
    kur({ ...taban, lisans: null, sunucuHatasi: "cevrimdisi" });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText(/Sunucuya ulaşılamıyor\. İnternet/)).toBeTruthy();
    expect(screen.queryByText(kodMetni("cevrimdisi"))).toBeNull();
  });

  it("diger_sunucu_hatasi_ek_satirda", async () => {
    kur({ ...taban, lisans: null, sunucuHatasi: "gecici_hata" });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText(kodMetni("gecici_hata"))).toBeTruthy();
  });

  it("deneme_gunluk_cubuk_yok_destekcide_var", async () => {
    kur({ ...taban, lisans: { durum: "etkin", tur: "deneme", kalan: 2000 } });
    const ilk = render(<CanliCeviri onOyunaGit={vi.fn()} />);
    await screen.findByRole("progressbar", { name: "Bu dönem kalan yeni satır" });
    expect(screen.queryByRole("progressbar", { name: "Bugün kalan" })).toBeNull();
    ilk.unmount();
    kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 100 } });
    const iki = render(<CanliCeviri onOyunaGit={vi.fn()} />);
    await screen.findByRole("progressbar", { name: "Bu dönem kalan yeni satır" });
    expect(screen.queryByRole("progressbar", { name: "Bugün kalan" })).toBeNull(); // günlük kalan bilinmiyor
    iki.unmount();
    kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 100, gunKalan: 40 } });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByRole("progressbar", { name: "Bugün kalan" })).toBeTruthy();
  });

  it.each([
    ["hak_bitti", { durum: "hak_bitti", tur: "destekci", kalan: 0, gunKalan: 100 }, /Bu ayki çeviri hakkın doldu/],
    ["gun_bitti", { durum: "etkin", tur: "destekci", kalan: 100, gunKalan: 0 }, /Bugünkü çeviri hakkın doldu/],
  ] as const)("%s_uyarisi_bir_kez", async (kod, lisans, metin) => {
    kur({ ...taban, lisans, hata: kod });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    await screen.findByRole("button", { name: /Canlı Çeviriyi Başlat/ });
    expect(screen.getAllByText(metin)).toHaveLength(1);
  });

  it("eslestirme_numarasi_varken_deneme_dugmesi_kalir", async () => {
    kur({ ...taban, eslesme: { numara: "482913", bitis: Date.now() + 1e6 } });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText("482913")).toBeTruthy();
    expect(screen.getByRole("button", { name: /7 gün ücretsiz dene/ })).toBeTruthy();
  });

  it("suresi_biten_denemede_deneme_dugmesi_yok", async () => {
    kur({ ...taban, lisans: { durum: "suresi_bitti", tur: "deneme" } });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByRole("button", { name: /Destekçi ol · aylık 75/ })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /7 gün ücretsiz dene/ })).toBeNull();
  });

  it("suresi_biten_destekcide_deneme_dugmesi_var", async () => {
    kur({ ...taban, lisans: { durum: "suresi_bitti", tur: "destekci" } });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByRole("button", { name: /7 gün ücretsiz dene/ })).toBeTruthy();
  });

  it("kontrol_ekraninda_kayit_hatasi_discord_dugmesi", async () => {
    const tv = kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 5, gunKalan: 5 }, sunucuHatasi: "kayit" });
    render(<CanliCeviri onOyunaGit={vi.fn()} />);
    expect(await screen.findByText(/Lisans bu bilgisayara kaydedilemedi/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Discord'a yaz" }));
    expect(tv.disBaglanti).toHaveBeenCalledWith("https://discord.gg/nnNUCMUBJx");
  });

  it("fark_kart_basliklari_iki_noktasiz", async () => {
    kur(taban);
    const { container } = render(<CanliCeviri onOyunaGit={vi.fn()} />);
    await screen.findByText(/Her şey çevrilmez/);
    const basliklar = [...container.querySelectorAll(".canli-fark-kutu strong")].map((b) => b.textContent);
    expect(basliklar).toEqual(["Türkçe paket (ücretsiz)", "Canlı çeviri"]);
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
  it("destekci_baska_bilgisayar_ekler_deneme_goremez", async () => {
    const tv = kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 100, gunKalan: 50 } });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    const giris = await screen.findByLabelText("Diğer bilgisayarın eşleştirme numarası");
    fireEvent.change(giris, { target: { value: " tv-7k2m " } });
    fireEvent.click(screen.getByRole("button", { name: /Bilgisayarı ekle/ }));
    await waitFor(() => expect(tv.canliCihazEkle).toHaveBeenCalledWith("TV-7K2M"));
    expect(await screen.findByText(/birkaç saniye içinde/)).toBeTruthy();
    cleanup();
    kur({ ...taban, lisans: { durum: "etkin", tur: "deneme", kalan: 100 } });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    await screen.findByRole("heading", { name: /Deneme/ });
    expect(screen.queryByLabelText("Diğer bilgisayarın eşleştirme numarası")).toBeNull();
  });

  it("cihaz_ekle_hatasi_gosterilir", async () => {
    kur({ ...taban, lisans: { durum: "etkin", tur: "ust", kalan: 100, gunKalan: 50 } }, {
      canliCihazEkle: vi.fn(() => Promise.resolve({ hata: "numara_yok" })),
    });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    fireEvent.change(await screen.findByLabelText("Diğer bilgisayarın eşleştirme numarası"), { target: { value: "TV-ABCD" } });
    fireEvent.click(screen.getByRole("button", { name: /Bilgisayarı ekle/ }));
    expect(await screen.findByText(kodMetni("numara_yok"))).toBeTruthy();
  });
  it("denemede_destek_ol_numara_alir_ve_gosterir", async () => {
    const deneme: CanliDurum = { ...taban, lisans: { durum: "etkin", tur: "deneme", kalan: 1500 } };
    const tv = kur(deneme);
    render(<CanliCeviri onOyunaGit={() => {}} />);
    fireEvent.click(await screen.findByRole("button", { name: /Destekçi ol/ }));
    await waitFor(() => expect(tv.canliDestekOl).toHaveBeenCalledWith("destekci"));
    cleanup();
    kur({ ...deneme, eslesme: { numara: "TV-7K2M", bitis: Date.now() + 3600_000 } });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    expect(await screen.findByText("TV-7K2M")).toBeTruthy();
    expect(screen.getByText(/Ödeme sayfası tarayıcında açıldı/)).toBeTruthy();
    // Deneme sürerken Başlat düğmesi de yerinde.
    expect(screen.getByRole("button", { name: /Canlı Çeviriyi Başlat/ })).toBeTruthy();
  });

  it("denemede_hak_bitince_ek_hak_degil_destekci_ol", async () => {
    kur({ ...taban, lisans: { durum: "hak_bitti", tur: "deneme", kalan: 0 } });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    expect(await screen.findByRole("button", { name: /Destekçi ol/ })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Ek hak al/ })).toBeNull();
  });

  it("destekcide_destek_ol_karti_yok", async () => {
    kur({ ...taban, lisans: { durum: "etkin", tur: "destekci", kalan: 100, gunKalan: 50 } });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    await screen.findByRole("heading", { name: /Destekçi/ });
    expect(screen.queryByRole("button", { name: /Destekçi ol/ })).toBeNull();
  });
  it("denemede_destek_ol_hatasi_gosterilir", async () => {
    kur({ ...taban, lisans: { durum: "etkin", tur: "deneme", kalan: 1500 } }, {
      canliDestekOl: vi.fn(() => Promise.resolve({ hata: "hiz" })),
    });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    fireEvent.click(await screen.findByRole("button", { name: /Destekçi ol/ }));
    expect(await screen.findByText(kodMetni("hiz"))).toBeTruthy();
  });
  it("destekcide_hak_bitince_ek_hak_30_tl_baglantisi", async () => {
    const tv = kur({ ...taban, lisans: { durum: "hak_bitti", tur: "destekci", kalan: 0, gunKalan: 100 } });
    render(<CanliCeviri onOyunaGit={() => {}} />);
    fireEvent.click(await screen.findByRole("button", { name: /Ek hak al · 30/ }));
    await waitFor(() => expect(tv.canliDestekOl).toHaveBeenCalledWith("ek"));
  });
});
