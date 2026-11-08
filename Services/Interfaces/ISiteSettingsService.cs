using System.Threading.Tasks;

namespace MovieWeb.Services.Interfaces
{
    public interface ISiteSettingsService
    {
        Task<T?> GetAsync<T>(string key);
        T? Get<T>(string key);
        Task SetAsync<T>(string key, T value);
        void Set<T>(string key, T value);
        Task RemoveAsync(string key);
        void Remove(string key);
    }
}
