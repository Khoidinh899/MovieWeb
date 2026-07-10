using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    public class Episode
    {
        [Key]
        public int EpisodeId { get; set; }

        [Required]
        public int MovieId { get; set; }

        [ForeignKey("MovieId")]
        public virtual Movie Movie { get; set; }

        [StringLength(200)]
        public string? ServerName { get; set; } 

        [Required]
        [StringLength(255)]
        public string EpisodeName { get; set; }

        [Required]
        [StringLength(500)]
        public string Slug { get; set; }
        public string? LinkM3u8 { get; set; }
    }
}