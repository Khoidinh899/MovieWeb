using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Helpers;
using MovieWeb.Models.Entities;
using ApiMovie = MovieWeb.Models.API.Movie;
using DbMovie = MovieWeb.Models.Entities.Movie;

namespace MovieWeb.Services;

public interface IMovieSyncService
{
    Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies);
    Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies, int minYear);
    Task BackfillAllEpisodesAsync();
    Task BackfillSingleMoviesAsync();
    Task SyncMovieFromApiBySlug(string apiSlug, int movieId);
    Task BackfillFromVSMovAsync(int fromPage = 1, int toPage = 10);
    Task SyncMoviesFromVSMovApiAsync(int startPage = 1, int endPage = 10);
    Task<DbMovie?> SyncSingleMovieFromVSMovBySlugAsync(string slug);
    Task<(DbMovie? Movie, bool WasUpdatedOrAdded)> SyncSingleMovieFromVSMovBySlugWithStatusAsync(string slug);
    Task EnsureVSMovEpisodesAsync(int movieId);
}

public sealed class MovieSyncService(
    MovieWebDbContext context,
    IVSMovService provider,
    ILogger<MovieSyncService> logger,
    ICategorySyncService categories,
    ICountrySyncService countries,
    IActorSyncService actors,
    IDirectorSyncService directors) : IMovieSyncService
{
    private static readonly SemaphoreSlim[] SyncLocks = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task EnsureVSMovEpisodesAsync(int movieId)
    {
        var sources = await context.Episodes.Where(e => e.MovieId == movieId)
            .Select(e => e.LinkM3u8).ToListAsync();
        if (sources.Any(VSMovSource.IsSupported) && sources.Where(s => s is not null).All(VSMovSource.IsSupported)) return;
        // Slug nguồn VSMov: khác Slug khi admin đã đổi URL phim.
        var slug = await context.Movies.Where(m => m.MovieId == movieId)
            .Select(m => m.SourceSlug ?? m.Slug).SingleOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(slug)) await SyncAsync(slug, movieId);
    }

    public async Task SyncMovieFromApiBySlug(string apiSlug, int movieId) =>
        await SyncAsync(apiSlug, movieId);

    public Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies) =>
        SyncMoviesFromApiToDbAsync(apiMovies, 0);

    public async Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies, int minYear)
    {
        foreach (var movie in apiMovies.Where(m => m.Year >= minYear && !string.IsNullOrWhiteSpace(m.Slug)))
            await SyncAsync(movie.Slug);
    }

    public Task<DbMovie?> SyncSingleMovieFromVSMovBySlugAsync(string slug) => SyncMovieAsync(slug);

    private async Task<DbMovie?> SyncMovieAsync(string slug) => (await SyncAsync(slug)).Movie;

    public Task<(DbMovie? Movie, bool WasUpdatedOrAdded)> SyncSingleMovieFromVSMovBySlugWithStatusAsync(string slug) =>
        SyncAsync(slug);

    public Task SyncMoviesFromVSMovApiAsync(int startPage = 1, int endPage = 10) =>
        BackfillFromVSMovAsync(startPage, endPage);

    public async Task BackfillFromVSMovAsync(int fromPage = 1, int toPage = 10)
    {
        if (fromPage < 1 || toPage < fromPage) throw new ArgumentOutOfRangeException(nameof(fromPage));
        for (var page = fromPage; page <= toPage; page++)
        {
            var response = await provider.GetLatestMoviesAsync(page);
            if (response?.Status != true || response.Items is null || response.Items.Count == 0) break;
            foreach (var movie in response.Items.Where(m => !string.IsNullOrWhiteSpace(m.Slug)))
                await SyncAsync(movie.Slug);
        }
    }

    public Task BackfillAllEpisodesAsync() => BackfillStoredMoviesAsync(true);
    public Task BackfillSingleMoviesAsync() => BackfillStoredMoviesAsync(false);

    private async Task BackfillStoredMoviesAsync(bool series)
    {
        var slugs = await context.Movies.Where(m => m.IsActive == true &&
            (series ? m.Type == "series" || m.Type == "hoathinh" : m.Type != "series" && m.Type != "hoathinh"))
            .OrderBy(m => m.MovieId).Select(m => m.SourceSlug ?? m.Slug).ToListAsync();
        foreach (var slug in slugs) await SyncAsync(slug);
    }

    private async Task<(DbMovie? Movie, bool WasUpdatedOrAdded)> SyncAsync(string slug, int? movieId = null)
    {
        var gate = SyncLocks[(uint)StringComparer.Ordinal.GetHashCode(slug) % (uint)SyncLocks.Length];
        await gate.WaitAsync();
        try { return await SyncCoreAsync(slug, movieId); }
        catch (Npgsql.PostgresException exception) when (exception.SqlState == "55P03")
        {
            logger.LogWarning("Catalog busy; deferring sync for {Slug}", slug);
            return (null, false);
        }
        finally { gate.Release(); }
    }

    private static string ContentSnapshot(DbMovie movie) => System.Text.Json.JsonSerializer.Serialize(new
    {
        movie.ApiId, movie.Name, movie.OriginalName, movie.Description, movie.Type, movie.Status,
        movie.PosterUrl, movie.ThumbUrl, movie.Trailer, movie.TrailerUrl, movie.Time,
        movie.EpisodeCurrent, movie.EpisodeTotal, movie.Quality, movie.Language, movie.Year,
        Categories = movie.Categories.Select(c => c.CategoryId).Order().ToArray(),
        Countries = movie.Countries.Select(c => c.CountryId).Order().ToArray(),
        Actors = movie.Actors.Select(a => a.ActorId).Order().ToArray(),
        Directors = movie.Directors.Select(d => d.DirectorId).Order().ToArray(),
        Episodes = movie.Episodes.OrderBy(e => e.Slug).ThenBy(e => e.ServerName)
            .Select(e => new { e.Slug, e.EpisodeName, e.ServerName, e.LinkM3u8 }).ToArray()
    });

    private async Task<(DbMovie? Movie, bool WasUpdatedOrAdded)> SyncCoreAsync(string slug, int? movieId)
    {
        var response = await provider.GetMovieDetailAsync(slug);
        if (response?.Status != true || response.Item is null) return (null, false);
        var detail = response.Item;
        var sourceEpisodes = (response.Episodes ?? [])
            .SelectMany(server => (server.ServerData ?? []).Select(ep => new
            {
                Server = VSMovSource.NormalizeServer(server.ServerName), Episode = ep
            }))
            .Where(item => VSMovSource.IsSupported(item.Episode.LinkEmbed))
            .DistinctBy(item => (item.Server, item.Episode.Slug)).ToList();
        // Never clear existing data on provider errors or an incomplete response.
        if (sourceEpisodes.Count == 0) return (null, false);

        await using var transaction = await CatalogWriteLock.AcquireAsync(context);
        try
        {
        var movie = await context.Movies.Include(m => m.Episodes).Include(m => m.Categories)
            .Include(m => m.Countries).Include(m => m.Actors).Include(m => m.Directors)
            .SingleOrDefaultAsync(m => movieId.HasValue ? m.MovieId == movieId
                : m.SourceSlug == slug || (m.SourceSlug == null && m.Slug == slug));
        var isNew = movie is null;
        // Slug nguồn mới trùng URL mà admin đã đặt cho một phim khác: không tạo phim (Slug là duy nhất).
        if (isNew && await context.Movies.AnyAsync(m => m.Slug == (detail.Slug ?? slug)))
        {
            logger.LogWarning("Skip creating VSMov movie {Slug}: slug is used as a custom URL by another movie", detail.Slug ?? slug);
            await transaction.RollbackAsync();
            return (null, false);
        }
        movie ??= new DbMovie { Slug = detail.Slug ?? slug, IsActive = true, CreatedAt = DateTime.Now };
        if (isNew) context.Movies.Add(movie);

        var previousContent = ContentSnapshot(movie);
        movie.ApiId = detail.Id.ToString();
        movie.Name = detail.Name;
        movie.OriginalName = detail.OriginName;
        movie.Description = detail.Content;
        movie.Type = detail.Type;
        movie.Status = detail.Status;
        movie.PosterUrl = detail.PosterUrl;
        movie.ThumbUrl = detail.ThumbUrl;
        movie.Trailer = detail.TrailerUrl;
        movie.Time = detail.Time;
        movie.EpisodeCurrent = detail.EpisodeCurrent;
        movie.EpisodeTotal = detail.EpisodeTotal;
        movie.Quality = detail.Quality;
        movie.Language = detail.Lang;
        movie.Year = detail.Year;

        if (detail.Category is not null) movie.Categories = await categories.SyncCategoriesAsync(detail.Category);
        if (detail.Country is not null) movie.Countries = await countries.SyncCountriesAsync(detail.Country);
        if (detail.Actor is not null) movie.Actors = await actors.SyncActorsAsync(detail.Actor);
        if (detail.Director is not null) movie.Directors = await directors.SyncDirectorsAsync(detail.Director);

        var remaining = movie.Episodes.ToList();
        foreach (var source in sourceEpisodes)
        {
            var item = source.Episode;
            var existing = remaining.FirstOrDefault(e => e.Slug == item.Slug &&
                VSMovSource.NormalizeServer(e.ServerName) == source.Server)
                ?? remaining.FirstOrDefault(e => !VSMovSource.IsSupported(e.LinkM3u8) && e.EpisodeName == item.Name);
            if (existing is null)
            {
                existing = new Episode { EpisodeName = item.Name ?? "1", Slug = item.Slug ?? "tap-1" };
                movie.Episodes.Add(existing);
            }
            else remaining.Remove(existing);
            existing.ServerName = source.Server;
            existing.EpisodeName = item.Name ?? "1";
            existing.Slug = item.Slug ?? "tap-1";
            existing.LinkM3u8 = item.LinkEmbed;
        }
        // Keep EpisodeIds referenced by Watch Party; retire old sources without deleting rows.
        foreach (var obsolete in remaining.Where(e => !VSMovSource.IsSupported(e.LinkM3u8)))
        {
            obsolete.LinkM3u8 = null;
        }
        movie.TrailerUrl = sourceEpisodes[0].Episode.LinkEmbed;
        // Related-entity services may save this context, so compare snapshots rather than EF flags.
        var contentChanged = isNew || previousContent != ContentSnapshot(movie);
        if (contentChanged) movie.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync();
        logger.LogInformation("VSMov synced movie {MovieId}: {Count} episodes", movie.MovieId, sourceEpisodes.Count);
        await transaction.CommitAsync();
        return (movie, contentChanged);
        }
        catch
        {
            // Related services may have saved before a later failure. Never flush that graph again.
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
