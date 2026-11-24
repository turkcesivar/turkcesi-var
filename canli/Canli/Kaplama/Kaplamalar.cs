using System;
using System.Collections.Generic;
using System.Drawing;

namespace Canli.Kaplama
{
    /// <summary>Çalıştırıcının kaplamalarla konuştuğu tek yüz (sınamada sahtesi verilir).</summary>
    public interface IKaplama : IDisposable
    {
        /// <summary>Oyun ekranı ve altyazı bölgesi (ekran pikselleri).</summary>
        void Konumla(Rectangle ekran, Rectangle altyaziBolgesi);
        /// <summary>metin null/boş: gizle. kutu: okunan satırın altyazı bölgesine göre dikdörtgeni (boşsa bölge ortası).</summary>
        void AnaGoster(string metin, Rectangle kutu);
        /// <summary>metin null/boş: gizle.</summary>
        void OncekiGoster(string metin);
        /// <summary>Menü ve ek bölge kutuları (oyun ekranına göre); boş liste katmanı gizler.</summary>
        void MenuGoster(IReadOnlyList<MenuKutusu> kutular);
        void GizleHepsi();
    }

    /// <summary>Gerçek kaplama pencereleri: ana kutu, önceki satır kutusu ve tam ekran menü katmanı. UI iş parçacığında kurulur.</summary>
    public sealed class Kaplamalar : IKaplama
    {
        private readonly AnaKutu _ana = new AnaKutu();
        private readonly OncekiKutu _onceki = new OncekiKutu();
        private readonly MenuKatmani _menu = new MenuKatmani();
        private Rectangle _bolge;

        public Kaplamalar()
        {
            _ana.Show();
            _onceki.Show();
        }

        public void Konumla(Rectangle ekran, Rectangle altyaziBolgesi)
        {
            _bolge = altyaziBolgesi;
            _menu.Konumla(ekran);
        }

        public void AnaGoster(string metin, Rectangle kutu) { _ana.Goster(metin, kutu, _bolge); }

        public void OncekiGoster(string metin)
        {
            int ust = _ana.Gorunur ? _ana.Top : _bolge.Y + _bolge.Height / 2;
            _onceki.Goster(metin, _bolge.X + _bolge.Width / 2, ust, _bolge.Width);
        }

        public void MenuGoster(IReadOnlyList<MenuKutusu> kutular) { _menu.Ayarla(kutular); }

        public void GizleHepsi()
        {
            _ana.Goster(null, Rectangle.Empty, _bolge);
            _onceki.Gizle();
            _menu.Ayarla(null);
        }

        public void Dispose()
        {
            _ana.Dispose();
            _onceki.Dispose();
            _menu.Dispose();
        }
    }
}
