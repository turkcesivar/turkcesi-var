using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Canli.Kaplama
{
    /// <summary>Kaplama pencerelerinin ortak Win32 ayarları: yakalamada görünmez, tıklama geçer, odak almaz.</summary>
    public static class PencereAyarlari
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        public const int KaplamaStili = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr h, uint ayar);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr h, int i);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr h, int i, int deger);

        /// <summary>canli.ps1 TiklamaGecsin: ekran yakalamada görünmez + katmanlı, tıklama geçer, araç penceresi, odaksız.</summary>
        public static void TiklamaGecsin(IntPtr h)
        {
            SetWindowDisplayAffinity(h, WDA_EXCLUDEFROMCAPTURE);
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | KaplamaStili);
        }
    }

    /// <summary>Tüm kaplama pencerelerinin tabanı: çerçevesiz, en üstte, görev çubuğunda yok, gösterilince odak çalmaz.</summary>
    public abstract class KaplamaPenceresi : Form
    {
        protected KaplamaPenceresi()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        // WinForms stilleri yeniden yazdığında (Opacity/TransparencyKey değişimi) da bayraklar kalsın.
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= PencereAyarlari.WS_EX_TRANSPARENT | PencereAyarlari.WS_EX_TOOLWINDOW | PencereAyarlari.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            PencereAyarlari.TiklamaGecsin(Handle);
        }
    }
}
