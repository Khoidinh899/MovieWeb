// Models/Entities/UserRequestMovie.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("UserRequestMovie")]
    public class UserRequestMovie
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        [Column("request_id")]
        [Required]
        public int RequestId { get; set; }

        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("RequestId")]
        public virtual RequestsMovie Request { get; set; } = null!;
    }
}