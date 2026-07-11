using Microsoft.AspNetCore.Identity;
using MovieWeb.Models.Entities;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MovieWeb.Middlewares
{
    public class UserStatusMiddleware
    {
        private readonly RequestDelegate _next;

        public UserStatusMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, UserManager<User> userManager, SignInManager<User> signInManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = userManager.GetUserId(context.User);
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await userManager.FindByIdAsync(userId);
                    if (user == null || user.IsActive == false)
                    {
                        // Force logout instantly
                        await signInManager.SignOutAsync();
                        
                        // Clear authentication cookie
                        context.Response.Cookies.Delete("MoonPhim.Auth.Azure");
                        
                        // Redirect to home with a locked status code or query string
                        context.Response.Redirect("/?auth=login&error=locked");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
