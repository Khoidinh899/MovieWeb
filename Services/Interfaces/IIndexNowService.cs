using System.Collections.Generic;
using System.Threading.Tasks;

namespace MovieWeb.Services.Interfaces;

public interface IIndexNowService
{
    string GetIndexNowKey();
    Task<bool> SubmitUrlsAsync(IEnumerable<string> urls);
    Task<bool> NotifyNewMovieOrEpisodeAsync(string movieSlug, int movieId, string? episodeSlug = null, int? episodeId = null);
}
