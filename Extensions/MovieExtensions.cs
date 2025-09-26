// Extensions/MovieExtensions.cs
using MovieWeb.Models.API;
using MovieWeb.Models.Entities;
using ApiMovie = MovieWeb.Models.API.Movie;
using EntityMovie = MovieWeb.Models.Entities.Movie;
using ApiCategory = MovieWeb.Models.API.Category;
using EntityCategory = MovieWeb.Models.Entities.Category;
using ApiCountry = MovieWeb.Models.API.Country;
using EntityCountry = MovieWeb.Models.Entities.Country;

namespace MovieWeb.Extensions
{
    public static class MovieExtensions
    {
        public static ApiMovie ToApiModel(this EntityMovie dbMovie)
        {
            return new ApiMovie
            {
                Id = dbMovie.ApiId ?? dbMovie.MovieId.ToString(),
                Name = dbMovie.Name ?? "",
                Slug = dbMovie.Slug ?? "",
                OriginName = dbMovie.OriginalName ?? "",
                Content = dbMovie.Content ?? "",
                Type = dbMovie.Type ?? "",
                Status = dbMovie.Status ?? "",
                PosterUrl = dbMovie.PosterUrl ?? "",
                ThumbUrl = dbMovie.ThumbUrl ?? "",
                TrailerUrl = dbMovie.TrailerUrl ?? "",
                Time = dbMovie.Time ?? "",
                EpisodeCurrent = dbMovie.EpisodeCurrent ?? "",
                EpisodeTotal = dbMovie.EpisodeTotal ?? "",
                Quality = dbMovie.Quality ?? "",
                Lang = dbMovie.Language ?? "",
                Year = dbMovie.Year ?? 0,
                View = dbMovie.ViewCount ?? 0,
                IsCopyright = dbMovie.IsCopyright ?? false,
                SubDocquyen = false,
                Chieurap = false,
                Notify = "",
                Showtimes = "",
                
                Category = new List<ApiCategory>(),
                Country = new List<ApiCountry>(),
                Actor = new List<string>(),
                Director = new List<string>()
                
                // Xóa phần Modified vì không có ModifiedInfo class
            };
        }

        public static List<ApiMovie> ToApiModelList(this List<EntityMovie> dbMovies)
        {
            return dbMovies.Select(movie => movie.ToApiModel()).ToList();
        }

        public static string GetFullPosterUrl(this ApiMovie movie, string cdnDomain = "https://img.ophim.live/uploads/movies/")
        {
            if (string.IsNullOrEmpty(movie.PosterUrl))
                return "https://via.placeholder.com/300x450/333333/ffffff?text=" + Uri.EscapeDataString(movie.Name ?? "Movie");

            if (movie.PosterUrl.StartsWith("http"))
                return movie.PosterUrl;

            return cdnDomain + movie.PosterUrl.TrimStart('/');
        }

        public static string GetFullThumbUrl(this ApiMovie movie, string cdnDomain = "https://img.ophim.live/uploads/movies/")
        {
            if (string.IsNullOrEmpty(movie.ThumbUrl))
                return movie.GetFullPosterUrl(cdnDomain);

            if (movie.ThumbUrl.StartsWith("http"))
                return movie.ThumbUrl;

            return cdnDomain + movie.ThumbUrl.TrimStart('/');
        }
    }
}