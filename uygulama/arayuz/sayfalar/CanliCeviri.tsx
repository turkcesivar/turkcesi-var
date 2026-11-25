// Canlı Çeviri: lisans ekranı (deneme / destek), kontrol ekranı (kota, başlat/durdur), engel kartları.

import { useCallback, useEffect, useRef, useState } from "react";
import { SEVIYE_TAVAN, type CanliDurum } from "../../../src/canli/tipler.js";
import { Simge } from "../bilesenler/Simge.js";
import { Bildirim, Dugme, Iskelet, sayi } from "../bilesenler/temel.js";
import { useYumusakKapat } from "../bilesenler/yumusak.js";
import {
  DENEME_DAVETI, DENEME_KARTI, DIL_ACIKLAMA, DIL_ADIMLAR, DIL_BASLIK, DIL_GELISMIS, DIL_GEREKLI_IPUCU, DIL_KOMUTU, DIL_WIN10,
  ESLESTIRME_ACIKLAMA_SON, FARK, GUN_BITTI, HAK_BITTI, KAPANIS, NASIL_BASLIK, NEDEN_UCRETLI,
  SINIRLAR, SINIRLAR_BASLIK, SURE_BITTI, durumSatiri, hileEngel, kodMetni, paketliEngel,
} from "../canli-metinler.js";
import { DESTEK_ADRESI, DISCORD_ADRESI } from "../sabitler.js";

const ac = (url: string) => void window.tv.disBaglanti(url);

function Fark() {
  return (
    <div className="canli-fark">
      {FARK.map((f) => (
        <div key={f.baslik} className="canli-fark-kutu">
          <strong>{f.baslik}</strong>
          <p>{f.metin}</p>
        </div>
      ))}
    </div>
  );
}

function Sinirlar() {
  return (
    <div className="canli-sinirlar">
      <h3>{SINIRLAR_BASLIK}</h3>
      <ul className="madde-listesi">
        {SINIRLAR.map((s) => (
          <li key={s}>{s}</li>
        ))}
      </ul>
      <p className="soluk">{KAPANIS}</p>
    </div>
  );
}

function Nasil({ katli = false }: { katli?: boolean }) {
  const icerik = (
    <>
      <Fark />
      <Sinirlar />
    </>
  );
  if (katli)
    return (
      <details className="panel canli-nasil">
        <summary>{NASIL_BASLIK}</summary>
        {icerik}
      </details>
    );
  return (
    <section className="panel canli-nasil" aria-label={NASIL_BASLIK}>
      <h2 className="panel-baslik">
        <Simge ad="bilgi" /> {NASIL_BASLIK}
      </h2>
      {icerik}
    </section>
  );
}

function OnayPenceresi({ onayla, vazgec }: { onayla: () => void; vazgec: () => void }) {
  const [kapaniyor, kapat] = useYumusakKapat(vazgec);
  return (
    <div className={`perde${kapaniyor ? " kapaniyor" : ""}`}>
      <div className="pencere canli-pencere" role="dialog" aria-modal="true" aria-labelledby="canli-onay-baslik">
        <h2 id="canli-onay-baslik">{NASIL_BASLIK}</h2>
        <Fark />
        <Sinirlar />
        <div className="eylemler">
          <Dugme tur="ana" buyuk onClick={onayla}>
            Anladım, denemeyi başlat
          </Dugme>
          <Dugme tur="ikincil" buyuk onClick={kapat}>
            Vazgeç
          </Dugme>
        </div>
      </div>
    </div>
  );
}

/** Windows'ta İngilizce metin tanıma yoksa: nasıl kurulacağı, ayarları açma ve yeniden denetleme. */
function DilPaketiKarti() {
  const [denetleniyor, setDenetleniyor] = useState(false);
  const denetle = async () => {
    setDenetleniyor(true);
    try {
      await window.tv.canliDilDenetle();
    } finally {
      setDenetleniyor(false);
    }
  };
  return (
    <section className="panel canli-dil" aria-label={DIL_BASLIK}>
      <h2 className="panel-baslik">
        <Simge ad="uyari" /> {DIL_BASLIK}
      </h2>
      <p>{DIL_ACIKLAMA}</p>
      <ol className="canli-adimlar">
        {DIL_ADIMLAR.map((a) => (
          <li key={a}>{a}</li>
        ))}
      </ol>
      <div className="eylemler">
        <Dugme tur="ana" simge="ayarlar" onClick={() => void window.tv.canliDilAyarlari()}>
          Dil ayarlarını aç
        </Dugme>
        <Dugme tur="ikincil" simge="yenile" donen={denetleniyor} pasif={denetleniyor} onClick={() => void denetle()}>
          Yeniden denetle
        </Dugme>
      </div>
      <p className="soluk">{DIL_WIN10}</p>
      <details className="canli-gelismis">
        <summary>Dil ekleyince de olmadıysa</summary>
        <p className="soluk">{DIL_GELISMIS}</p>
        <code className="canli-komut">{DIL_KOMUTU}</code>
      </details>
    </section>
  );
}

function Cubuk({ etiket, kalan, tavan }: { etiket: string; kalan: number; tavan: number }) {
  const yuzde = tavan > 0 ? Math.max(0, Math.min(100, (kalan / tavan) * 100)) : 0;
  return (
    <div className="canli-cubuk">
      <div className="canli-cubuk-ust">
        <span>{etiket}</span>
        <strong>
          {sayi(kalan)} / {sayi(tavan)}
        </strong>
      </div>
      <div className="ilerleme-iz" role="progressbar" aria-label={etiket} aria-valuenow={Math.round(yuzde)} aria-valuemin={0} aria-valuemax={100}>
        <div className="ilerleme-dolgu" style={{ width: `${yuzde}%` }} />
      </div>
    </div>
  );
}

export function CanliCeviri({ onOyunaGit }: { onOyunaGit: (kimlik: string) => void }) {
  const [d, setD] = useState<CanliDurum>();
  const [cihazHatasi, setCihazHatasi] = useState(false);
  const [onay, setOnay] = useState(false);
  const [hata, setHata] = useState<string>();
  const [isleniyor, setIsleniyor] = useState(false);
  const [denemeKartiKapali, setDenemeKartiKapali] = useState(false);
  const canli = useRef(true);

  const yukle = useCallback(async () => {
    try {
      const x = await window.tv.canliDurum();
      if (canli.current) {
        setCihazHatasi(false);
        setD(x);
      }
    } catch {
      if (canli.current) setCihazHatasi(true);
    }
  }, []);

  useEffect(() => {
    canli.current = true;
    void yukle();
    const birak = window.tv.canliDinle((x) => {
      setCihazHatasi(false);
      setD(x);
    });
    return () => {
      canli.current = false;
      birak();
    };
  }, [yukle]);

  const yenidenDene = () => {
    setCihazHatasi(false);
    setD(undefined);
    setHata(undefined);
    void window.tv.canliYenile().then(yukle, yukle);
  };

  /** Deneme / eşleştirme: hata kodu varsa gösterir, yoksa durumu yeniler. */
  const islem = async (f: () => Promise<{ hata?: string }>) => {
    setIsleniyor(true);
    setHata(undefined);
    try {
      const r = await f();
      if (r.hata) setHata(r.hata);
      await yukle();
    } catch {
      setHata("cihaz");
    } finally {
      if (canli.current) setIsleniyor(false);
    }
  };

  let govde: React.ReactNode;
  if (cihazHatasi) {
    govde = (
      <Bildirim tur="tehlike">
        <p>{kodMetni("cihaz")}</p>
        <div className="eylemler">
          <Dugme simge="yenile" onClick={yenidenDene}>
            Yeniden dene
          </Dugme>
        </div>
      </Bildirim>
    );
  } else if (!d || d.yukleniyor) {
    govde = <Iskelet tur="genis" adet={2} />;
  } else if (d.lisans === null) {
    govde = (
      <Bildirim tur="uyari">
        <p>Sunucuya ulaşılamıyor. İnternet bağlantını kontrol edip yeniden dene.</p>
        {d.sunucuHatasi && d.sunucuHatasi !== "cevrimdisi" && <p className="soluk">{kodMetni(d.sunucuHatasi)}</p>}
        <div className="eylemler">
          <Dugme simge="yenile" onClick={yenidenDene}>
            Yeniden dene
          </Dugme>
        </div>
      </Bildirim>
    );
  } else if (d.lisans.durum === "lisanssiz" || d.lisans.durum === "suresi_bitti") {
    const l = d.lisans;
    const kod = hata ?? d.sunucuHatasi;
    // Süresi biten deneme yeniden denenemez (sunucu 409 döner).
    const denemeOlur = !(l.durum === "suresi_bitti" && l.tur === "deneme");
    const dilYok = d.ocrHazir === false;
    const denemeDugmesi = (tur: "ana" | "ikincil") =>
      denemeOlur && (
        <Dugme
          tur={tur}
          buyuk
          simge="canli"
          pasif={isleniyor || dilYok}
          title={dilYok ? DIL_GEREKLI_IPUCU : undefined}
          onClick={() => setOnay(true)}
        >
          7 gün ücretsiz dene
        </Dugme>
      );
    govde = (
      <>
        {onay && (
          <OnayPenceresi
            vazgec={() => setOnay(false)}
            onayla={() => {
              setOnay(false);
              void islem(() => window.tv.canliDeneme());
            }}
          />
        )}
        {dilYok && <DilPaketiKarti />}
        {l.durum === "suresi_bitti" && <Bildirim tur="bilgi">{SURE_BITTI}</Bildirim>}
        {l.durum === "suresi_bitti" && l.tur === "deneme" && (
          <div className="canli-davet">
            <span>{DENEME_DAVETI}</span>
            <Dugme tur="ikincil" simge="sohbet" onClick={() => ac(DISCORD_ADRESI)}>
              Discord&apos;a katıl
            </Dugme>
          </div>
        )}
        {kod && !(kod === "ocr_dil_yok" && dilYok) && (
          <Bildirim tur="tehlike">
            <p>{kodMetni(kod)}</p>
            {kod === "kayit" && (
              <div className="eylemler">
                <Dugme simge="sohbet" onClick={() => ac(DISCORD_ADRESI)}>
                  Discord&apos;a yaz
                </Dugme>
              </div>
            )}
          </Bildirim>
        )}
        <section className="panel" aria-label="Canlı çeviri">
          <h2 className="panel-baslik">
            <Simge ad="canli" /> Canlı çeviri
          </h2>
          <p>Türkçesi olmayan oyunları, ekrandaki yazıyı anında çevirerek beklemeden oynamanı sağlar.</p>
          {d.eslesme ? (
            <>
              <div className="canli-eslesme">
                <p>
                  Eşleştirme numaran: <strong className="canli-numara">{d.eslesme.numara}</strong> — Kreosus&apos;ta destek olurken bu numarayı mesaj kısmına yaz. {ESLESTIRME_ACIKLAMA_SON}
                </p>
                <div className="eylemler">
                  <Dugme tur="ana" simge="kalp" onClick={() => ac(DESTEK_ADRESI)}>
                    Kreosus&apos;u aç
                  </Dugme>
                  <Dugme tur="ikincil" simge="sohbet" onClick={() => ac(DISCORD_ADRESI)}>
                    Discord&apos;da destek ol
                  </Dugme>
                </div>
              </div>
              {denemeOlur && <div className="eylemler">{denemeDugmesi("ikincil")}</div>}
            </>
          ) : (
            <div className="eylemler">
              {denemeDugmesi("ana")}
              <Dugme tur="ikincil" buyuk simge="kalp" pasif={isleniyor} onClick={() => void islem(() => window.tv.canliEslestir())}>
                Destek ol ve etkinleştir
              </Dugme>
            </div>
          )}
        </section>
        <Nasil />
        <section className="panel" aria-label="Neden ücretli">
          <h2 className="panel-baslik">
            <Simge ad="kalp" /> Neden ücretli?
          </h2>
          <p>{NEDEN_UCRETLI}</p>
        </section>
      </>
    );
  } else {
    const l = d.lisans;
    const tur = l.tur ?? "deneme";
    const tavan = SEVIYE_TAVAN[tur];
    const acik = d.calisma !== "kapali";
    const sorun = d.uyari ?? d.hata ?? d.sunucuHatasi;
    const gunBitti = l.gunKalan === 0;
    const hakBlogu = l.durum === "hak_bitti";
    const gunBlogu = !hakBlogu && gunBitti;
    // Aynı uyarı aşağıdaki özel blokta gösteriliyorsa sorun bandında tekrarlanmaz.
    const dilYok = d.ocrHazir === false;
    const sorunGoster =
      !!sorun && !((sorun === "hak_bitti" && hakBlogu) || (sorun === "gun_bitti" && gunBlogu) || (sorun === "ocr_dil_yok" && dilYok));
    govde = (
      <>
        {dilYok && <DilPaketiKarti />}
        {tur === "deneme" && !denemeKartiKapali && (
          <div className="canli-davet" role="status">
            <span>{DENEME_KARTI}</span>
            <Dugme tur="ikincil" simge="sohbet" onClick={() => ac(DISCORD_ADRESI)}>
              Discord&apos;a katıl
            </Dugme>
            <button type="button" className="canli-kapat" aria-label="Kartı kapat" onClick={() => setDenemeKartiKapali(true)}>
              <Simge ad="kapat" boyut={16} />
            </button>
          </div>
        )}
        <section className="panel" aria-label="Lisans">
          <h2 className="panel-baslik">
            <Simge ad="canli" /> {tur === "deneme" ? "Deneme" : tur === "ust" ? "Üst seviye destekçi" : "Destekçi"}
            {l.donemBitis && <span className="surum">{new Date(l.donemBitis).toLocaleDateString("tr-TR")} tarihine kadar</span>}
          </h2>
          <Cubuk etiket="Bu dönem kalan yeni satır" kalan={l.kalan ?? 0} tavan={tavan.ay} />
          {/* Denemede günlük sınır dönem sınırına eşit; günlük kalan bilinmiyorsa çubuk gösterilmez. */}
          {tur !== "deneme" && l.gunKalan !== undefined && <Cubuk etiket="Bugün kalan" kalan={l.gunKalan} tavan={tavan.gun} />}
          <div className="eylemler">
            {acik ? (
              <Dugme tur="tehlike" buyuk onClick={() => void window.tv.canliDurdur()}>
                ■ Durdur
              </Dugme>
            ) : (
              <Dugme tur="ana" buyuk pasif={dilYok} title={dilYok ? DIL_GEREKLI_IPUCU : undefined} onClick={() => void window.tv.canliBaslat()}>
                ▶ Canlı Çeviriyi Başlat
              </Dugme>
            )}
          </div>
          <p className="soluk canli-durum">{durumSatiri(d.calisma, d.oyunAdi)}</p>
        </section>
        {d.engel && (
          <Bildirim tur={d.engel.tur === "paketli" ? "bilgi" : "uyari"}>
            <p>{d.engel.tur === "paketli" ? paketliEngel(d.engel.oyunAdi) : hileEngel(d.engel.oyunAdi)}</p>
            {d.engel.tur === "paketli" && (
              <div className="eylemler">
                <Dugme tur="ana" simge="dil" onClick={() => onOyunaGit(d.engel!.kimlik ?? "")}>
                  Türkçe yap
                </Dugme>
              </div>
            )}
          </Bildirim>
        )}
        {sorun && sorunGoster && (
          <Bildirim tur="uyari">
            <p>{kodMetni(sorun)}</p>
            {sorun === "kayit" && (
              <div className="eylemler">
                <Dugme simge="sohbet" onClick={() => ac(DISCORD_ADRESI)}>
                  Discord&apos;a yaz
                </Dugme>
              </div>
            )}
          </Bildirim>
        )}
        {hakBlogu && (
          <Bildirim tur="uyari">
            <p>{HAK_BITTI}</p>
            <div className="eylemler">
              <Dugme tur="ana" simge="kalp" onClick={() => ac(DESTEK_ADRESI)}>
                Ek hak al
              </Dugme>
            </div>
          </Bildirim>
        )}
        {gunBlogu && (
          <Bildirim tur="uyari">
            <p>{GUN_BITTI}</p>
            <div className="eylemler">
              <Dugme tur="ana" simge="kalp" onClick={() => ac(DESTEK_ADRESI)}>
                Seviyeni yükselt
              </Dugme>
            </div>
          </Bildirim>
        )}
        <Nasil katli />
      </>
    );
  }

  return (
    <div className="sayfa canli-sayfa">
      <header className="sayfa-baslik">
        <div>
          <h1>Canlı Çeviri</h1>
          <p className="soluk">Türkçesi olmayan oyunlarda ekrandaki yazıyı anında Türkçeye çevirir.</p>
        </div>
      </header>
      <div className="canli-icerik">{govde}</div>
    </div>
  );
}
