using System.Security.Cryptography;

namespace GenealogyWeb.Services
{
    /// <summary>
    /// 密码存储：PBKDF2（Rfc2898DeriveBytes / SHA-256），带随机盐与可配置迭代次数。
    /// 哈希字符串格式：<c>{迭代次数}.{saltBase64}.{hashBase64}</c>（三段以点分隔）。
    /// </summary>
    /// <remarks>课程/原型足够；生产若对接 Identity 或 Argon2，请替换并迁移存量哈希。</remarks>
    public static class PasswordHelper
    {
        /// <summary>生成新密码哈希（每次盐随机）。</summary>
        /// <param name="password">明文密码。</param>
        /// <param name="iterations">PBKDF2 迭代次数，默认 100_000。</param>
        public static string HashPassword(string password, int iterations = 100_000)
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);

            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>校验明文是否与已存哈希一致（常数时间比较）。</summary>
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
