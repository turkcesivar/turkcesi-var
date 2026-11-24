using System;
using System.Collections.Generic;
using System.Drawing;
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

        private Okuyucu(OcrEngine motor) { _motor = motor; }

        // en-US yoksa null + "ocr_dil_yok". dilleri: kullanılabilir dil etiketleri (sınama için enjekte edilir).
        public static Okuyucu Olustur(out string hataKodu, Func<IEnumerable<string>> dilleri = null)
        {
            hataKodu = null;
            IEnumerable<string> liste = dilleri != null
                ? dilleri()
                : OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag);
            bool var = liste.Any(t => t != null && t.StartsWith("en-US", StringComparison.OrdinalIgnoreCase));
            OcrEngine motor = null;
            if (var)
                motor = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            if (motor == null) { hataKodu = DilYok; return null; }
            return new Okuyucu(motor);
        }

        public IReadOnlyList<OcrSatiri> Oku(Bitmap b)
        {
            return OkuAsync(b).GetAwaiter().GetResult();
        }

        private async Task<IReadOnlyList<OcrSatiri>> OkuAsync(Bitmap b)
        {
            using (var ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Bmp);
                ms.Position = 0;
                var ras = System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(ms);
                var dec = await BitmapDecoder.CreateAsync(ras).AsTask().ConfigureAwait(false);
                var sb = await dec.GetSoftwareBitmapAsync().AsTask().ConfigureAwait(false);
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
