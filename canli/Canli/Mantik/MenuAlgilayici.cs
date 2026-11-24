using System;

namespace Canli.Mantik
{
    /// <summary>
    /// Ekranın küçük imzasındaki değişim oranından menü kararı verir: ekran 450 ms sabit kalırsa oku,
    /// menü açıkken büyük değişimde kutuları kaldır. Zaman enjekte edilir (milisaniye).
    /// </summary>
    public sealed class MenuAlgilayici
    {
        public enum Karar { Yok, Oku, Kaldir }

        public const double DegisimEsigi = 0.004;
        public const double KaldirmaEsigi = 0.025;
        public const double SabitMs = 450;
        public const int EnAzSatir = 5;

        private readonly Func<double> _simdi;
        private double _sabitBas;
        private bool _islendi;

        /// <summary>Menü kutuları ekranda mı (çalıştırıcı açar; Kaldir kararı kapatır).</summary>
        public bool MenuAcik { get; set; }

        public MenuAlgilayici(Func<double> simdiMs)
        {
            _simdi = simdiMs;
            _sabitBas = simdiMs();
        }

        /// <summary>Bir önceki kareye göre değişim oranı (Yakalayici.Degisim) verilir.</summary>
        public Karar Imza(double oran)
        {
            double simdi = _simdi();
            if (oran > DegisimEsigi)
            {
                _sabitBas = simdi;
                _islendi = false;
                if (oran > KaldirmaEsigi && MenuAcik)
                {
                    MenuAcik = false;
                    return Karar.Kaldir;
                }
                return Karar.Yok;
            }
            if (!_islendi && simdi - _sabitBas > SabitMs)
            {
                _islendi = true;
                return Karar.Oku;
            }
            return Karar.Yok;
        }

        /// <summary>Otomatik okumada menü sayılmak için gereken satır sayısı (F7 ile elle okumada aranmaz).</summary>
        public bool YeterliSatir(int n) { return n >= EnAzSatir; }
    }
}
