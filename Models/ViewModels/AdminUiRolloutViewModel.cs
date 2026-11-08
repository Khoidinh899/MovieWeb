using System;
using System.Collections.Generic;

namespace MovieWeb.Models.ViewModels
{
    public class AdminUiRolloutViewModel
    {
        public string CurrentMode { get; set; } = "off";
        public DateTime? ModeUpdatedAt { get; set; }
        
        public int TotalUsers { get; set; }
        public int V2Users { get; set; }
        public int V1Users { get; set; }
        public int UnpickedUsers { get; set; }
        public double V2Percent { get; set; }
        public double V1Percent { get; set; }
        public double UnpickedPercent { get; set; }
        public int RevertedUsers { get; set; }
        public int TotalSwitches { get; set; }

        // Survey stats
        public int SurveyTotal { get; set; }
        public int SurveyNew { get; set; }
        public int SurveyOld { get; set; }
        public int SurveySame { get; set; }
        public double SurveyNewPercent { get; set; }
        public double SurveyOldPercent { get; set; }
        public double SurveySamePercent { get; set; }

        // Conversion stats for invitation broadcasts
        public List<BroadcastConversionDto> BroadcastConversions { get; set; } = new();

        // Feedbacks list
        public List<UiFeedbackItemDto> Feedbacks { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalFeedbacks { get; set; }
        public string SelectedFilter { get; set; } = "all";
    }

    public class BroadcastConversionDto
    {
        public int BroadcastId { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int RecipientCount { get; set; }
        public int ReadCount { get; set; }
        public double ReadRate { get; set; }
        public int ConvertedToV2Count { get; set; }
        public double ConversionRate { get; set; }
    }

    public class UiFeedbackItemDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Email { get; set; }
        public string Choice { get; set; } = "";
        public string? Comment { get; set; }
        public string UiAtVote { get; set; } = "v1";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
