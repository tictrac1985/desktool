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

        /// <summary>Raised when a value could not be encrypted and was therefore dropped.
        /// The UI subscribes so the loss is visible instead of silent.</summary>
        public static event Action<string> ProtectFailed;

        /// <summary>True for ciphertext that this Windows user cannot decrypt — a settings
        /// file carried over from another profile, restored from a backup, or written before
        /// a DPAPI master-key reset. Such a value is kept byte-for-byte rather than erased,
        /// so it stays recoverable from the original profile. Callers must not display it.</summary>
        public static bool IsSealed(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>Encrypts to "enc:v1:&lt;base64&gt;".
        /// A value that is already sealed ciphertext (see <see cref="IsSealed"/>) is passed
        /// through untouched — re-encrypting it is impossible and dropping it would destroy
        /// data the owning profile can still read.
        /// If DPAPI itself fails the value is dropped rather than stored as readable plain
        /// text — we promised the user these values are not stored readable — but the drop is
        /// announced through <see cref="ProtectFailed"/> instead of happening in silence.</summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            if (IsSealed(plain)) return plain;
            try
            {
                byte[] cipher = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
                return Prefix + Convert.ToBase64String(cipher);
            }
            catch (Exception ex)
            {
                Log.Error("Secret.Protect", ex);
                var handler = ProtectFailed;
                if (handler != null) { try { handler("Secret.Protect"); } catch { } }
                return "";
            }
        }

        /// <summary>Decrypts a value written by <see cref="Protect"/>. Values without the
        /// prefix are returned as-is: that is how settings written by earlier versions keep
        /// working, and they get encrypted on the next save.
        /// On failure the ciphertext is returned unchanged, NOT an empty string: returning ""
        /// used to be written straight back by the next Save, permanently destroying the
        /// user's note and pinned clips the moment a settings file met a different profile.
        /// Callers test <see cref="IsSealed"/> and show a locked state instead of the text.</summary>
        public static string Unprotect(string stored)
        {
            if (!IsSealed(stored)) return stored;
            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    Convert.FromBase64String(stored.Substring(Prefix.Length)), null,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                // wrong user profile, restored backup, or reset master key — keep the bytes
                Log.Error("Secret.Unprotect", ex);
                return stored;
            }
        }
    }
}
