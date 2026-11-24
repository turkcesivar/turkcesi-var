using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Canli.Kaplama
{
    /// <summary>Menüde ya da ek bölgede okunan bir satır. Kutu: oyun ekranına göre piksel. Tr null: çeviri bekleniyor; "": çevrilmez.</summary>
    public sealed class MenuKutusu
    {
        public string En;
        public Rectangle Kutu;
        public string Tr;
    }

    /// <summary>Tam ekran saydam katman: her menü/ek bölge satırının üstüne Türkçesini çizer (canli.ps1 $mform Paint).</summary>
    public sealed class MenuKatmani : KaplamaPenceresi
    {
        private static readonly Color Saydam = Color.Magenta;
        private IReadOnlyList<MenuKutusu> _kutular = new MenuKutusu[0];

        public MenuKatmani()
        {
            BackColor = Saydam;
            TransparencyKey = Saydam;
            DoubleBuffered = true;
            Size = new Size(10, 10);
        }

        /// <summary>Katmanı oyunun ekranına oturtur (ekran pikselleri).</summary>
        public void Konumla(Rectangle ekran) { Bounds = ekran; }

        public void Ayarla(IReadOnlyList<MenuKutusu> kutular)
        {
            _kutular = kutular ?? new MenuKutusu[0];
            bool goster = _kutular.Count > 0;
            if (goster != Visible) Visible = goster;
            if (goster) Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var zemin = new SolidBrush(Color.FromArgb(18, 18, 22)))   // #121216
            {
                foreach (var m in _kutular)
                {
                    if (string.IsNullOrEmpty(m.Tr)) continue;
                    Rectangle r = m.Kutu;
                    int px = Math.Max(11, (int)(r.Height * 0.82));
                    Font f = YaziTipi(px);
                    try
                    {
                        SizeF boy = g.MeasureString(m.Tr, f);
                        if (boy.Width > r.Width * 1.6 && r.Width > 0)
                        {
                            px = Math.Max(10, (int)(px * r.Width * 1.6 / boy.Width));
                            f.Dispose();
                            f = YaziTipi(px);
                            boy = g.MeasureString(m.Tr, f);
                        }
                        float w = Math.Max(r.Width, boy.Width) + 8, h = Math.Max(r.Height, boy.Height) + 4;
                        g.FillRectangle(zemin, r.X - 4f, r.Y - 2f, w, h);
                        g.DrawString(m.Tr, f, Brushes.White, r.X, r.Y - 2 + (h - boy.Height) / 2);
                    }
                    finally { f.Dispose(); }
                }
            }
        }

        private static Font YaziTipi(int px) { return new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel); }
    }
}
