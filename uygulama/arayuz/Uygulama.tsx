// Uygulama kabuğu (oyuncu sürümü): sol menü, Oyunlarım / Oyun / Hakkında, oyun başına işlemler,
// açılışta güncellenmiş oyunlara Türkçeyi kendiliğinden yeniden kurma, uygulama güncellemesi.

import { useCallback, useEffect, useRef, useState } from "react";
import type { ArayuzOyunu, DesteklenenOyun, GuncellemeDurumu } from "../../src/servis/api.js";
import type { Ilerleme, OyunDurumu } from "../../src/servis/tipler.js";
import { GuncellemeUyarisi } from "./bilesenler/GuncellemeUyarisi.js";
import { Tost } from "./bilesenler/Tost.js";
import logoAdresi from "./varliklar/logo.svg";
import { Simge, type SimgeAdi } from "./bilesenler/Simge.js";
import { Dugme } from "./bilesenler/temel.js";
import { turkceHata } from "./hata.js";
import { calistirici, type Calistir, type Mesaj } from "./islem.js";
import { CanliCeviri } from "./sayfalar/CanliCeviri.js";
import { Hakkinda, guncellemeHatasiMetni } from "./sayfalar/Hakkinda.js";
import { Oyunlarim, oyunAnahtari } from "./sayfalar/Oyunlarim.js";
import { OyunSayfasi } from "./sayfalar/OyunSayfasi.js";

export type { Mesaj } from "./islem.js";

type Sayfa = { ad: "oyunlar" } | { ad: "oyun"; oyun: ArayuzOyunu } | { ad: "canli" } | { ad: "hakkinda" };

export function Logo() {
  return (
    <div className="logo">
      <img className="logo-resim" src={logoAdresi} alt="Türkçesi Var" draggable={false} />
    </div>
  );
}

function MenuDugmesi({ simge, ad, secili, onClick }: { simge: SimgeAdi; ad: string; secili: boolean; onClick: () => void }) {
  return (
    <button type="button" className={`menu-dugmesi${secili ? " secili" : ""}`} onClick={onClick} aria-current={secili ? "page" : undefined}>
      <Simge ad={simge} />
      {ad}
    </button>
  );
}

export function Uygulama() {
  const [sayfa, setSayfa] = useState<Sayfa>({ ad: "oyunlar" });
  const [oyunlar, setOyunlar] = useState<ArayuzOyunu[]>([]);
  const [desteklenenler, setDesteklenenler] = useState<DesteklenenOyun[]>([]);
  const [steamBulundu, setSteamBulundu] = useState(true);
  const [durumlar, setDurumlar] = useState<Record<string, OyunDurumu | undefined>>({});
  const [yukleniyor, setYukleniyor] = useState(true);
  const [islemler, setIslemler] = useState<Record<string, Ilerleme | undefined>>({});
  const islemRef = useRef<Record<string, Ilerleme | undefined>>({});
  const [mesaj, setMesaj] = useState<NonNullable<Mesaj> & { kimlik: number }>();
  const mesajSayaci = useRef(0);
  const [guncelleme, setGuncelleme] = useState<GuncellemeDurumu>({ durum: "kapali" });
  const [surum, setSurum] = useState("");

  /** Kısa bildirim: süresini ve yumuşak giriş/çıkışını Tost bileşeni yönetir. */
  const goster = useCallback((m: Mesaj) => {
    setMesaj(m ? { ...m, kimlik: ++mesajSayaci.current } : undefined);
  }, []);
  const mesajBitti = useCallback(() => setMesaj(undefined), []);

  const durumYenile = useCallback(
    async (o: ArayuzOyunu): Promise<OyunDurumu | undefined> => {
      if (!o.kimlik) return undefined;
      try {
        const d = await window.tv.oyunDurumu(o.kimlik, o.klasor);
        setDurumlar((x) => ({ ...x, [oyunAnahtari(o)]: d }));
        return d;
      } catch (e) {
        goster({ tur: "tehlike", metin: turkceHata(e) });
        return undefined;
      }
    },
    [goster],
  );

  /** Oyuna bağlı işlem yürütücü: güncel durum `islemRef`'te (eski kapanışlar yanılmasın), görüntü `islemler`'de. */
  const calistir = useCallback(
    (o: ArayuzOyunu): Calistir => {
      const k = oyunAnahtari(o);
      return calistirici({
        suren: () => islemRef.current[k],
        ayarla: (i) => {
          islemRef.current = { ...islemRef.current, [k]: i };
          setIslemler(islemRef.current);
        },
        bildir: goster,
        yenile: async () => void (await durumYenile(o)),
      });
    },
    [goster, durumYenile],
  );

  const turkceYap = useCallback(
    (o: ArayuzOyunu, basari = "Türkçe kuruldu. Oyunu açabilirsin!") =>
      calistir(o)("Türkçe kuruluyor", (il) => window.tv.turkceYap(o.kimlik!, o.klasor, il), basari),
    [calistir],
  );

  const tara = useCallback(async () => {
    try {
      const t = await window.tv.oyunlariTara();
      setOyunlar(t.oyunlar);
      setDesteklenenler(t.desteklenenler);
      setSteamBulundu(t.steamBulundu);
      setYukleniyor(false);
      return t.oyunlar;
    } catch (e) {
      setYukleniyor(false);
      goster({ tur: "tehlike", metin: turkceHata(e) });
      return [];
    }
  }, [goster]);

  useEffect(() => {
    void window.tv.surum().then(setSurum, () => {});
    void window.tv.guncellemeDurumu().then(setGuncelleme, () => {});
    return window.tv.guncellemeDinle(setGuncelleme);
  }, []);

  useEffect(() => {
    void (async () => {
      const liste = await tara();
      // Geliştirme kısayolu: #oyun=<kimlik> | #hakkinda
      const h = new URLSearchParams(location.hash.slice(1));
      const acilacak = liste.find((o) => o.kimlik && o.kimlik === h.get("oyun"));
      if (acilacak) setSayfa({ ad: "oyun", oyun: acilacak });
      if (h.has("hakkinda")) setSayfa({ ad: "hakkinda" });
      for (const o of liste) {
        if (o.durum !== "destekleniyor") continue;
        const d = await durumYenile(o);
        // Steam oyunu güncellediyse Türkçeyi kendiliğinden yeniden kur.
        if (d?.yama === "degismis" && d.ceviri.uygulanabilir > 0) {
          await turkceYap(o, `${o.ad} güncellenmişti; Türkçe kendiliğinden yeniden kuruldu.`);
        } else if (d?.ceviriEski && d.ceviri.uygulanabilir > 0) {
          // Uygulama güncellendi ve yeni bir çeviri paketi getirdi: düzeltmeler oyuna da gitsin.
          await turkceYap(o, `${o.ad} için Türkçe çeviri güncellendi; yenisi kendiliğinden kuruldu.`);
        }
      }
    })();
  }, [tara, durumYenile, turkceYap]);

  /** "Yenile": Steam ve Epic'i yeniden tarar; yeni kurulan oyunları bildirir. */
  const oyunlariYenile = async () => {
    const onceki = new Set(oyunlar.map((o) => o.klasor.toLowerCase()));
    setYukleniyor(true);
    const liste = await tara();
    for (const o of liste) if (o.durum === "destekleniyor") await durumYenile(o);
    const yeni = liste.filter((o) => !onceki.has(o.klasor.toLowerCase()));
    goster(
      yeni.length
        ? { tur: "basari", metin: `${yeni.length} yeni oyun bulundu: ${yeni.map((o) => o.ad).join(", ")}` }
        : { tur: "bilgi", metin: "Yeni oyun bulunamadı; liste güncel." },
    );
  };

  /** "Güncellemeleri denetle": yeni sürüm varsa "Güncelleme var" penceresi kendiliğinden açılır. */
  const [denetleniyor, setDenetleniyor] = useState(false);
  const guncellemeDenetle = async () => {
    setDenetleniyor(true);
    try {
      const d = await window.tv.guncellemeDenetle();
      setGuncelleme(d);
      if (d.durum === "guncel") goster({ tur: "basari", metin: `En güncel sürümü kullanıyorsun (${surum}).` });
      else if (d.durum === "hata") goster({ tur: d.neden === "yayin-yok" ? "bilgi" : "tehlike", metin: guncellemeHatasiMetni(d.neden) });
      else if (d.durum === "kapali") goster({ tur: "bilgi", metin: "Güncelleme denetimi yalnızca kurulu uygulamada çalışır." });
    } catch (e) {
      goster({ tur: "tehlike", metin: turkceHata(e) });
    } finally {
      setDenetleniyor(false);
    }
  };

  /** "Yeniden başlat ve güncelle": yazma sürerken ana süreç reddeder; nedeni kısa bir bildirimle söylenir. */
  const guncellemeKur = () => void window.tv.guncellemeKur().catch((e: unknown) => goster({ tur: "bilgi", metin: turkceHata(e) }));

  /** "İndir": yeni sürüm yalnızca kullanıcı isteyince iner; inemezse kısa bir bildirim çıkar, yeniden denenebilir. */
  const guncellemeIndir = () => void window.tv.guncellemeIndir().catch((e: unknown) => goster({ tur: "tehlike", metin: turkceHata(e) }));

  const klasorSec = async (kimlik?: string) => {
    try {
      const r = await window.tv.klasorSec(kimlik);
      if (!r) return;
      if ("hata" in r) return goster({ tur: "tehlike", metin: r.hata });
      setOyunlar((x) => [r.oyun, ...x.filter((o) => o.klasor !== r.oyun.klasor)]);
      setSayfa({ ad: "oyun", oyun: r.oyun });
      await durumYenile(r.oyun);
    } catch (e) {
      goster({ tur: "tehlike", metin: turkceHata(e) });
    }
  };

  /** Canlı Çeviri'deki "Türkçe yap": taranmış oyunlarda kimliği bulup sayfasını açar; yoksa Oyunlarım. */
  const canliOyunaGit = (kimlik: string) => {
    const o = oyunlar.find((x) => x.kimlik === kimlik);
    setSayfa(o ? { ad: "oyun", oyun: o } : { ad: "oyunlar" });
  };

  let icerik: React.ReactNode;
  if (sayfa.ad === "oyunlar") {
    icerik = (
      <Oyunlarim
        oyunlar={oyunlar}
        desteklenenler={desteklenenler}
        durumlar={durumlar}
        yukleniyor={yukleniyor}
        steamBulundu={steamBulundu}
        onSec={(o) => setSayfa({ ad: "oyun", oyun: o })}
        onKlasorSec={(k) => void klasorSec(k)}
        onYenile={() => void oyunlariYenile()}
      />
    );
  } else if (sayfa.ad === "canli") {
    icerik = <CanliCeviri onOyunaGit={canliOyunaGit} />;
  } else if (sayfa.ad === "hakkinda") {
    icerik = <Hakkinda surum={surum} guncelleme={guncelleme} denetleniyor={denetleniyor} onDenetle={() => void guncellemeDenetle()} onKur={guncellemeKur} onIndir={guncellemeIndir} />;
  } else {
    const o = sayfa.oyun;
    const k = o.kimlik ?? "";
    const ak = oyunAnahtari(o);
    icerik = (
      <OyunSayfasi
        oyun={o}
        durum={o.kimlik ? durumlar[ak] : undefined}
        islem={islemler[ak]}
        geri={() => setSayfa({ ad: "oyunlar" })}
        turkceYap={() => void turkceYap(o)}
        kaldir={() => void calistir(o)("Oyun orijinal hâline döndürülüyor", () => window.tv.kaldir(k, o.klasor), "Türkçe kaldırıldı; oyun orijinal hâline döndü.")}
        klasoruAc={() => void window.tv.klasoruAc(k, o.klasor)}
      />
    );
  }

  return (
    <div className="kabuk">
      <nav className="yan-menu" aria-label="Ana menü">
        <Logo />
        <div className="menu-liste">
          <MenuDugmesi simge="oyunlar" ad="Oyunlarım" secili={sayfa.ad === "oyunlar" || sayfa.ad === "oyun"} onClick={() => setSayfa({ ad: "oyunlar" })} />
          <MenuDugmesi simge="canli" ad="Canlı Çeviri" secili={sayfa.ad === "canli"} onClick={() => setSayfa({ ad: "canli" })} />
          <MenuDugmesi simge="bilgi" ad="Hakkında" secili={sayfa.ad === "hakkinda"} onClick={() => setSayfa({ ad: "hakkinda" })} />
        </div>
        <div className="menu-alt">
          {guncelleme.durum === "hazir" ? (
            <div className="guncelleme-karti">
              <strong>Yeni sürüm hazır</strong>
              <span className="soluk">{guncelleme.surum}</span>
              <Dugme tur="ana" simge="yenile" onClick={guncellemeKur}>
                Yeniden başlat
              </Dugme>
            </div>
          ) : guncelleme.durum === "var" ? (
            <div className="guncelleme-karti">
              <strong>Yeni sürüm var</strong>
              <span className="soluk">{guncelleme.surum}</span>
              <Dugme tur="ana" simge="yenile" onClick={guncellemeIndir}>
                İndir
              </Dugme>
            </div>
          ) : guncelleme.durum === "indiriliyor" ? (
            <p className="slogan">Yeni sürüm indiriliyor… %{guncelleme.yuzde}</p>
          ) : (
            <p className="slogan">Türkçesini aramayı bırak.</p>
          )}
          <div className="surum-satiri">
            {surum && <span>Sürüm {surum}</span>}
            {guncelleme.durum !== "var" && guncelleme.durum !== "indiriliyor" && guncelleme.durum !== "hazir" && (
              <Dugme tur="sade" simge="yenile" donen={denetleniyor} pasif={denetleniyor} onClick={() => void guncellemeDenetle()}>
                {denetleniyor ? "Denetleniyor…" : "Güncellemeleri denetle"}
              </Dugme>
            )}
          </div>
        </div>
      </nav>
      <main className="icerik">
        <GuncellemeUyarisi g={guncelleme} kur={guncellemeKur} indir={guncellemeIndir} />
        {/* Sayfa değişince içerik yumuşakça belirir (anahtar değişince yeniden oluşur). */}
        <div key={sayfa.ad === "oyun" ? `oyun:${sayfa.oyun.klasor}` : sayfa.ad} className="sayfa-gecis">
          {icerik}
        </div>
      </main>
      {mesaj && (
        <Tost key={mesaj.kimlik} mesaj={mesaj} onBitti={mesajBitti} />
      )}
    </div>
  );
}
