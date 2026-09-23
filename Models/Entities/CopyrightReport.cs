using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("CopyrightReports")]
    public class CopyrightReport
    {
        [Key]
        [Column("ReportId")]
        public int ReportId { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("FullName")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(150)]
        [Column("Organization")]
        public string? Organization { get; set; }

        [Required]
        [MaxLength(500)]
        [Column("MovieUrl")]
        public string MovieUrl { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("MovieTitle")]
        public string? MovieTitle { get; set; }

        [Required]
        [Column("ProofDetails")]
        public string ProofDetails { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("Status")]
        public string Status { get; set; } = "Pending";

        [Column("AdminNote")]
        public string? AdminNote { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
