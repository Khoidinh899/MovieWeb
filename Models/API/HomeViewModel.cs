using System.Collections.Generic;

namespace MovieWeb.Models.API
{
    public class HomeViewModel
    {
        public List<Movie> HotMovies { get; set; } = new List<Movie>();

        public List<Movie> LatestMovies { get; set; } = new List<Movie>();
        public List<Movie> SingleMovies { get; set; } = new List<Movie>();
        public List<Movie> TvSeries { get; set; } = new List<Movie>();
        public List<Movie> HoatHinhMovies { get; set; } = new List<Movie>();


        // Banner hiển thị phim hot
        public List<Movie> BannerMovies { get; set; } = new List<Movie>();
        public string CdnImageDomain { get; set; }

    }
}
