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

            var password = "K9mP#3xR8qW$7nZ5";
            var (hash, salt) = CreatePasswordHash(password);
            var secretKey = GenerateSecretKey();

            Console.WriteLine("\n=== COPY THIS TO appsettings.json ===");
            Console.WriteLine("\"AdminConfig\": {");
            Console.WriteLine("  \"Username\": \"admin_origami\",");
            Console.WriteLine($"  \"PasswordHash\": \"{hash}\",");
            Console.WriteLine($"  \"Salt\": \"{salt}\",");
            Console.WriteLine($"  \"SecretKey\": \"{secretKey}\"");
            Console.WriteLine("}");
            Console.WriteLine("=====================================");
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