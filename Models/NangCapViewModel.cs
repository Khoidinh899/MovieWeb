using MovieWeb.Models.DTOs;
using System.Collections.Generic;

// Sửa namespace thành MovieWeb.Models để khớp với vị trí file
namespace MovieWeb.Models 
{
    public class NangCapViewModel
    {
        // Thông tin người dùng
        public string UserName { get; set; }
        public string Avatar { get; set; }
        public string CurrentStatus { get; set; }
        public decimal Balance { get; set; }

        // Danh sách các gói cước để hiển thị
        public List<SubscriptionPlanDto> Plans { get; set; }

        public NangCapViewModel()
        {
            Plans = new List<SubscriptionPlanDto>();
        }
    }
}