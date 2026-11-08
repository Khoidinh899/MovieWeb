using System;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Models.ViewModels
{
    public class UserInterfaceViewModel
    {
        public string CurrentUi { get; set; } = "v1";
        public string RolloutMode { get; set; } = "members";
        public string SelectedChoice { get; set; } = "";
        public string? FeedbackComment { get; set; }
        public DateTime? FeedbackUpdatedAt { get; set; }
        public int SwitchCount { get; set; }
        public UserProfileDto? UserProfile { get; set; }
    }
}
