// Oyunlarım: Türkçesi hazır oyunlar (bulunamasa da, klasör seçme yardımıyla) ve kütüphanedeki diğer oyunlar.

import { useMemo, useState } from "react";
import type { ArayuzOyunu, DesteklenenOyun } from "../../../src/servis/api.js";
import type { OyunDurumu } from "../../../src/servis/tipler.js";
import { Simge, type SimgeAdi } from "../bilesenler/Simge.js";
import { Dugme, Iskelet, Kapak, Rozet, sirayla, type RozetTuru } from "../bilesenler/temel.js";
import { DISCORD_ADRESI, OYUN_ISTEK_ADRESI } from "../sabitler.js";

export function rozetBul(o: ArayuzOyunu, d: OyunDurumu | undefined): { metin: string; tur: RozetTuru; simge?: SimgeAdi } {
  if (o.durum === "hile-korumali") return { metin: "Hile korumalı", tur: "tehlike", simge: "kalkan" };
  if (o.durum === "desteklenmiyor") return { metin: "Desteklenmiyor", tur: "notr" };
  if (d?.yama === "degismis") return { metin: "Yeniden kurulmalı", tur: "uyari", simge: "uyari" };
  if (d?.yama === "kurulu") return { metin: "Türkçe kurulu", tur: "basari", simge: "tamam" };
  if (d && d.ceviri.uygulanabilir > 0) return { metin: "Türkçe hazır", tur: "vurgu", simge: "dil" };
  if (d) return { metin: "Türkçesi yakında", tur: "bilgi" };
  return { metin: "Hazırlanıyor…", tur: "notr" };
}

/**
 * Bir kurulumun arayüzdeki anahtarı: oyun + klasör. Aynı oyun hem Steam'de hem Epic'te kuruluysa
 * iki kartın durumu ve işlemi birbirine karışmaz.
 */
export const oyunAnahtari = (o: { kimlik?: string; klasor: string }) => `${o.kimlik ?? ""}|${o.klasor.toLowerCase()}`;

export const platformAdi = (o: ArayuzOyunu) => (o.platform === "epic" ? "Epic Games" : o.platform === "elle" || o.elle ? "Elle seçildi" : "Steam");

function OneCikanKart({ o, d, onSec, sira }: { o: ArayuzOyunu; d?: OyunDurumu; onSec: (o: ArayuzOyunu) => void; sira: number }) {
  const r = rozetBul(o, d);
  const [logoYok, setLogoYok] = useState(false);
  return (
    <button type="button" className="one-cikan belir" style={sirayla(sira)} onClick={() => onSec(o)} aria-label={`${o.ad} — ${r.metin}`}>
      <Kapak ad={o.ad} src={o.gorseller.afis ?? o.gorseller.baslik} oran="yatay" />
      <div className="one-cikan-karartma" />
      <div className="one-cikan-icerik">
        {o.gorseller.logo && !logoYok ? (
          <img className="one-cikan-logo" src={o.gorseller.logo} alt="" onError={() => setLogoYok(true)} />
        ) : (
          <span className="one-cikan-ad">{o.ad}</span>
        )}
        <div className="one-cikan-alt">
          <Rozet tur={r.tur} simge={r.simge}>
            {r.metin}
          </Rozet>
          {d?.paket && <span className="one-cikan-bilgi">{d.paket.satir.toLocaleString("tr-TR")} satır Türkçe</span>}
          <span className="one-cikan-bilgi">· {platformAdi(o)}</span>
        </div>
      </div>
    </button>
  );
}

/** Türkçesi hazır ama bu bilgisayarda bulunamayan oyun: yalnız ad + düğme; dosyalar ve yol düğmenin ipucunda. */
function EksikSatir({ d, onKlasorSec }: { d: DesteklenenOyun; onKlasorSec: (kimlik: string) => void }) {
  const ipucu = `Klasörde şu dosyalar bulunur: ${d.tanimaDosyalari.join(", ")}. Steam'de genellikle …\\steamapps\\common\\${d.klasorAdi}`;
  return (
    <div className="eksik-satir" role="group" aria-label={`${d.ad} — bilgisayarında bulunamadı`}>
      <span className="eksik-satir-ad">{d.ad}</span>
      <Dugme tur="ikincil" simge="ara" onClick={() => onKlasorSec(d.kimlik)} title={ipucu}>
        Klasörü seç
      </Dugme>
    </div>
  );
}

/**
 * Bulunamayan Türkçesi hazır oyunlar: kapanabilir liste. Her zaman kapalı gelir; yalnız arama
 * yapılırken açılır (yüzlerce oyunun Türkçesi olsa da ekran kalabalıklaşmaz).
 */
function EksikListe({ eksik, acik, onKlasorSec }: { eksik: DesteklenenOyun[]; acik: boolean; onKlasorSec: (kimlik: string) => void }) {
  return (
    <details className="eksik-liste" open={acik} key={String(acik)}>
      <summary>
        Bilgisayarında bulunamayan Türkçesi hazır oyunlar <span className="sayac">{eksik.length}</span>
      </summary>
      <p className="soluk eksik-aciklama">Oyunu Steam ya da Epic dışından kurduysan klasörünü seç; üst klasörü seçersen içinde kendim ararım.</p>
      <div className="eksik-satirlar">
        {eksik.map((d) => (
          <EksikSatir key={d.kimlik} d={d} onKlasorSec={onKlasorSec} />
        ))}
      </div>
    </details>
  );
}

function Kart({ o, onSec, sira }: { o: ArayuzOyunu; onSec: (o: ArayuzOyunu) => void; sira: number }) {
  const r = rozetBul(o, undefined);
  return (
    <button type="button" className={`kart kart-${o.durum} belir`} style={sirayla(sira)} onClick={() => onSec(o)} aria-label={`${o.ad} — ${r.metin}`}>
      <Kapak ad={o.ad} src={o.gorseller.kapak} />
      <div className="kart-rozet">
        <Rozet tur={r.tur} simge={r.simge}>
          {r.metin}
        </Rozet>
      </div>
      <div className="kart-alt">
        <span className="kart-ad">{o.ad}</span>
        <span className="kart-platform">{platformAdi(o)}</span>
      </div>
    </button>
  );
}

export function Oyunlarim(p: {
  oyunlar: ArayuzOyunu[];
  desteklenenler: DesteklenenOyun[];
  durumlar: Record<string, OyunDurumu | undefined>;
  yukleniyor: boolean;
  steamBulundu: boolean;
  onSec: (o: ArayuzOyunu) => void;
  onKlasorSec: (kimlik?: string) => void;
  /** Steam ve Epic'i yeniden tarar (yeni kurulan oyunlar için). */
  onYenile?: () => void;
  /** Canlı Çeviri sayfasına gider. */
  onCanliyaGit?: () => void;
}) {
  const [arama, setArama] = useState("");
  const [platform, setPlatform] = useState<"hepsi" | "steam" | "epic">("hepsi");
  const aranan = arama.trim().toLocaleLowerCase("tr");
  const uyar = (ad: string) => !aranan || ad.toLocaleLowerCase("tr").includes(aranan);
  // Platform filtresi yalnızca iki platformda da oyunu olana gösterilir.
  const ikiPlatform = p.oyunlar.some((o) => o.platform === "epic") && p.oyunlar.some((o) => o.platform !== "epic");
  const platformUyar = (o: ArayuzOyunu) => !ikiPlatform || platform === "hepsi" || (platform === "epic") === (o.platform === "epic");

  const { hazir, eksik, diger } = useMemo(() => {
    const destek = p.oyunlar.filter((o) => o.durum === "destekleniyor");
    return {
      hazir: destek.filter((o) => uyar(o.ad) && platformUyar(o)),
      eksik: platform !== "hepsi" && ikiPlatform ? [] : p.desteklenenler.filter((d) => !destek.some((o) => o.kimlik === d.kimlik) && uyar(d.ad)),
      diger: p.oyunlar.filter((o) => o.durum !== "destekleniyor" && uyar(o.ad) && platformUyar(o)),
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [p.oyunlar, p.desteklenenler, aranan, platform, ikiPlatform]);

  const durum = (o: ArayuzOyunu) => (o.kimlik ? p.durumlar[oyunAnahtari(o)] : undefined);

  return (
    <div className="sayfa">
      <header className="sayfa-baslik">
        <div>
          <h1>Oyunlarım</h1>
          <p className="soluk">
            {p.yukleniyor
              ? "Oyunların aranıyor…"
              : p.oyunlar.length > 0
                ? `Bilgisayarında kurulu ${p.oyunlar.length} oyun bulundu (Steam ve Epic Games)`
                : "Steam ya da Epic Games'te kurulu oyun bulunamadı. Oyununun klasörünü kendin seçebilirsin."}
          </p>
        </div>
        <div className="arac-cubugu">
          <label className="arama">
            <Simge ad="ara" />
            <input type="search" role="searchbox" placeholder="Oyun ara" value={arama} onChange={(e) => setArama(e.target.value)} />
          </label>
          {ikiPlatform && (
            <div className="secici" role="tablist" aria-label="Platform">
              {(
                [
                  ["hepsi", "Tümü"],
                  ["steam", "Steam"],
                  ["epic", "Epic Games"],
                ] as const
              ).map(([k, ad]) => (
                <button key={k} type="button" role="tab" aria-selected={platform === k} className={platform === k ? "secili" : ""} onClick={() => setPlatform(k)}>
                  {ad}
                </button>
              ))}
            </div>
          )}
          <Dugme
            tur="ikincil"
            simge="yenile"
            donen={p.yukleniyor}
            pasif={p.yukleniyor}
            onClick={p.onYenile}
            title="Yeni kurduğun oyunları bulmak için Steam ve Epic Games'i yeniden tara"
          >
            Yenile
          </Dugme>
          <Dugme tur="ikincil" simge="yukle" onClick={() => p.onKlasorSec()} title="Oyun listede yoksa klasörünü göster">
            Klasör seç
          </Dugme>
        </div>
      </header>

      {p.yukleniyor && (
        <section aria-label="Yükleniyor" className="bolum">
          <div className="one-cikanlar">
            <Iskelet tur="genis" adet={1} />
          </div>
          <div className="izgara iskelet-izgara">
            <Iskelet tur="dikey" adet={10} />
          </div>
        </section>
      )}

      {!p.yukleniyor && (hazir.length > 0 || eksik.length > 0) && (
        <section aria-label="Türkçesi hazır" className="bolum">
          <h2>
            Türkçesi hazır <span className="sayac">{hazir.length}</span>
          </h2>
          {hazir.length > 0 ? (
            <div className="one-cikanlar">
              {hazir.map((o, i) => (
                <OneCikanKart key={o.klasor} o={o} d={durum(o)} onSec={p.onSec} sira={i} />
              ))}
            </div>
          ) : aranan === "" && p.oyunlar.every((o) => o.durum !== "destekleniyor") ? (
            <div className="kompakt-kutu">
              <span className="soluk">
                {`Bilgisayarında Türkçesi hazır bir oyun bulunamadı.${eksik.length > 0 ? ` Türkçesi hazır oyunlar: ${eksik.slice(0, 4).map((d) => d.ad).join(", ")}${eksik.length > 4 ? ` ve ${eksik.length - 4} oyun daha` : ""}.` : ""}`}
              </span>
              {p.onCanliyaGit && (
                <Dugme tur="ikincil" onClick={p.onCanliyaGit}>
                  Başka oyunlar için Canlı Çeviri
                </Dugme>
              )}
            </div>
          ) : null}
          {eksik.length > 0 && <EksikListe eksik={eksik} acik={aranan !== ""} onKlasorSec={p.onKlasorSec} />}
        </section>
      )}

      {diger.length > 0 && (
        <section aria-label="Diğer oyunlar" className="bolum">
          <h2>
            Diğer oyunlar <span className="sayac">{diger.length}</span>
          </h2>
          <p className="soluk bolum-aciklama">
            Türkçesi Var oyunları kendi başına çevirmez; yalnızca Türkçesi hazırlanmış oyunlara yama kurar. Bu oyunların Türkçesi henüz yok
            ya da hile koruması olduğu için dokunulmuyor. Yeni oyunlar uygulama güncellendikçe eklenir; en çok istenenler önce gelir.{" "}
            {p.onCanliyaGit && (
              <>
                Türkçesi olmayan oyunları Canlı Çeviri ile oynayabilirsin.{" "}
                <Dugme tur="sade" onClick={p.onCanliyaGit}>
                  Canlı Çeviri
                </Dugme>
              </>
            )}
            <Dugme tur="sade" simge="sohbet" onClick={() => void window.tv.disBaglanti(DISCORD_ADRESI)}>
              Discord&apos;da oyun iste
            </Dugme>
            <Dugme tur="sade" onClick={() => void window.tv.disBaglanti(OYUN_ISTEK_ADRESI)}>
              GitHub&apos;da iste
            </Dugme>
          </p>
          <div className="izgara izgara-soluk">
            {diger.map((o, i) => (
              <Kart key={o.klasor} o={o} onSec={p.onSec} sira={i + 2} />
            ))}
          </div>
        </section>
      )}

      {!p.yukleniyor && hazir.length + eksik.length + diger.length === 0 && <p className="bos-durum">Aramana uyan oyun yok.</p>}
    </div>
  );
}
