using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Canli.Mantik
{
    /// <summary>Ön plandaki pencerenin süreç yolunu izler; yol değişince "onplan" olayı yazar. Ekran okunmaz.</summary>
    public sealed class OnPlanIzleyici
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr h, out uint surecNo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint erisim, bool miras, uint surecNo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr tutamac);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr surec, uint bayrak, StringBuilder ad, ref uint boyut);

        private const uint SurecSorguSinirli = 0x1000;

        private readonly Func<string> _yolAl;
        private readonly ICikis _cikis;
        private bool _ilk = true;
        private string _son;

        public OnPlanIzleyici(Func<string> yolAl, ICikis cikis)
        {
            _yolAl = yolAl;
            _cikis = cikis;
        }

        /// <summary>Yolu alır; ilk tikte ya da son yoldan farklıysa (büyük/küçük harf yok sayılır) olayı yazar.</summary>
        public void Tik()
        {
            string yol = _yolAl();
            if (!_ilk && string.Equals(yol, _son, StringComparison.OrdinalIgnoreCase)) return;
            _ilk = false;
            _son = yol;
            _cikis.Yaz(Protokol.Olay("onplan", ("yol", yol)));
        }

        /// <summary>Ön plandaki pencerenin süreç tam yolu; alınamazsa null.</summary>
        public static string GercekYol()
        {
            try
            {
                IntPtr pencere = GetForegroundWindow();
                if (pencere == IntPtr.Zero) return null;
                GetWindowThreadProcessId(pencere, out uint surecNo);
                if (surecNo == 0) return null;
                IntPtr sur = OpenProcess(SurecSorguSinirli, false, surecNo);
                if (sur == IntPtr.Zero) return null;
                try
                {
                    var ad = new StringBuilder(1024);
                    uint boyut = (uint)ad.Capacity;
                    return QueryFullProcessImageName(sur, 0, ad, ref boyut) ? ad.ToString(0, (int)boyut) : null;
                }
                finally { CloseHandle(sur); }
            }
            catch (Exception) { return null; }
        }
    }
}
