using System.Threading.Tasks;

namespace MovieWeb.Services.Interfaces
{
    public interface ITurnstileService
    {
        Task<bool> VerifyTokenAsync(string token, string? ipAddress);
    }
}
