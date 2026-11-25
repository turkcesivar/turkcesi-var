// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Hakkinda } from "../../uygulama/arayuz/sayfalar/Hakkinda.js";

afterEach(cleanup);

describe("Hakkında", () => {
  it("zorunlu atfı ve lisansı gösterir; GitHub bağlantısı resmi depoya gider", async () => {
    const disBaglanti = vi.fn();
    (window as unknown as { tv: unknown }).tv = { disBaglanti, guncellemeKur: vi.fn() };
    render(<Hakkinda surum="0.1.0" guncelleme={{ durum: "guncel" }} destekAdresi="" />);
    expect(screen.getByText(/Türkçesi Var — orijinali/).textContent).toBe("Türkçesi Var — orijinali turkcesivar, github.com/turkcesivar/turkcesi-var");
    expect(screen.getByText(/GNU GPL v3.0/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "GitHub sayfası" }));
    expect(disBaglanti).toHaveBeenCalledWith("https://github.com/turkcesivar/turkcesi-var");
    fireEvent.click(screen.getByRole("button", { name: "Çeviri hatası bildir" }));
    expect(disBaglanti).toHaveBeenLastCalledWith("https://github.com/turkcesivar/turkcesi-var/issues/new?template=ceviri-hatasi.yml");
    const topluluk = screen.getByRole("region", { name: "Topluluk" });
    expect(topluluk.textContent).toContain("sıradaki oyunu birlikte seçiyoruz");
    fireEvent.click(screen.getByRole("button", { name: /Discord'a katıl/ }));
    expect(disBaglanti).toHaveBeenLastCalledWith("https://discord.gg/nnNUCMUBJx");
    fireEvent.click(screen.getByRole("button", { name: "Kullanım koşulları" }));
    expect(screen.getByRole("dialog", { name: "Kullanım koşulları" })).toBeTruthy();
    expect(screen.getByText(/Ücretsiz kısım ve garanti/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Kapat" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });
});
