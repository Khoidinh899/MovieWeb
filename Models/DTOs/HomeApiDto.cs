namespace MovieWeb.Models.DTOs
{
    public class HomeApiDto
    {
        public List<MovieDto> Banners { get; set; } = new List<MovieDto>();
        public List<MovieSectionDto> Sections { get; set; } = new List<MovieSectionDto>();
    }

    public class MovieSectionDto
    {
        public string Title { get; set; }
        public List<MovieDto> Movies { get; set; } = new List<MovieDto>();
    }
}