// Models/Entities/Role.cs
using Microsoft.AspNetCore.Identity;

namespace MovieWeb.Models.Entities
{
    public class Role : IdentityRole<int>
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual ICollection<User> Users { get; set; } = new HashSet<User>();
    }
}
