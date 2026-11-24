namespace Canli.Mantik
{
    /// <summary>Ardışık siyah karelerin sayacı: sayı ilk kez 25'e ulaşınca bir kez true döner.</summary>
    public sealed class SiyahKareSayaci
    {
        public const int Esik = 25;
        private int _say;

        public bool Ekle(bool siyah)
        {
            if (!siyah) { _say = 0; return false; }
            _say++;
            return _say == Esik;
        }
    }
}
