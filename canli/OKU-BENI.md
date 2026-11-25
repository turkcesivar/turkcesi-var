# Canlı Çeviri: canli.exe

`canli.exe`, oyunun altyazısını ekrandan okuyup (OCR) çeviri sunucusuna gönderen ve Türkçe karşılığını oyunun üstüne çizen küçük bir Windows programıdır (C#, .NET Framework 4.8, x64). Türkçesi Var uygulaması bu programı arka planda başlatır ve onunla satır satır JSON ile konuşur.

Bu klasör herkese açık depodadır; anahtar, parola, istem ya da sözlük içermez.

## Derleme

```powershell
powershell -File canli\derle.ps1
```

`derle.ps1` programı Release olarak derler, çıktıyı `canli\cikti\` klasörüne kopyalar ve testleri çalıştırır. Gereken: .NET SDK ve .NET Framework 4.8 hedefleme paketi.

## Protokol özeti (stdin/stdout, satır başına bir JSON, UTF-8)

Girdi komutları: `basla` (lisans, oyun, oyunAdi, profil, sureciAdi), `duraklat`, `devam`, `dur`, `cik`, `izle`.

Çıktı olayları: `hazir`, `basladi`, `durdu`, `kalan`, `uyari`, `hata`, `onplan`. Geçersiz bir satır `{"olay":"hata","kod":"gecersiz_komut"}` döndürür. stdout'a bu kanal dışında hiçbir şey yazılmaz.

`izle` (bir kez gönderilir, tekrarı etkisizdir): ön plandaki pencerenin süreç yolunu saniyede bir yoklar ve her DEĞİŞTİĞİNDE `{"olay":"onplan","yol":"C:\...\Oyun.exe"}` yazar (yol alınamazsa `"yol":null`). Yalnız süreç yolu okunur, ekran okunmaz. `basla` oturumundan bağımsızdır: `dur` izlemeyi durdurmaz, `cik` kapatır.

`hata` kodları: `ocr_dil_yok`, `gecersiz_komut`, `lisans_yok`, `cihaz_dustu`, `suresi_bitti`, `paketli`, `yasakli`
(bu beşinde `durdu` nedeni `lisans`), ayrıca `yapilandirma` ve `cihaz_yok` (sunucu yapılandırma/istek hatası) ve
`beklenmeyen` (programın kendi hatası); bu üçünde `durdu` nedeni `hata`. Uygulama bilmediği bir kodu genel hata gibi göstermelidir.

Uygulamadan (stdin) gelen `basla` komutunda `sureciAdi` zorunludur (yoksa `gecersiz_komut`): canli.exe yalnız o süreç
öndeyken ekranı okur. Süreç adı boş bırakılabilen tek yol komut satırı sınamasıdır.

Profil bölgeleri ekranın içinde ve boyutu sıfırdan büyük olmalıdır; geçersiz bölge varsayılana döner (ek bölge atlanır).

Komut satırından deneme: `canli.exe --lisans K --oyun ID --oyunAdi AD [--renk sari] [--bolge x,y,g,y2] [--ek x,y,g,y2] [--surec Oyun.exe] [--tani]`

## Altyazı rengi

Profilde renk verilmemişse (`ikisi`) altyazı rengi oturumda öğrenilir: 2 sn'de bir altyazı bölgesi süzgeçsiz okunur,
yazı kutularında zemin (kutu kenarı) renginden uzak, ince çizgilerde yoğunlaşan, baskın ve koyu olmayan renk yazı
rengi sayılır (gölge/çerçeve elenir). Aynı renk 3 kez gelince öğrenilir; en çok 5 renk (ör. beyaz ipucu + konuşmacıya göre değişen renkler). Öğrenilen renkler oyun başına `%LOCALAPPDATA%\TurkcesiVar\canli\bellek\renk-*.txt` dosyasına yazılır ve
sonraki açılışta hemen kullanılır. Yalnız ince çizgi üstündeki pikseller tutulur: yatay ve dikey eşleşme
dizilerinden kısası yazı kalınlığını aşan piksel (kar, gökyüzü gibi geniş leke) atılır; böylece aynı karede açık gri
konuşmacı adı tutulur, aynı renkteki kar atılır. Bozuk okumalar (� işareti, tuhaf simgeler, harf-rakam karışık
kelimeler, düşük harf oranı) sunucuya gönderilmez (tanı olayı `bozuk_okuma`). Öğrenene kadar sabit süzgeç (beyaz, sarı, turuncu) çalışır. Profil `beyaz`,
`sari` ya da `turuncu` derse yalnız o renk kullanılır. Tanı olayı: `renk` (`kaynak` = disk/algilandi, `renkler`).

## Önceden indirilen bellek

`basladi`dan sonra oyunun ortak belleği arka planda iner (`GET /bellek/<oyun>`, lisanslı): önce disk önbelleği
`%LOCALAPPDATA%\TurkcesiVar\canli\bellek\` (anında), sonra sunucu (yalnız sürüm değiştiyse gövde gelir). Altyazı ve
menü satırları sunucuya gitmeden önce bu bellekte aranır (sadeleştirilmiş tam eşleşme, yoksa üçlü harf dizininden
seçilen ≤ 40 aday arasında ≥ 0,85 benzerlik; "AD: metin" önekli satırlar adsız da aranır). İnmezse çeviri sunucudan sürer.

## Altyazı gösterimi

Her gelen çeviri ana kutuda gösterilir; yeni İngilizce satır gelince önceki Türkçe, yenisi gelene kadar ekranda kalır
(hiçbir Türkçe yokken "…" yer tutucu). Ekrandakinden daha eski bir satırın geç gelen çevirisi yok sayılır. Soluk
"önceki satır" kutusu kullanılmaz (hızlı diyalogda karışıklık yaratıyordu; 25 Kasım kullanıcı geri bildirimi).
Aynı satırın gürültülü okumaları (harf bazlı ≥ 0,80 benzerlik, kesik/yarım okuma, son 4 sn'lik okumalar) yeniden
çevrilmez; yarım okunan satırın tamamı bir kez yeniden çevrilir. OCR satırları yukarıdan aşağı, soldan sağa birleştirilir.

## Tanı kaydı

`--tani` (ya da `basla` komutunda `"tani": true`) verilirse her olay
`%LOCALAPPDATA%\TurkcesiVar\canli\tani\<yyyyMMdd-HHmmss>.jsonl` dosyasına bir JSON satırı olarak yazılır
(`t` = oturum başından ms). Olaylar: `basladi`, `altyazi` (`en`, `ocrMs`, `istekMs`, `toplamMs`,
`kaynak` = onbellek/yerel/bellek/model/okunamaz, `hata`), `on_bellek` (`satir`, `kaynak` = disk/sunucu/degismedi/hata, `ms`), `menu_okundu`, `menu_atlandi`, `menu_ceviri`, `ek_okundu`,
`sunucu_hatasi`, `arkada`, `duraklat`, `devam`, `menu_kapandi`, `durdu`. Kayıt stdout protokolüne karışmaz.

## Ölçümler

**Mafia: The Old Country, 24 Kasım 2026** (pencereli tam ekran, sarı altyazı, sol üst ek bölge; 7,8 dk,
deneme lisansıyla, sunucu üzerinden). Karşılaştırma: fizibilite denemesi (doğrudan API, sunucusuz).

| Ölçüm | canli.exe (sunucu) | Fizibilite |
|---|---|---|
| Altyazı: İngilizce okunmasından Türkçenin gelmesine (medyan) | 1,66 sn (en az 1,47, en çok 1,86) | 1,37 sn |
| Altyazı: ekran okuma (OCR, medyan) | 30 ms | — |
| Altyazı: istek süresi (medyan) | 1,63 sn | 1,26 sn |
| Altyazı satırı: çevrilen / hata | 20 / 0 (2 "okunamaz", 1 yerel bellek) | 44 / 0 |
| Menü ekranı: tam ekran OCR (medyan) | 227 ms | 185 ms |
| Menü: yeni satırların gelmesi (medyan) | 2,0 sn; bilinen satırlar OCR ile birlikte (~0,2 sn) | 1,35 sn |
| Menü satırı: okunan / bellekten | 306 / 162 | — |
| Ek bölge (görev yazısı) çevirisi (medyan) | 1,15 sn | — |
| Sunucu hatası | 1 (`hiz`, art arda açılan menü ekranlarında; sonraki istekler düzeldi) | — |

Kullanıcı yorumu: "gayet iyi çalışıyor". Bulunan tek görsel sorun: notlardaki uzun Türkçe cümleler alt
satıra geçmeyip ekran dışına taşıyordu → düzeltildi (menü katmanı ekran kenarında satır kaydırır).

Sonuç: OCR ve çizim hızlı; gecikmenin tamamına yakını sunucu isteği. Altyazı medyanı hedefin (≤1,5 sn)
biraz üstünde; iyileştirme sunucu tarafında yapılacak (istek başına ek yükün azaltılması, menü isteklerinin
hız sınırına takılmaması).
