using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Canli
{
    public static class CihazKimligi
    {
        /// <summary>sha256("turkcesivar:" + makineGuid), küçük harf onaltılık.</summary>
        public static string Ozet(string makineGuid)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes("turkcesivar:" + makineGuid));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid (64 bit kayıt defteri görünümü).</summary>
        public static string MakineGuidOku()
        {
            using (var taban = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var anahtar = taban.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                return anahtar?.GetValue("MachineGuid") as string ?? "";
            }
        }
    }
}
