# Canlı Çeviri: canli.exe

`canli.exe`, oyunun altyazısını ekrandan okuyup (OCR) çeviri sunucusuna gönderen ve Türkçe karşılığını oyunun üstüne çizen küçük bir Windows programıdır (C#, .NET Framework 4.8, x64). Türkçesi Var uygulaması bu programı arka planda başlatır ve onunla satır satır JSON ile konuşur.

Bu klasör herkese açık depodadır; anahtar, parola, istem ya da sözlük içermez.

## Derleme

```powershell
powershell -File canli\derle.ps1
```

`derle.ps1` programı Release olarak derler, çıktıyı `canli\cikti\` klasörüne kopyalar ve testleri çalıştırır. Gereken: .NET SDK ve .NET Framework 4.8 hedefleme paketi.

## Protokol özeti (stdin/stdout, satır başına bir JSON, UTF-8)

Girdi komutları: `basla` (lisans, oyun, oyunAdi, profil, sureciAdi), `duraklat`, `devam`, `dur`, `cik`.

Çıktı olayları: `hazir`, `basladi`, `durdu`, `kalan`, `uyari`, `hata`. Geçersiz bir satır `{"olay":"hata","kod":"gecersiz_komut"}` döndürür. stdout'a bu kanal dışında hiçbir şey yazılmaz.

Komut satırından deneme: `canli.exe --lisans K --oyun ID --oyunAdi AD [--renk sari] [--bolge x,y,g,y2] [--ek x,y,g,y2] [--surec Oyun.exe] [--tani]`
