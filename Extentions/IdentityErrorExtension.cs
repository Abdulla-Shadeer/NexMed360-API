using Microsoft.AspNetCore.Identity;

namespace NexMed360Api.Extensions
{
    public static class IdentityErrorExtension
    {
        public static string IdentityError(IdentityResult result)
        {
            var error = "";
            if (result.Errors.First().Code == "DuplicateUserName")
            {
                error = result.Errors.First(e => e.Code == "DuplicateEmail").Description;
            }
            else
            {
                error = result.Errors.First().Description;
            }

            return error;
        }
    }
}
