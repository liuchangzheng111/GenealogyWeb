using System.Security.Cryptography;

namespace GenealogyApp.Services
{
    public static class PasswordHelper
    {
        // Format: {iterations}.{saltBase64}.{hashBase64}
        public static string HashPassword(string password, int iterations = 100_000)
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);

            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string hashed, string password)
        {
            try
            {
                var parts = hashed.Split('.', 3);
                if (parts.Length != 3) return false;
                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expected = Convert.FromBase64String(parts[2]);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
                byte[] actual = pbkdf2.GetBytes(expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }
    }
}
