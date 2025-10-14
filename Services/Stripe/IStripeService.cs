using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using Stripe.Checkout;

namespace MovieWeb.Services.Interfaces
{
    public interface IStripeService
    {
        // ===== CUSTOMER MANAGEMENT =====
        Task<string> CreateOrGetCustomerAsync(User user);
        Task<bool> UpdateCustomerAsync(string customerId, User user);
        
        // ===== PRODUCT & PRICE MANAGEMENT =====
        Task<bool> SyncSubscriptionPlansToStripeAsync();
        Task<string> CreateProductAsync(SubscriptionPlan plan);
        Task<string> CreatePriceAsync(string productId, SubscriptionPlan plan);

        // ===== CHECKOUT SESSION =====
        Task<Session> CreateCheckoutSessionAsync(
            User user,
            SubscriptionPlan plan,
            string successUrl,
            string cancelUrl
        );
        Task<bool> ActivateSubscriptionFromSession(string sessionId);
        
        // ===== SUBSCRIPTION MANAGEMENT =====
        Task<bool> CancelSubscriptionAsync(string stripeSubscriptionId, bool immediately = false);
        Task<bool> ReactivateSubscriptionAsync(string stripeSubscriptionId);
        
        // ===== WEBHOOK HANDLING =====
        Task HandleWebhookAsync(string json, string stripeSignature);
        Task HandleCheckoutCompletedAsync(Session session);
        Task HandleSubscriptionUpdatedAsync(Stripe.Subscription subscription);
        Task HandleSubscriptionDeletedAsync(Stripe.Subscription subscription);

        // ✅ Signature mới khớp với StripeService.cs
        Task HandlePaymentSucceededAsync(Stripe.Invoice invoice, string subscriptionId, string? paymentIntentId, string? chargeId);
        Task HandlePaymentFailedAsync(Stripe.Invoice invoice, string subscriptionId);
        
        // ===== UTILITY =====
        decimal ConvertVNDToUSD(decimal amountVND);
        decimal ConvertUSDToVND(decimal amountUSD);
    }
}
