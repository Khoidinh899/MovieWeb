using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("WatchPartyMembers")]
    public class WatchPartyMember
    {
        [Key]
        public int Id { get; set; }

        public Guid RoomId { get; set; }

        public int? UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? AvatarUrl { get; set; }

        [StringLength(100)]
        public string? ConnectionId { get; set; }

        public bool IsHost { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LeftAt { get; set; }

        // Navigation properties
        [ForeignKey("RoomId")]
        public virtual WatchPartyRoom Room { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
