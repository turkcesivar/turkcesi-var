// Tek tip, çizgi stilinde küçük simgeler (24×24, currentColor).

const YOLLAR = {
  oyunlar: "M6 11h4M8 9v4M15 12h.01M18 10h.01M17.32 5H6.68a4 4 0 0 0-3.98 3.59l-.9 9.06A2 2 0 0 0 3.79 20c.98 0 1.88-.55 2.32-1.43L7 17h10l.89 1.57A2.62 2.62 0 0 0 20.21 20a2 2 0 0 0 1.99-2.35l-.9-9.06A4 4 0 0 0 17.32 5Z",
  ayarlar: "M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6",
  geri: "M15 18l-6-6 6-6",
  tamam: "M20 6 9 17l-5-5",
  kalkan: "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z M9.5 9.5l5 5M14.5 9.5l-5 5",
  bilgi: "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20ZM12 16v-4M12 8h.01",
  uyari: "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0ZM12 9v4M12 17h.01",
  ara: "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16ZM21 21l-4.35-4.35",
  yukle: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12",
  indir: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3",
  dil: "M5 8l6 6M4 14l6-6 2-3M2 5h12M7 2h1M22 22l-5-10-5 10M14 18h6",
  kalem: "M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z",
  geriAl: "M3 7v6h6M21 17a9 9 0 0 0-15-6.7L3 13",
  yenile: "M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15",
  kalp: "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78Z",
  kilit: "M19 11H5a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7a2 2 0 0 0-2-2ZM7 11V7a5 5 0 0 1 10 0v4",
  yildiz: "M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M5.6 18.4l2.1-2.1M16.3 7.7l2.1-2.1",
  kapat: "M18 6 6 18M6 6l12 12",
  canli: "M3 6h18v12H3ZM7 11h4M7 14h8M15 11h2",
  sohbet: "M7.9 20A9 9 0 1 0 4 16.1L2 22Z",
} as const;

export type SimgeAdi = keyof typeof YOLLAR;

export function Simge({ ad, boyut = 18 }: { ad: SimgeAdi; boyut?: number }) {
  return (
    <svg className="simge" width={boyut} height={boyut} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={YOLLAR[ad]} />
    </svg>
  );
}
