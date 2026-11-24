using System;
using System.Drawing;
using System.Windows.Forms;

namespace Canli.Kaplama
{
    /// <summary>Türkçe altyazı kutusu: İngilizce satırın tam üstüne, onu örtecek boyda (canli.ps1 Goster).</summary>
    public sealed class AnaKutu : KaplamaPenceresi
    {
        public const double Opaklik = 0.93;
        private readonly Label _etiket;

        public AnaKutu()
        {
            BackColor = Color.FromArgb(12, 12, 14);   // #0C0C0E
            Opacity = 0.0;
            Size = new Size(10, 10);
            _etiket = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 236, 120),   // #FFEC78
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Padding = new Padding(14, 6, 14, 6),
                Location = new Point(0, 0)
            };
            Controls.Add(_etiket);
        }

        public bool Gorunur { get { return Opacity > 0; } }

        /// <summary>metin boşsa gizlenir. kutu: okunan satırın bölgeye göre dikdörtgeni (boşsa bölgenin ortası).
        /// bolge: altyazı bölgesinin ekran pikselleri.</summary>
        public void Goster(string metin, Rectangle kutu, Rectangle bolge)
        {
            if (string.IsNullOrEmpty(metin)) { Opacity = 0.0; _etiket.Text = ""; return; }
            bool kutuVar = kutu.Width > 0 && kutu.Height > 0;
            _etiket.AutoSize = true;
            _etiket.MaximumSize = new Size(Math.Max(50, (int)(bolge.Width * 0.95)), 0);
            _etiket.Text = metin;
            Size tercih = _etiket.PreferredSize;
            int w = Math.Max(tercih.Width, kutuVar ? kutu.Width + 40 : 0);
            int h = Math.Max(tercih.Height, kutuVar ? kutu.Height + 16 : 0);
            _etiket.AutoSize = false;
            _etiket.Size = new Size(w, h);
            Size = _etiket.Size;

            double cx, cy;
            if (kutuVar) { cx = bolge.X + kutu.X + kutu.Width / 2.0; cy = bolge.Y + kutu.Y + kutu.Height / 2.0; }
            else { cx = bolge.X + bolge.Width / 2.0; cy = bolge.Y + bolge.Height / 2.0; }
            Left = (int)(cx - w / 2.0);
            Top = (int)(cy - h / 2.0);
            if (!Visible) Show();
            Opacity = Opaklik;
        }
    }
}
