using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Canli.Goruntu
{
    public sealed class OcrSatiri
    {
        public string Metin;
        public Rectangle Kutu;
    }

    public sealed class Okuyucu
    {
        public const string DilYok = "ocr_dil_yok";
        private readonly OcrEngine _motor;
        private readonly object _kilit = new object();

        private Okuyucu(OcrEngine motor) { _motor = motor; }

        // en-US yoksa null + "ocr_dil_yok". dilleri: kullanılabilir dil etiketleri (sınama için enjekte edilir).
        public static Okuyucu Olustur(out string hataKodu, Func<IEnumerable<string>> dilleri = null)
        {
            hataKodu = null;
            IEnumerable<string> liste = dilleri != null
                ? dilleri()
                : OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag);
            bool enVar = liste.Any(t => t != null && t.StartsWith("en-US", StringComparison.OrdinalIgnoreCase));
            OcrEngine motor = null;
            if (enVar)
                motor = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            if (motor == null) { hataKodu = DilYok; return null; }
            return new Okuyucu(motor);
        }

        // Tek OCR motoru var; zamanlayıcılardan eşzamanlı çağrılabileceği için kilitlenir.
        // Görüntü MaxImageDimension'ı aşarsa orantılı küçültülür, kutular geri büyütülür.
        public IReadOnlyList<OcrSatiri> Oku(Bitmap b)
        {
            lock (_kilit)
            {
                int enBuyuk = (int)OcrEngine.MaxImageDimension;
                double olcek = 1.0;
                Bitmap kucuk = null;
                try
                {
                    Bitmap girdi = b;
                    if (b.Width > enBuyuk || b.Height > enBuyuk)
                    {
                        olcek = (double)enBuyuk / Math.Max(b.Width, b.Height);
                        int w = Math.Max(1, (int)(b.Width * olcek)), h = Math.Max(1, (int)(b.Height * olcek));
                        kucuk = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(kucuk))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(b, 0, 0, w, h);
                        }
                        girdi = kucuk;
                    }
                    var sonuc = OkuAsync(girdi).GetAwaiter().GetResult();
                    if (olcek != 1.0)
                        foreach (var s in sonuc)
                            s.Kutu = new Rectangle((int)(s.Kutu.X / olcek), (int)(s.Kutu.Y / olcek),
                                                   (int)(s.Kutu.Width / olcek), (int)(s.Kutu.Height / olcek));
                    return sonuc;
                }
                finally { if (kucuk != null) kucuk.Dispose(); }
            }
        }

        private async Task<List<OcrSatiri>> OkuAsync(Bitmap b)
        {
            using (var ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Bmp);
                ms.Position = 0;
                using (var ras = System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(ms))
                {
                    var dec = await BitmapDecoder.CreateAsync(ras).AsTask().ConfigureAwait(false);
                    using (var sb = await dec.GetSoftwareBitmapAsync().AsTask().ConfigureAwait(false))
                    {
                        var sonuc = await _motor.RecognizeAsync(sb).AsTask().ConfigureAwait(false);
                        var liste = new List<OcrSatiri>();
                        foreach (var l in sonuc.Lines)
                        {
                            string metin = Regex.Replace(l.Text ?? "", @"\s+", " ").Trim();
                            if (metin.Length == 0) continue;
                            double x1 = double.MaxValue, y1 = double.MaxValue, x2 = 0, y2 = 0;
                            foreach (var w in l.Words)
                            {
                                var r = w.BoundingRect;
                                x1 = Math.Min(x1, r.X); y1 = Math.Min(y1, r.Y);
                                x2 = Math.Max(x2, r.X + r.Width); y2 = Math.Max(y2, r.Y + r.Height);
                            }
                            var kutu = x2 > 0 ? new Rectangle((int)x1, (int)y1, (int)(x2 - x1), (int)(y2 - y1)) : Rectangle.Empty;
                            liste.Add(new OcrSatiri { Metin = metin, Kutu = kutu });
                        }
                        return liste;
                    }
                }
            }
        }
    }
}
