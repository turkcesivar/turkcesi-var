using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Canli.Mantik
{
    /// <summary>Ön plandaki pencere oyunun mu? Değilse hiçbir şey yakalanmaz ve okunmaz.</summary>
    public sealed class OnPlanBekcisi
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr h, out uint surecNo);

        private readonly string _surec;
        private readonly Func<(string ad, IntPtr pencere)> _onPlan;

        public OnPlanBekcisi(string surecAdi) : this(surecAdi, GercekOnPlan) { }

        /// <summary>onPlan: ön plandaki pencerenin süreç adı ve tutamacı (sınamada sahtesi verilir).</summary>
        public OnPlanBekcisi(string surecAdi, Func<(string ad, IntPtr pencere)> onPlan)
        {
            _surec = Yalin(surecAdi);
            _onPlan = onPlan;
        }

        /// <summary>Süreç adı boşsa (Plan B sınaması) her zaman true ve pencere = IntPtr.Zero (birincil ekran).</summary>
        public bool OyunOnde(out IntPtr pencere)
        {
            pencere = IntPtr.Zero;
            if (_surec.Length == 0) return true;
            var (ad, h) = _onPlan();
            if (ad == null || !string.Equals(Yalin(ad), _surec, StringComparison.OrdinalIgnoreCase)) return false;
            pencere = h;
            return true;
        }

        // "C:\Oyun\Mafia.exe" / "Mafia.exe" / "Mafia" → "Mafia"
        private static string Yalin(string ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) return "";
            ad = ad.Trim();
            try { ad = Path.GetFileName(ad); } catch (ArgumentException) { }
            if (ad.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ad = ad.Substring(0, ad.Length - 4);
            return ad;
        }

        private static uint _sonSurecNo;
        private static string _sonSurecAdi;

        private static (string, IntPtr) GercekOnPlan()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return (null, IntPtr.Zero);
            GetWindowThreadProcessId(h, out uint no);
            if (no == 0) return (null, h);
            if (no == _sonSurecNo && _sonSurecAdi != null) return (_sonSurecAdi, h);
            try
            {
                using (var p = Process.GetProcessById((int)no))
                {
                    _sonSurecNo = no;
                    _sonSurecAdi = p.ProcessName;
                    return (_sonSurecAdi, h);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is System.ComponentModel.Win32Exception)
            {
                return (null, h);
            }
        }
    }
}
