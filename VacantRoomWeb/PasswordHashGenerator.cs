namespace VacantRoomWeb
{
    /// <summary>
    /// Utility class to generate password hashes for admin configuration
    /// Run this once to generate hash and salt, then add to appsettings.json
    /// </summary>
    public static class PasswordHashGenerator
    {
        /// <summary>
        /// Generate password hash and salt for a given password
        /// Usage: var result = PasswordHashGenerator.GenerateHash("your_admin_password");
        /// </summary>
        public static (string hash, string salt) GenerateHash(string password)
        {
            return AdminAuthService.CreatePasswordHash(password);
        }

        /// <summary>
        /// Console application to generate admin password hash
        /// Uncomment and run this method to generate your admin credentials
        /// </summary>
        /*
        public static void Main(string[] args)
        {
            Console.WriteLine("Admin Password Hash Generator");
            Console.WriteLine("============================");
            
            Console.Write("Enter admin password: ");
            var password = Console.ReadLine();
            
            if (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Password cannot be empty!");
                return;
            }
            
            var (hash, salt) = GenerateHash(password);
            
            Console.WriteLine("\nGenerated credentials:");
            Console.WriteLine($"Password Hash: {hash}");
            Console.WriteLine($"Salt: {salt}");
            
            Console.WriteLine("\nAdd these to your appsettings.json:");
            Console.WriteLine("\"AdminConfig\": {");
            Console.WriteLine("  \"Username\": \"admin\",");
            Console.WriteLine($"  \"PasswordHash\": \"{hash}\",");
            Console.WriteLine($"  \"Salt\": \"{salt}\",");
            Console.WriteLine("  \"SecretKey\": \"your_secret_key_for_auth_tokens\"");
            Console.WriteLine("}");
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        */
    }
}