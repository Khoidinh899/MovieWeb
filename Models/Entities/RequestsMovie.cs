// Models/Entities/RequestsMovie.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("RequestsMovie")]
    public class RequestsMovie
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("movie_title")]
        [MaxLength(255)]
        public string? MovieTitle { get; set; }

        [Column("movie_year")]
        public int? MovieYear { get; set; }

        [Column("ophim_slug")]
        [MaxLength(255)]
        public string? OphimSlug { get; set; }

        [Column("request_count")]
        [Required]
        public int RequestCount { get; set; } = 1;

        [Column("status")]
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Chờ đồng bộ";

        [Column("conversation_log")]
        public string? ConversationLog { get; set; }

        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("admin_note")]
        [MaxLength(500)]
        public string? AdminNote { get; set; }

        // Navigation property - Danh sách users đã request phim này
        public virtual ICollection<UserRequestMovie> UserRequests { get; set; } = new List<UserRequestMovie>();
    }

    /// <summary>
    /// Helper class: Constants cho Status
    /// </summary>
    public static class RequestStatus
    {
        public const string Pending = "Chờ đồng bộ";
        public const string NeedsVerification = "Cần xác minh thủ công";
        public const string Completed = "Đã hoàn tất";
    }
}