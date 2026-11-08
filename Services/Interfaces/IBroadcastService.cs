using System.Collections.Generic;
using System.Threading.Tasks;
using MovieWeb.Models.Entities;

namespace MovieWeb.Services.Interfaces
{
    public interface IBroadcastService
    {
        Task<AdminBroadcast> CreateAndEnqueueBroadcastAsync(
            string title,
            string content,
            string? styleJson,
            string? url,
            string targetType,
            List<int>? targetUserIds,
            bool sendPush,
            int? createdBy);

        Task ExecuteBroadcastJobAsync(int broadcastId, List<int>? targetUserIds = null);
        Task<bool> RevokeBroadcastAsync(int broadcastId);
    }
}
