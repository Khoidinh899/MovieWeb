using System.Collections.Generic;
using System.Threading.Tasks;
using Hangfire;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Jobs
{
    [AutomaticRetry(Attempts = 0)]
    public class BroadcastJob
    {
        private readonly IBroadcastService _broadcastService;

        public BroadcastJob(IBroadcastService broadcastService)
        {
            _broadcastService = broadcastService;
        }

        public async Task Execute(int broadcastId, List<int>? targetUserIds = null)
        {
            await _broadcastService.ExecuteBroadcastJobAsync(broadcastId, targetUserIds);
        }
    }
}
