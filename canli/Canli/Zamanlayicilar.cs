using System;
using System.Windows.Forms;

namespace Canli
{
    /// <summary>Çalıştırıcının üç zamanlayıcısı (sınamada sahtesi verilir; gerçek zamanlayıcı kurulmaz).</summary>
    public interface IZamanlayicilar : IDisposable
    {
        void Baslat(Action yakalama, Action yoklama, Action ekBolge);
        void Durdur();
    }

    /// <summary>WinForms zamanlayıcıları (UI iş parçacığında çalışır): yakalama 200 ms, sonuç yoklama 30 ms, ek bölge 1 sn.</summary>
    public sealed class WinFormsZamanlayicilar : IZamanlayicilar
    {
        public const int YakalamaMs = 200, YoklamaMs = 30, EkBolgeMs = 1000;

        private readonly Timer _yakalama = new Timer { Interval = YakalamaMs };
        private readonly Timer _yoklama = new Timer { Interval = YoklamaMs };
        private readonly Timer _ekBolge = new Timer { Interval = EkBolgeMs };

        public void Baslat(Action yakalama, Action yoklama, Action ekBolge)
        {
            _yakalama.Tick += (s, e) => yakalama();
            _yoklama.Tick += (s, e) => yoklama();
            _ekBolge.Tick += (s, e) => ekBolge();
            _yakalama.Start();
            _yoklama.Start();
            _ekBolge.Start();
        }

        public void Durdur()
        {
            _yakalama.Stop();
            _yoklama.Stop();
            _ekBolge.Stop();
        }

        public void Dispose()
        {
            Durdur();
            _yakalama.Dispose();
            _yoklama.Dispose();
            _ekBolge.Dispose();
        }
    }
}
