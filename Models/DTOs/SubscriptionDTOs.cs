using System;
using System.ComponentModel.DataAnnotations;

namespace MovieWeb.Models.DTOs
{
    // ===== REQUEST DTOs =====
    
    public class CreateCheckoutSessionRequest
    {
        [Required]
        public int PlanId { get; set; }
        
        public string? SuccessUrl { get; set; }
        
        public string? CancelUrl { get; set; }
    }

    public class StudentVerificationRequest
    {
        [Required]
        [EmailAddress]
        public string StudentEmail { get; set; } = string.Empty;
    }

    public class VerifyStudentCodeRequest
    {
        [Required]
        public string VerificationCode { get; set; } = string.Empty;
    }

    public class CancelSubscriptionRequest
    {
        [Required]
        public int SubscriptionId { get; set; }
        
        public string? Reason { get; set; }
        
        public bool CancelImmediately { get; set; } = false;
    }

    // ===== RESPONSE DTOs =====
    
    public class CheckoutSessionResponse
    {
        public string SessionId { get; set; } = string.Empty;
        
        public string SessionUrl { get; set; } = string.Empty;
        
        public string PublishableKey { get; set; } = string.Empty;
    }

    public class SubscriptionPlanDto
    {
        public int PlanId { get; set; }
        
        public string PlanName { get; set; } = string.Empty;
        
        public string DisplayName { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public decimal PriceVND { get; set; }
        
        public decimal PriceUSD { get; set; }
        
        public int DurationMonths { get; set; }
        
        public int ActualMonths { get; set; }
        
        public int BonusMonths { get; set; }
        
        public string PlanType { get; set; } = string.Empty;
        
        public bool IsPopular { get; set; }
        
        public string PriceDisplay { get; set; } = string.Empty;
        
        public string DurationDisplay { get; set; } = string.Empty;
        
        public decimal? MonthlySavings { get; set; }
        
        public string? SavingsDisplay { get; set; }
        
        public List<string> Features { get; set; } = new List<string>();
    }

    public class UserSubscriptionDto
    {
        public int SubscriptionId { get; set; }
        
        public int UserId { get; set; }
        
        public SubscriptionPlanDto? Plan { get; set; }
        
        // FIX: Thêm property này
        public string? StripeSubscriptionId { get; set; }
        
        public string Status { get; set; } = string.Empty;
        
        public string StatusDisplay { get; set; } = string.Empty;
        
        public DateTime StartDate { get; set; }
        
        public DateTime EndDate { get; set; }
        
        public DateTime? NextBillingDate { get; set; }
        
        public bool AutoRenew { get; set; }
        
        public bool IsActive { get; set; }
        
        public int DaysRemaining { get; set; }
        
        public bool IsExpiringSoon { get; set; }
    }

    public class TransactionDto
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int TransactionId { get; set; }
        
        public string TransactionCode { get; set; } = string.Empty;
        
        public decimal AmountVND { get; set; }
        
        public string AmountDisplay { get; set; } = string.Empty;
        
        public string Currency { get; set; } = string.Empty;
        
        public string PaymentMethod { get; set; } = string.Empty;
        
        public string Status { get; set; } = string.Empty;
        
        public string StatusDisplay { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        public SubscriptionPlanDto? Plan { get; set; }
    }

    public class RevenueStatsDto
    {
        /// <summary>
        /// Tổng doanh thu từ các giao dịch thành công (VND).
        /// </summary>
        public decimal TotalRevenue { get; set; }

        /// <summary>
        /// Tổng số lượng tất cả giao dịch (completed, pending, failed...).
        /// </summary>
        public int TotalTransactions { get; set; }

        /// <summary>
        /// Số lượng giao dịch đã thành công.
        /// </summary>
        public int CompletedTransactions { get; set; }

        /// <summary>
        /// Số lượng giao dịch đang chờ xử lý.
        /// </summary>
        public int PendingTransactions { get; set; }

        /// <summary>
        /// Số lượng giao dịch đã thất bại.
        /// </summary>
        public int FailedTransactions { get; set; }

        /// <summary>
        /// Số lượng giao dịch đã được hoàn tiền.
        /// </summary>
        public int RefundedTransactions { get; set; }
    }

    public class RevenueTrendDto
    {
        public string Date { get; set; } = string.Empty;
        
        public decimal Revenue { get; set; }
        
        public int Transactions { get; set; }
    }

    public class PlanRevenueDto
    {
        public string PlanType { get; set; } = string.Empty;
        
        public string DisplayName { get; set; } = string.Empty;
        
        public int TransactionCount { get; set; }
        
        public decimal TotalRevenue { get; set; }
        
        public decimal AvgRevenue { get; set; }
        
        public string TotalRevenueDisplay => $"{TotalRevenue:N0} ₫";
    }

    // ===== API RESPONSE WRAPPERS =====
    
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        
        public string? Message { get; set; }
        
        public T? Data { get; set; }
        
        public List<string>? Errors { get; set; }
    }

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        
        public int TotalItems { get; set; }
        
        public int PageNumber { get; set; }
        
        public int PageSize { get; set; }
        
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
        
        public bool HasPrevious => PageNumber > 1;
        
        public bool HasNext => PageNumber < TotalPages;
    }
}