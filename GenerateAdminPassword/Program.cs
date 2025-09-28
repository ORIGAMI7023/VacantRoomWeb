using System;
using System.Security.Cryptography;
using System.Text;

namespace VacantRoomWeb
{
    public class TempPasswordGenerator
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Generating hash for password: K9mP#3xR8qW$7nZ5");

            var password = "null";  // 在此输入你的新密码
            var (hash, salt) = CreatePasswordHash(password);
            var secretKey = GenerateSecretKey();

            // ===== 1. 输出 IIS web.config 环境变量格式 =====
            Console.WriteLine("\n=== IIS web.config 环境变量格式 ===");
            Console.WriteLine($"\t<environmentVariable name=\"VACANTROOM_ADMIN_USERNAME\" value=\"admin_origami\" />");
            Console.WriteLine($"\t<environmentVariable name=\"VACANTROOM_ADMIN_PASSWORDHASH\" value=\"{hash}\" />");
            Console.WriteLine($"\t<environmentVariable name=\"VACANTROOM_ADMIN_SALT\" value=\"{salt}\" />");
            Console.WriteLine($"\t<environmentVariable name=\"VACANTROOM_ADMIN_SECRETKEY\" value=\"{secretKey}\" />");
            Console.WriteLine("==================================");

            // ===== 2. 输出 dotnet user-secrets 命令 =====
            Console.WriteLine("\n=== dotnet user-secrets 命令 ===");
            Console.WriteLine($"dotnet user-secrets set \"VACANTROOM_ADMIN_USERNAME\" \"admin_origami\"");
            Console.WriteLine($"dotnet user-secrets set \"VACANTROOM_ADMIN_PASSWORDHASH\" \"{hash}\"");
            Console.WriteLine($"dotnet user-secrets set \"VACANTROOM_ADMIN_SALT\" \"{salt}\"");
            Console.WriteLine($"dotnet user-secrets set \"VACANTROOM_ADMIN_SECRETKEY\" \"{secretKey}\"");
            Console.WriteLine($"dotnet user-secrets set \"PASSWORD\" \"{password}\"");
            Console.WriteLine("==================================");

        }

        public static (string hash, string salt) CreatePasswordHash(string password)
        {
            var salt = GenerateSalt();
            var hash = ComputePasswordHash(password, salt);
            return (hash, salt);
        }

        public static string ComputePasswordHash(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var saltedPassword = salt + password;
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hash);
        }

        private static string GenerateSalt()
        {
            var saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        private static string GenerateSecretKey()
        {
            var keyBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(keyBytes);
            return Convert.ToBase64String(keyBytes);
        }
    }
}
