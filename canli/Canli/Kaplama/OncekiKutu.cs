using System;
using System.Drawing;
using System.Windows.Forms;

namespace Canli.Kaplama
{
    /// <summary>Önceki satırın soluk kutusu: ana kutunun hemen üstünde (canli.ps1 GecmisGoster).</summary>
    public sealed class OncekiKutu : KaplamaPenceresi
    {
        public const double Opaklik = 0.80;
        private readonly Label _etiket;

        public OncekiKutu()
        {
            BackColor = Color.FromArgb(12, 12, 14);
            Opacity = 0.0;
            Size = new Size(10, 10);
            _etiket = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(170, 170, 160),   // #AAAAA0
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Padding = new Padding(12, 4, 12, 4),
                Location = new Point(0, 0)
            };
            Controls.Add(_etiket);
        }

        public void Gizle() { Opacity = 0.0; }

        /// <summary>Yatayda merkezX'e ortalanır, alt kenarı ust'ün 6 piksel üstünde durur.</summary>
        public void Goster(string metin, int merkezX, int ust, int bolgeGenisligi)
        {
            if (string.IsNullOrEmpty(metin)) { Gizle(); return; }
            _etiket.MaximumSize = new Size(Math.Max(50, (int)(bolgeGenisligi * 0.95)), 0);
            _etiket.Text = metin;
            Size = _etiket.PreferredSize;
            Left = merkezX - Width / 2;
            Top = ust - Height - 6;
            if (!Visible) Show();
            Opacity = Opaklik;
        }
    }
}
