using MovieWeb.Models.Entities;

namespace MovieWeb.Models.DTOs
{
    public class MovieDto
    {
        public int MovieId { get; set; }
        public string Slug { get; set; }
        public string Name { get; set; }
        public string? OriginalName { get; set; }
        public string? Content { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? PosterUrl { get; set; }
        public string? ThumbUrl { get; set; }
        public string? TrailerUrl { get; set; }
        public string? Time { get; set; }
        public string? EpisodeCurrent { get; set; }
        public string? EpisodeTotal { get; set; }
        public string? Quality { get; set; }
        public string? Language { get; set; }
        public int? Year { get; set; }
        public int ViewCount { get; set; }
        public decimal Rating { get; set; }
        public int RatingCount { get; set; }
        public bool IsRecommended { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        public List<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
        public List<CountryDto> Countries { get; set; } = new List<CountryDto>();
        public List<ActorDto> Actors { get; set; } = new List<ActorDto>();
        public List<DirectorDto> Directors { get; set; } = new List<DirectorDto>();
    }
    
    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
    }
    
    public class CountryDto
    {
        public int CountryId { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
    }
    
    public class ActorDto
    {
        public int ActorId { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
    }
    
    public class DirectorDto
    {
        public int DirectorId { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
    }
}