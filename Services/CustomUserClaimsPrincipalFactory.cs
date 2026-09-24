using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MovieWeb.Models.Entities;
using System.Security.Claims;

namespace MovieWeb.Services
{
    public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User, Role>
    {
        public CustomUserClaimsPrincipalFactory(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // ⭐ THÊM CUSTOM CLAIMS
            identity.AddClaim(new Claim("RoleId", user.RoleId.ToString()));
            
            var subscriptionType = !string.IsNullOrEmpty(user.SubscriptionType) 
                ? user.SubscriptionType.ToLower() 
                : "free";
            identity.AddClaim(new Claim("SubscriptionType", subscriptionType));

            if (!string.IsNullOrEmpty(user.Avatar))
            {
                identity.AddClaim(new Claim("Avatar", user.Avatar));
            }

            var fullName = user.FullName;
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                identity.AddClaim(new Claim("FullName", fullName));
            }

            return identity;
        }
    }
}