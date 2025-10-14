using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;

namespace MovieWeb.Services.Interfaces
{
    public interface ISubscriptionService
    {
        // ===== SUBSCRIPTION PLANS =====
        Task<List<SubscriptionPlanDto>> GetAllPlansAsync(bool activeOnly = true);
        Task<SubscriptionPlanDto?> GetPlanByIdAsync(int planId);
        Task<List<SubscriptionPlanDto>> GetPlansByTypeAsync(string planType);

        // ===== USER SUBSCRIPTIONS =====
        Task<UserSubscriptionDto?> GetActiveSubscriptionAsync(int userId);
        Task<UserSubscription?> GetSubscriptionByIdAsync(int subscriptionId);
        Task<List<UserSubscriptionDto>> GetUserSubscriptionHistoryAsync(int userId);
        Task<bool> HasActiveSubscriptionAsync(int userId);
        Task<bool> IsPremiumUserAsync(int userId);
        Task<bool> FulfillOrderAsync(string sessionId);


        // ===== SUBSCRIPTION ACTIONS =====
        Task<UserSubscription> CreateSubscriptionAsync(int userId, int planId, string? stripeSubscriptionId = null);
        Task<bool> CancelSubscriptionAsync(int subscriptionId, string? reason = null, bool immediately = false);
        Task<bool> RenewSubscriptionAsync(int subscriptionId);
        Task<bool> ExtendSubscriptionAsync(int subscriptionId, int months);
        Task UpdateSubscriptionStatusAsync(int subscriptionId, string status);

        // ===== STUDENT VERIFICATION =====
        Task<bool> SendStudentVerificationEmailAsync(int userId, string studentEmail);
        Task<bool> VerifyStudentEmailAsync(int userId, string verificationCode);
        Task<bool> IsStudentEmailValidAsync(string email);

        // ===== TRANSACTIONS =====
        Task<Transaction> CreateTransactionAsync(int userId, int planId, string paymentMethod = "stripe");
        Task<bool> CompleteTransactionAsync(int transactionId, string? stripePaymentIntentId = null, string? stripeChargeId = null);
        Task<bool> FailTransactionAsync(int transactionId, string? reason = null);
        Task<List<TransactionDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10);

        // ===== ADMIN FUNCTIONS =====
        Task<RevenueStatsDto> GetRevenueStatsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<List<PlanRevenueDto>> GetRevenueByPlanAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<List<RevenueTrendDto>> GetRevenueTrendAsync(int days = 30);
        Task<List<UserSubscriptionDto>> GetExpiringSubscriptionsAsync(int daysThreshold = 7);
        Task<PaginatedResponse<UserSubscriptionDto>> GetAllSubscriptionsAsync(int page = 1, int pageSize = 20, string? status = null);
        Task<PaginatedResponse<TransactionDto>> GetAllTransactionsAsync(int page = 1, int pageSize = 20, string? status = null);
        Task<int> GetTotalActiveSubscriptionsAsync();
        Task<int> GetTotalPremiumUsersAsync();

        // ===== STRIPE INTEGRATION HELPERS =====
        Task<bool> UpdateAsync(UserSubscription subscription);
        Task<bool> CancelAsync(string stripeSubscriptionId, bool immediately = false);

        // ===== NOTIFICATIONS =====
        Task SendExpiryReminderAsync(int subscriptionId);
        Task SendPaymentFailedNotificationAsync(int userId, int transactionId);
        Task SendSubscriptionActivatedNotificationAsync(int userId, int subscriptionId);

        // ===== HELPER METHODS =====
        Task<User?> GetUserByIdAsync(int userId);
        Task<SubscriptionPlan?> GetPlanEntityByIdAsync(int planId);
        // ===== CheckActive =====
        Task<UserSubscriptionDto?> GetActiveOrCancelledWithTimeAsync(int userId);
        Task<bool> UpgradeSubscriptionAsync(int userId, int newPlanId, string? stripeSubscriptionId = null);
    }
}