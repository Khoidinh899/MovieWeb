// Thêm file này, ví dụ: Models/DTOs/PaymentHistoryDto.cs
using MovieWeb.Models; // HoC: MovieWeb.Models.DTOs
using System.Collections.Generic;

namespace MovieWeb.Models.DTOs
{
    public class PaymentHistoryDto
    {
        public List<PaymentHistoryViewModel> Transactions { get; set; } = new List<PaymentHistoryViewModel>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalTransactions { get; set; }
        public int PageSize { get; set; }
        public bool HasTransactions => Transactions.Any();
    }
}