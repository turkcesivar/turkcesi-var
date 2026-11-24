using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Canli.Mantik
{
    public interface IEkranKaynagi
    {
        IReadOnlyList<Rectangle> Ekranlar();
        Rectangle Birincil();
        Rectangle PencereDikdortgeni(IntPtr h);
    }

    /// <summary>Gerçek ekranlar ve pencere konumu (Screen + GetWindowRect).</summary>
    public sealed class GercekEkranKaynagi : IEkranKaynagi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Sol, Ust, Sag, Alt; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr h, out RECT r);

        public IReadOnlyList<Rectangle> Ekranlar()
        {
            var l = new List<Rectangle>();
            foreach (var s in Screen.AllScreens) l.Add(s.Bounds);
            return l;
        }

        public Rectangle Birincil() { return Screen.PrimaryScreen.Bounds; }

        public Rectangle PencereDikdortgeni(IntPtr h)
        {
            RECT r;
            if (!GetWindowRect(h, out r)) return Rectangle.Empty;
            return Rectangle.FromLTRB(r.Sol, r.Ust, r.Sag, r.Alt);
        }
    }

    public static class EkranSecici
    {
        public static Rectangle OyunEkrani(IntPtr oyunPenceresi)
        {
            return OyunEkrani(oyunPenceresi, new GercekEkranKaynagi());
        }

        /// <summary>Pencere merkezini içeren ekran; yoksa en çok kesişen; yoksa birincil.</summary>
        public static Rectangle OyunEkrani(IntPtr oyunPenceresi, IEkranKaynagi kaynak)
        {
            if (oyunPenceresi == IntPtr.Zero) return kaynak.Birincil();
            var p = kaynak.PencereDikdortgeni(oyunPenceresi);
            if (p.Width <= 0 || p.Height <= 0) return kaynak.Birincil();

            var merkez = new Point(p.X + p.Width / 2, p.Y + p.Height / 2);
            var ekranlar = kaynak.Ekranlar();
            foreach (var e in ekranlar)
                if (e.Contains(merkez)) return e;

            Rectangle? en = null; long enAlan = 0;
            foreach (var e in ekranlar)
            {
                var k = Rectangle.Intersect(e, p);
                long alan = k.IsEmpty ? 0 : (long)k.Width * k.Height;
                if (alan > enAlan) { enAlan = alan; en = e; }
            }
            return en ?? kaynak.Birincil();
        }

        /// <summary>[x, y, genişlik, yükseklik] oranlarını ekranın piksellerine çevirir (ekran başlangıcı eklenir).
        /// Yuvarlama PowerShell [int] gibidir (en yakın tam sayı, yarımda çifte): kaynak betikteki değerlerle aynı.</summary>
        public static Rectangle OranlariPiksele(Rectangle ekran, double[] oran)
        {
            int x = (int)Math.Round(ekran.Width * oran[0]);
            int y = (int)Math.Round(ekran.Height * oran[1]);
            int g = (int)Math.Round(ekran.Width * oran[2]);
            int h = (int)Math.Round(ekran.Height * oran[3]);
            return new Rectangle(ekran.X + x, ekran.Y + y, g, h);
        }
    }
}
