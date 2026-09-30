using System.Threading.Tasks;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Services.Interfaces
{
    public interface IAnnouncementService
    {
        Task<AnnouncementSettingsDto> GetSettingsAsync();
        Task<bool> SaveSettingsAsync(AnnouncementSettingsDto settings);
    }
}
