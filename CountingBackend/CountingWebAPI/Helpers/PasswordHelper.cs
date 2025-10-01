using System.Security.Cryptography;
using System.Text;
using System; // For Buffer, Convert, ArgumentException

namespace CountingWebAPI.Helpers
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16; 
        private const int HashSize = 32; 
        private const int Iterations = 100000;

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(HashSize);
            var hashBytes = new byte[SaltSize + HashSize];
            Buffer.BlockCopy(salt, 0, hashBytes, 0, SaltSize);
            Buffer.BlockCopy(hash, 0, hashBytes, SaltSize, HashSize);
            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (string.IsNullOrEmpty(hashedPassword)) return false;
            try {
                var hashBytes = Convert.FromBase64String(hashedPassword);
                if (hashBytes.Length != SaltSize + HashSize) return false;
                var salt = new byte[SaltSize];
                Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);
                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
                byte[] computedHash = pbkdf2.GetBytes(HashSize);
                for (var i = 0; i < HashSize; i++) {
                    if (hashBytes[i + SaltSize] != computedHash[i]) return false;
                }
                return true;
            } catch { return false; }
        }
    }
}