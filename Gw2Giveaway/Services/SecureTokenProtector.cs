using System;
using System.Security.Cryptography;
using System.Text;

namespace Gw2Giveaway.Services
{
    public static class SecureTokenProtector
    {
        public static string Protect(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string Unprotect(string encryptedValue)
        {
            if (string.IsNullOrWhiteSpace(encryptedValue))
                return string.Empty;

            byte[] protectedBytes = Convert.FromBase64String(encryptedValue);
            byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
