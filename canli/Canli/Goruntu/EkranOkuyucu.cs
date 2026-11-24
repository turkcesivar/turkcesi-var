using System.Collections.Generic;
using System.Drawing;

namespace Canli.Goruntu
{
    /// <summary>Ekrandan görüntü alma + OCR; çalıştırıcı bunun üstünden okur (sınamada sahtesi verilir).</summary>
    public interface IEkranOkuyucu
    {
        Bitmap Al(Rectangle ekranPikseli);
        byte[] KucukImza(Rectangle ekranPikseli);
        IReadOnlyList<OcrSatiri> Oku(Bitmap b);
    }

    public sealed class GercekEkranOkuyucu : IEkranOkuyucu
    {
        private readonly Okuyucu _okuyucu;

        public GercekEkranOkuyucu(Okuyucu okuyucu) { _okuyucu = okuyucu; }

        public Bitmap Al(Rectangle ekranPikseli) { return Yakalayici.Al(ekranPikseli); }
        public byte[] KucukImza(Rectangle ekranPikseli) { return Yakalayici.KucukImza(ekranPikseli); }
        public IReadOnlyList<OcrSatiri> Oku(Bitmap b) { return _okuyucu.Oku(b); }
    }
}
