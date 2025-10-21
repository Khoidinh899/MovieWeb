// Filters/HangfireAuthorizationFilter.cs
using Hangfire.Dashboard;
using MovieWeb.Services;

namespace MovieWeb.Filters
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            
            // Kiểm tra user đã đăng nhập chưa
            if (httpContext.User?.Identity?.IsAuthenticated != true)
            {
                return false;
            }
            
            // Option 1: Kiểm tra IsAdmin từ Claims
            var isAdminClaim = httpContext.User.HasClaim(c => 
                c.Type == "IsAdmin" && c.Value.ToLower() == "true");
            
            if (isAdminClaim)
            {
                return true;
            }
            
            // Option 2: Kiểm tra qua IAuthService (backup method)
            try
            {
                var authService = httpContext.RequestServices.GetService<IAuthService>();
                if (authService != null)
                {
                    var user = authService.GetCurrentUserAsync().Result;
                    return user?.IsAdmin == true;
                }
            }
            catch
            {
                // Nếu có lỗi, trả về false
                return false;
            }
            
            return false;
        }
    }
}