using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api.Security
{
    // see default implementation at http://stackoverflow.com/questions/20621950/asp-net-identity-default-password-hasher-how-does-it-work-and-is-it-secure
    public class OldTimeyPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            var salt = Controller.GenerateSalt();
            var hashed = Controller.HashPassword(salt, password);
            return salt + " " + hashed;
        }

        public PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string providedPassword)
        {
            var parts = hashedPassword.Split(new[] { ' ' }, 2);
            var salt = parts[0];
            var expectedHashed = parts[1];

            var actualHashed = Controller.HashPassword(salt, providedPassword);

            // TODO: use SuccessRehashNeeded to update to new-timey hashes
            return actualHashed == expectedHashed ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
        }
    }
}
