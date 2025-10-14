using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using System.Linq;

namespace MovieWeb.Extensions
{
    public static class UserMappingExtensions
    {
        public static UserProfileDto ToUserProfileDto(this User user)
        {
            if (user == null) return null!;

            return new UserProfileDto
            {
                UserId = user.Id,                     // IdentityUser<int> Id
                Username = user.UserName ?? string.Empty,  // IdentityUser UserName
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Avatar = user.Avatar,
                IsActive = user.IsActive ?? false,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt ?? DateTime.Now,
                UpdatedAt = user.UpdatedAt,
                LastLogin = user.LastLogin,
                RoleId = user.RoleId,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Address = user.Address,
                Bio = user.Bio,

                // Thống kê hoạt động
                TotalFavorites = user.Favorites?.Count ?? 0,
                TotalComments = user.Comments?.Count ?? 0,
                TotalRatings = user.Ratings?.Count ?? 0,
                TotalWatchHistory = user.WatchHistories?.Count ?? 0,

                // Subscription
                SubscriptionType = user.SubscriptionType,
                SubscriptionStartDate = user.SubscriptionStartDate,
                SubscriptionEndDate = user.SubscriptionEndDate
            };
        }

        public static List<UserProfileDto> ToUserProfileDtoList(this IEnumerable<User> users)
        {
            return users.Select(u => u.ToUserProfileDto()).ToList();
        }
    }
}
