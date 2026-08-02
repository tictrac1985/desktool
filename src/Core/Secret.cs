using System;
using System.Security.Cryptography;
using System.Text;

namespace DeskMate.Core
{
    /// <summary>Encrypts the settings values that can hold personal text — pinned clipboard
    /// snippets and the sticky note — with DPAPI, so they are not readable as plain text in
    /// settings.json (backups, sync folders, another admin on the machine).
    /// Keys are per Windows user; nothing is stored by the app itself.</summary>
    public static class Secret
    {
        const string Prefix = "enc:v1:";

        /// <summary>Encrypts to "enc:v1:&lt;base64&gt;". Fail-closed: if DPAPI is unavailable the
        /// value is dropped (empty string) rather than stored as readable plain text — we
        /// promised the user these values are not stored readable, so a silent plaintext
        /// fallback would be a privacy breach.</summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            try
            {
                byte[] cipher = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
                return Prefix + Convert.ToBase64String(cipher);
            }
            catch (Exception ex) { Log.Error("Secret.Protect", ex); return ""; }
        }

        /// <summary>Decrypts a value written by <see cref="Protect"/>. Values without the
        /// prefix are returned as-is: that is how settings written by earlier versions keep
        /// working, and they get encrypted on the next save.</summary>
        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
                return stored;
            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    Convert.FromBase64String(stored.Substring(Prefix.Length)), null,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                // wrong user profile or corrupted blob — drop it rather than show ciphertext
                Log.Error("Secret.Unprotect", ex);
                return "";
            }
        }
    }
}
