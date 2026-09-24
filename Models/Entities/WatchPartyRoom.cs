using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("WatchPartyRooms")]
    public class WatchPartyRoom
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(10)]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(64)]
        public string? ShareToken { get; set; }

        public int MovieId { get; set; }

        [StringLength(255)]
        public string MovieSlug { get; set; } = string.Empty;

        public int? EpisodeId { get; set; }

        public int? EpisodeNumber { get; set; }

        [StringLength(50)]
        public string? ServerName { get; set; }

        public int HostUserId { get; set; }

        public double CurrentTime { get; set; } = 0;

        public bool IsPlaying { get; set; } = false;

        public bool IsPrivate { get; set; } = false;

        [StringLength(20)]
        public string? PinCode { get; set; }

        public int MaxMembers { get; set; } = 20;

        public bool OnlyHostControl { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? EndedAt { get; set; }

        // Navigation properties
        [ForeignKey("MovieId")]
        public virtual Movie Movie { get; set; } = null!;

        [ForeignKey("HostUserId")]
        public virtual User HostUser { get; set; } = null!;

        [ForeignKey("EpisodeId")]
        public virtual Episode? Episode { get; set; }

        public virtual ICollection<WatchPartyMember> Members { get; set; } = new List<WatchPartyMember>();
    }
}
