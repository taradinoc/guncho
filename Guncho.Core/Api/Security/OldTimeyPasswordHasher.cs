using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api.Security
{
    // see default implementation at http://stackoverflow.com/questions/20621950/asp-net-identity-default-password-hasher-how-does-it-work-and-is-it-secure
    public class OldTimeyPasswordHasher : IPasswordHasher
    {
        #region Static methods for password hashing

        private const int SALT_LENGTH = 3;

        public static string GenerateSalt()
        {
            const string possibleSalt =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                "`1234567890-=~!@#$%^*()_+[]\\{}|;:,./?";

            string newSalt = "";
            Random rng = new Random();
            for (int i = 0; i < 3; i++)
                newSalt += possibleSalt[rng.Next(possibleSalt.Length)];
            return newSalt;
        }

        public static string HashPassword(string salt, string password)
        {
            List<byte> bytes = new List<byte>();
            if (salt != null)
                bytes.AddRange(Encoding.UTF8.GetBytes(salt));
            if (password != null)
                bytes.AddRange(Encoding.UTF8.GetBytes(password));

            SHA1 sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(bytes.ToArray());
            return Convert.ToBase64String(hash);
        }

        #endregion

        public string HashPassword(string password)
        {
            var salt = GenerateSalt();
            var hashed = HashPassword(salt, password);
            return salt + " " + hashed;
        }

        public PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string providedPassword)
        {
            var parts = hashedPassword.Split(new[] { ' ' }, 2);
            var salt = parts[0];
            var expectedHashed = parts[1];

            var actualHashed = HashPassword(salt, providedPassword);

            // TODO: use SuccessRehashNeeded to update to new-timey hashes
            return actualHashed == expectedHashed ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
        }
    }
}
