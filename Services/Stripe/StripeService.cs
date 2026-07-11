using Microsoft.Extensions.Options;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;
using Stripe;
using Stripe.Checkout;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.SignalR;
using MovieWeb.Hubs;

namespace MovieWeb.Services
{
    public class StripeService : IStripeService
    {
        private readonly MovieWebDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _secretKey;
        private readonly string _webhookSecret;
        private readonly string _currency;
        private readonly decimal _exchangeRate;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<StripeService> _logger;

        public StripeService(
            MovieWebDbContext context,
            IConfiguration configuration,
            ILogger<StripeService> logger,
            INotificationService notificationService,
            IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _notificationService = notificationService;        // ← THÊM
            _hubContext = hubContext;
            _secretKey = configuration["StripeSettings:SecretKey"] ?? throw new ArgumentNullException("Stripe SecretKey not configured");
            _webhookSecret = configuration["StripeSettings:WebhookSecret"] ?? "";
            _currency = (configuration["StripeSettings:Currency"] ?? "vnd").ToLower();
            _exchangeRate = decimal.Parse(configuration["SubscriptionSettings:ExchangeRateVNDtoUSD"] ?? "25000");
            StripeConfiguration.ApiKey = _secretKey;
        }


        // ===== CUSTOMER MANAGEMENT =====

        public async Task<string> CreateOrGetCustomerAsync(User user)
        {
            if (!string.IsNullOrEmpty(user.StripeCustomerId))
            {
                try
                {
                    var customerService = new CustomerService();
                    await customerService.GetAsync(user.StripeCustomerId);
                    return user.StripeCustomerId;
                }
                catch (StripeException)
                {
                    // Customer không tồn tại, tạo mới
                }
            }

            // Tạo customer mới
            var options = new CustomerCreateOptions
            {
                Email = user.Email,
                Name = user.FullName,
                Metadata = new Dictionary<string, string>
                {
                    { "user_id", user.Id.ToString() },
                    { "username", user.UserName ?? "" }
                }
            };

            var service = new CustomerService();
            var customer = await service.CreateAsync(options);

            // Lưu customer ID vào database
            user.StripeCustomerId = customer.Id;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return customer.Id;
        }

        public async Task<bool> UpdateCustomerAsync(string customerId, User user)
        {
            try
            {
                var options = new CustomerUpdateOptions
                {
                    Email = user.Email,
                    Name = user.FullName
                };

                var service = new CustomerService();
                await service.UpdateAsync(customerId, options);
                return true;
            }
            catch (StripeException)
            {
                return false;
            }
        }

        // ===== PRODUCT & PRICE MANAGEMENT =====

        public async Task<bool> SyncSubscriptionPlansToStripeAsync()
        {
            var plans = await _context.SubscriptionPlans
                .Where(p => p.IsActive && string.IsNullOrEmpty(p.StripeProductId))
                .ToListAsync();

            foreach (var plan in plans)
            {
                try
                {
                    var productId = await CreateProductAsync(plan);
                    var priceId = await CreatePriceAsync(productId, plan);

                    plan.StripeProductId = productId;
                    plan.StripePriceId = priceId;
                    plan.UpdatedAt = DateTime.Now;
                }
                catch (StripeException ex)
                {
                    Console.WriteLine($"Error syncing plan {plan.PlanName}: {ex.Message}");
                    continue;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string> CreateProductAsync(SubscriptionPlan plan)
        {
            var options = new ProductCreateOptions
            {
                Name = plan.DisplayName,
                Description = plan.Description,
                Metadata = new Dictionary<string, string>
                {
                    { "plan_id", plan.PlanId.ToString() },
                    { "plan_type", plan.PlanType },
                    { "duration_months", plan.DurationMonths.ToString() }
                }
            };

            var service = new ProductService();
            var product = await service.CreateAsync(options);
            return product.Id;
        }

        public async Task<string> CreatePriceAsync(string productId, SubscriptionPlan plan)
        {
            long unitAmount = _currency == "vnd" 
                ? (long)plan.PriceVND 
                : (long)(plan.PriceUSD * 100);

            var options = new PriceCreateOptions
            {
                Product = productId,
                UnitAmount = unitAmount,
                Currency = _currency,
                Recurring = new PriceRecurringOptions
                {
                    Interval = "month",
                    IntervalCount = plan.DurationMonths
                },
                Metadata = new Dictionary<string, string>
                {
                    { "plan_id", plan.PlanId.ToString() },
                    { "price_vnd", plan.PriceVND.ToString() }
                }
            };

            var service = new PriceService();
            var price = await service.CreateAsync(options);
            return price.Id;
        }

        // ===== CHECKOUT SESSION =====
        // Trong file service xử lý Stripe của bạn (ví dụ: Services/StripeService.cs)

        public async Task<bool> ActivateSubscriptionFromSession(string sessionId)
        {
            var sessionService = new Stripe.Checkout.SessionService();
            var session = await sessionService.GetAsync(sessionId);

            // 1. Kiểm tra xem session có hợp lệ và đã thanh toán chưa
            if (session == null || session.PaymentStatus != "paid")
            {
                _logger.LogWarning("Session không hợp lệ hoặc chưa thanh toán: {SessionId}", sessionId);
                return false;
            }

            // 2. Lấy UserId và PlanId từ Metadata mà bạn đã đính kèm lúc tạo session
            // LƯU Ý: Bạn phải đảm bảo đã thêm các metadata này lúc gọi CreateCheckoutSessionAsync
            session.Metadata.TryGetValue("UserId", out var userIdStr);
            session.Metadata.TryGetValue("PlanId", out var planIdStr);

            if (!int.TryParse(userIdStr, out var userId) || !int.TryParse(planIdStr, out var planId))
            {
                _logger.LogError("Không thể lấy UserId hoặc PlanId từ metadata của session: {SessionId}", sessionId);
                return false;
            }

            // 3. Lấy thông tin plan để biết loại gói là gì (premium/student)
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
            {
                _logger.LogError("Không tìm thấy Plan với ID: {PlanId}", planId);
                return false;
            }

            // 4. Tìm người dùng và cập nhật database (đoạn code này giống hệt trong webhook)
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                // **Đây là đoạn code quan trọng nhất**
                user.SubscriptionType = plan.PlanType.ToLower(); // "premium" hoặc "student"
                user.SubscriptionEndDate = DateTime.Now.AddMonths(plan.DurationMonths);
                user.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Kích hoạt gói {PlanType} cho User ID {UserId} từ session success.", plan.PlanType, userId);
                return true;
            }

            _logger.LogWarning("Không tìm thấy User ID {UserId} để kích hoạt gói.", userId);
            return false;
        }
        // Tìm hàm này trong file StripeService.cs và THAY THẾ TOÀN BỘ
        public async Task<Session> CreateCheckoutSessionAsync(
            User user,
            SubscriptionPlan plan,
            string successUrl,
            string cancelUrl)
        {
            // =======================================================================
            // ✅ BƯỚC 1: THÊM ĐOẠN KIỂM TRA "ÔNG BẢO VỆ" VÀO ĐÂY
            // =======================================================================
            var currentActiveSub = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan) // Thêm Include để lấy tên gói
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.EndDate > DateTime.Now && s.Status == "active");

            if (currentActiveSub != null)
            {
                // Nếu tìm thấy một gói đang active, ném ra lỗi để Controller bắt được.
                // Controller sẽ trả về lỗi 400 và front-end sẽ hiển thị popup.
                var planName = currentActiveSub.SubscriptionPlan?.DisplayName ?? "hiện tại";
                var daysRemaining = (currentActiveSub.EndDate - DateTime.Now).Days;

                // Dùng InvalidOperationException là chuẩn nhất cho trường hợp này
                throw new InvalidOperationException($"Bạn đang là thành viên {planName}. Thời hạn còn {daysRemaining} ngày. Bạn cần phải hủy gói hiện tại trước khi mua gói mới.");
            }
            // =======================================================================
            // KẾT THÚC ĐOẠN CODE MỚI
            // =======================================================================


            // Nếu không có gói nào active, code sẽ tiếp tục chạy như cũ
            var customerId = await CreateOrGetCustomerAsync(user);

            // Tạo transaction trước khi tạo session
            var transaction = new Transaction
            {
                UserId = user.Id,
                PlanId = plan.PlanId,
                TransactionCode = $"TXN_{DateTime.Now:yyyyMMddHHmmss}_{user.Id}",
                Amount = _currency == "vnd" ? plan.PriceVND : plan.PriceUSD,
                AmountVND = plan.PriceVND,
                Currency = _currency.ToUpper(),
                PaymentMethod = "stripe",
                Status = "pending",
                Description = $"Thanh toán gói {plan.DisplayName}",
                CreatedAt = DateTime.Now
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            var options = new SessionCreateOptions
            {
                Customer = customerId,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                Price = plan.StripePriceId,
                Quantity = 1
            }
        },
                Mode = "subscription",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
        {
            { "user_id", user.Id.ToString() },
            { "plan_id", plan.PlanId.ToString() },
            { "plan_type", plan.PlanType },
            { "actual_months", plan.ActualMonths.ToString() },
            { "transaction_code", transaction.TransactionCode } // Thêm transaction code vào metadata
        },
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
            {
                { "user_id", user.Id.ToString() },
                { "plan_id", plan.PlanId.ToString() }
            }
                }
            };

            var service = new SessionService();
            return await service.CreateAsync(options);
        }
        // ===== SUBSCRIPTION MANAGEMENT =====

        public async Task<bool> CancelSubscriptionAsync(string stripeSubscriptionId, bool immediately = false)
        {
            try
            {
                var service = new Stripe.SubscriptionService();

                if (immediately)
                {
                    await service.CancelAsync(stripeSubscriptionId);
                }
                else
                {
                    var options = new SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = true
                    };
                    await service.UpdateAsync(stripeSubscriptionId, options);
                }

                return true;
            }
            catch (StripeException)
            {
                return false;
            }
        }

        public async Task<bool> ReactivateSubscriptionAsync(string stripeSubscriptionId)
        {
            try
            {
                var options = new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = false
                };

                var service = new Stripe.SubscriptionService();
                await service.UpdateAsync(stripeSubscriptionId, options);
                return true;
            }
            catch (StripeException)
            {
                return false;
            }
        }

        // ===== WEBHOOK HANDLING =====

        public async Task HandleWebhookAsync(string json, string stripeSignature)
        {
            try
            {
                _logger.LogInformation("Starting webhook processing...");

                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    stripeSignature,
                    _webhookSecret
                );

                _logger.LogInformation($"Received webhook type: {stripeEvent.Type}");

                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        var session = stripeEvent.Data.Object as Session;
                        if (session != null)
                        {
                            _logger.LogInformation($"Processing checkout session: {session.Id}");
                            await HandleCheckoutCompletedAsync(session);
                        }
                        break;

                    case "customer.subscription.updated":
                        var subUpdated = stripeEvent.Data.Object as Stripe.Subscription;
                        if (subUpdated != null)
                        {
                            _logger.LogInformation($"Processing subscription update: {subUpdated.Id}");
                            await HandleSubscriptionUpdatedAsync(subUpdated);
                        }
                        break;

                    case "customer.subscription.deleted":
                        var subDeleted = stripeEvent.Data.Object as Stripe.Subscription;
                        if (subDeleted != null)
                        {
                            _logger.LogInformation($"Processing subscription deletion: {subDeleted.Id}");
                            await HandleSubscriptionDeletedAsync(subDeleted);
                        }
                        break;

                    case "invoice.payment_succeeded":
                        var jsonObjSuccess = JObject.Parse(json);
                        var subIdSuccess = jsonObjSuccess["data"]?["object"]?["subscription"]?.ToString();
                        var paymentIntentSuccess = jsonObjSuccess["data"]?["object"]?["payment_intent"]?.ToString();
                        var chargeSuccess = jsonObjSuccess["data"]?["object"]?["charge"]?.ToString();

                        var invoiceSuccess = stripeEvent.Data.Object as Invoice;
                        if (invoiceSuccess != null && !string.IsNullOrEmpty(subIdSuccess))
                        {
                            _logger.LogInformation($"Processing payment success for subscription: {subIdSuccess}");
                            await HandlePaymentSucceededAsync(invoiceSuccess, subIdSuccess, paymentIntentSuccess, chargeSuccess);
                        }
                        break;

                    case "invoice.payment_failed":
                        var jsonObjFailed = JObject.Parse(json);
                        var subIdFailed = jsonObjFailed["data"]?["object"]?["subscription"]?.ToString();

                        var invoiceFailed = stripeEvent.Data.Object as Invoice;
                        if (invoiceFailed != null && !string.IsNullOrEmpty(subIdFailed))
                        {
                            _logger.LogInformation($"Processing payment failure for subscription: {subIdFailed}");
                            await HandlePaymentFailedAsync(invoiceFailed, subIdFailed);
                        }
                        break;
                    case "invoice.paid": // ✅ Thêm dòng này
                    case "invoice_payment.paid": // ✅ Và dòng này

                    default:
                        _logger.LogInformation($"Unhandled event type: {stripeEvent.Type}");
                        break;
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook error");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General webhook error");
                throw;
            }
        }
        // THAY THẾ METHOD HandleCheckoutCompletedAsync
        // Tìm method này trong StripeService.cs và thay thế toàn bộ

        public async Task HandleCheckoutCompletedAsync(Session session)
        {
            try
            {
                _logger.LogInformation($"Processing checkout session: {session.Id}");

                if (!session.Metadata.TryGetValue("transaction_code", out var transactionCode))
                {
                    _logger.LogError("No transaction_code in metadata");
                    return;
                }

                _logger.LogInformation($"Looking for transaction: {transactionCode}");

                var existingTransaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode);

                if (existingTransaction == null)
                {
                    _logger.LogError($"Transaction not found: {transactionCode}");
                    return;
                }

                if (existingTransaction.Status == "completed")
                {
                    _logger.LogInformation($"Transaction {transactionCode} is already completed. Skipping duplicate fulfillment.");
                    return;
                }

                var userId = int.Parse(session.Metadata["user_id"]);
                var planId = int.Parse(session.Metadata["plan_id"]);
                var actualMonths = int.Parse(session.Metadata["actual_months"]);

                var plan = await _context.SubscriptionPlans.FindAsync(planId);
                if (plan == null)
                {
                    _logger.LogError($"Plan not found: {planId}");
                    return;
                }

                // ✅ LOGIC: Kiểm tra gói cũ đã hủy nhưng còn hạn
                var oldCancelledSub = await _context.UserSubscriptions
                    .Where(s => s.UserId == userId
                        && s.Status == "cancelled"
                        && s.EndDate > DateTime.Now)
                    .OrderByDescending(s => s.EndDate)
                    .FirstOrDefaultAsync();

                int bonusDays = 0;

                if (oldCancelledSub != null)
                {
                    // Tính số ngày còn lại của gói cũ
                    bonusDays = (int)(oldCancelledSub.EndDate - DateTime.Now).TotalDays;

                    // Đánh dấu gói cũ là stacked
                    oldCancelledSub.Status = "stacked";
                    oldCancelledSub.UpdatedAt = DateTime.Now;
                    _context.UserSubscriptions.Update(oldCancelledSub);

                    _logger.LogInformation($"Found cancelled subscription with {bonusDays} days remaining");
                }

                // Tính tổng số ngày = ngày mới + ngày cũ còn lại
                var totalDays = (actualMonths * 30) + bonusDays;

                // Tạo subscription mới
                var subscription = new UserSubscription
                {
                    UserId = userId,
                    PlanId = planId,
                    StripeSubscriptionId = session.SubscriptionId,
                    StripeCustomerId = session.CustomerId,
                    Status = "active",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(totalDays),
                    NextBillingDate = DateTime.Now.AddMonths(actualMonths),
                    AutoRenew = true,
                    BonusDaysFromPreviousPackage = bonusDays, // 🆕 THÊM DÒNG NÀY!
                    CreatedAt = DateTime.Now
                };

                _context.UserSubscriptions.Add(subscription);
                await _context.SaveChangesAsync();

                // Liên kết transaction với subscription
                existingTransaction.Status = "completed";
                existingTransaction.CompletedAt = DateTime.Now;
                existingTransaction.UpdatedAt = DateTime.Now;
                existingTransaction.StripePaymentIntentId = session.PaymentIntentId;
                existingTransaction.StripeChargeId = session.Id;
                existingTransaction.IpAddress = session.CustomerDetails?.Address?.Country;
                existingTransaction.SubscriptionId = subscription.SubscriptionId;
                _context.Transactions.Update(existingTransaction);

                // Update user
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.SubscriptionType = plan.PlanType.ToLower();
                    user.SubscriptionStartDate = DateTime.Now;
                    user.SubscriptionEndDate = DateTime.Now.AddDays(totalDays);
                    user.StripeCustomerId = session.CustomerId;
                    user.UpdatedAt = DateTime.Now;
                    _context.Users.Update(user);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    $"✅ Checkout completed: User {userId}, Plan {plan.DisplayName}, " +
                    $"Total days: {totalDays} (New: {actualMonths * 30}, Bonus: {bonusDays})"
                );
                try
                {
                    // 1. Lưu vào DB (không cần Hangfire vì đã trong webhook async rồi)
                    await _notificationService.CreatePaymentSuccessNotificationAsync(
                        userId,
                        plan.PlanType,
                        DateTime.Now.AddDays(totalDays),
                        plan.PriceVND
                    );

                    // 2. Gửi SignalR real-time
                    var notificationDto = new
                    {
                        NotificationId = 0,
                        Title = "✅ Thanh toán thành công!",
                        Content = $"Bạn đã nâng cấp lên gói {plan.DisplayName} thành công. " +
                                  $"Có hiệu lực đến {DateTime.Now.AddDays(totalDays):dd/MM/yyyy}. " +
                                  $"Cảm ơn bạn đã ủng hộ MoonPhim!",
                        Type = "PaymentSuccess",
                        Url = "/user/profile",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _hubContext.Clients
                        .User(userId.ToString())
                        .SendAsync("ReceiveNotification", notificationDto);

                    _logger.LogInformation("✅ Sent payment success notification to User {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error sending payment notification to User {UserId}", userId);
                    // Không throw để không làm fail webhook
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing checkout session");
                throw;
            }
        }
        public async Task HandleSubscriptionUpdatedAsync(Stripe.Subscription subscription)
        {
            var userSub = await _context.UserSubscriptions
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id);

            if (userSub == null)
            {
                _logger.LogWarning("Webhook received for unknown StripeSubscriptionId: {StripeSubscriptionId}", subscription.Id);
                return;
            }

            // ✅ LOGIC MỚI, THÔNG MINH HƠN ĐỂ XỬ LÝ HỦY GÓI
            if (subscription.Status == "active" && subscription.CancelAtPeriodEnd == true)
            {
                // Kịch bản 1: Đây chính là trường hợp của bạn.
                // Gói vẫn 'active' bên Stripe nhưng đã được yêu cầu hủy vào cuối kỳ.
                // Chúng ta KHÔNG được ghi đè status 'cancelled' trong DB của mình.
                // Thay vào đó, ta chỉ cần đảm bảo các thông tin khác khớp.
                _logger.LogInformation("Webhook: Subscription {SubscriptionId} is active but set to cancel at period end. Maintaining 'cancelled' status locally.", subscription.Id);

                // Cập nhật lại ngày hủy cho chắc chắn, lấy từ Stripe nếu có
                userSub.CancelledAt = subscription.CanceledAt ?? DateTime.UtcNow;
                userSub.AutoRenew = false;
                // Quan trọng: KHÔNG ĐỤNG ĐẾN userSub.Status
            }
            else if (subscription.Status == "canceled")
            {
                // Kịch bản 2: Gói đã thực sự bị hủy/hết hạn trên Stripe.
                _logger.LogInformation("Webhook: Subscription {SubscriptionId} is now officially canceled. Updating status.", subscription.Id);
                userSub.Status = "cancelled"; // Hoặc "expired" tùy logic của bạn
                userSub.EndDate = subscription.EndedAt ?? userSub.EndDate; // Cập nhật ngày hết hạn thực tế
            }
            else
            {
                // Kịch bản 3: Các cập nhật khác (ví dụ: past_due, unpaid...).
                // Với các trường hợp này, ta đồng bộ status như bình thường.
                _logger.LogInformation("Webhook: Syncing status for subscription {SubscriptionId} to '{Status}'", subscription.Id, subscription.Status);
                userSub.Status = subscription.Status;
            }

            // Luôn cập nhật các thông tin chung khác
            userSub.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }
        public async Task HandleSubscriptionDeletedAsync(Stripe.Subscription subscription)
        {
            var userSub = await _context.UserSubscriptions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id);

            if (userSub == null) return;

            userSub.Status = "cancelled";
            userSub.CancelledAt = DateTime.Now;
            userSub.UpdatedAt = DateTime.Now;

            if (userSub.User != null)
            {
                userSub.User.SubscriptionType = "free";
                userSub.User.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        public async Task HandlePaymentSucceededAsync(Invoice invoice, string subscriptionId, string? paymentIntentId, string? chargeId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
            {
                _logger.LogWarning("HandlePaymentSucceededAsync called without a subscriptionId for invoice: {InvoiceId}", invoice.Id);
                return;
            }

            // Kiểm tra xem transaction cho hóa đơn này đã tồn tại chưa
            if (!string.IsNullOrEmpty(paymentIntentId))
            {
                var existingTransactionCheck = await _context.Transactions.AnyAsync(t => t.StripePaymentIntentId == paymentIntentId);
                if (existingTransactionCheck)
                {
                    _logger.LogInformation("Transaction for payment intent {PaymentIntentId} already exists.", paymentIntentId);
                    return; // Đã xử lý rồi, không làm gì nữa
                }
            }

            var userSub = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

            if (userSub == null || userSub.SubscriptionPlan == null)
            {
                _logger.LogWarning("Could not find a matching user subscription or plan for Stripe subscription: {SubscriptionId}", subscriptionId);
                return;
            }

            // Nhiệm vụ chính: Tạo một transaction mới cho việc gia hạn.
            // Chúng ta không còn cập nhật ngày hết hạn ở đây nữa.
            var transaction = new Transaction
            {
                UserId = userSub.UserId,
                PlanId = userSub.PlanId,
                SubscriptionId = userSub.SubscriptionId,
                TransactionCode = $"TXN_{DateTime.Now:yyyyMMddHHmmss}_{userSub.UserId}",
                StripePaymentIntentId = paymentIntentId,
                StripeChargeId = chargeId,
                Amount = invoice.AmountPaid / 100m,
                AmountVND = userSub.SubscriptionPlan.PriceVND,
                Currency = invoice.Currency?.ToUpper() ?? "USD",
                PaymentMethod = "stripe",
                Status = "completed",
                Description = $"Gia hạn gói {userSub.SubscriptionPlan.DisplayName}",
                CompletedAt = DateTime.Now,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully created renewal transaction for subscription {SubscriptionId}.", subscriptionId);
        }

        public async Task HandlePaymentFailedAsync(Invoice invoice, string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId)) return;

            var userSub = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

            if (userSub == null || userSub.SubscriptionPlan == null) return;

            // Tạo transaction failed
            var transaction = new Transaction
            {
                UserId = userSub.UserId,
                PlanId = userSub.PlanId,
                SubscriptionId = userSub.SubscriptionId,
                TransactionCode = $"TXN_{DateTime.Now:yyyyMMddHHmmss}_{userSub.UserId}",
                Amount = invoice.AmountDue / 100m,
                AmountVND = userSub.SubscriptionPlan.PriceVND,
                Currency = invoice.Currency?.ToUpper() ?? "USD",
                PaymentMethod = "stripe",
                Status = "failed",
                Description = $"Thanh toán thất bại - {userSub.SubscriptionPlan.DisplayName}",
                FailureReason = invoice.LastFinalizationError?.Message ?? "Payment failed",
                UpdatedAt = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            _context.Transactions.Add(transaction);

            // Update subscription status
            userSub.Status = "payment_failed";
            userSub.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        // ===== UTILITY =====

        public decimal ConvertVNDToUSD(decimal amountVND)
        {
            return Math.Round(amountVND / _exchangeRate, 2);
        }

        public decimal ConvertUSDToVND(decimal amountUSD)
        {
            return Math.Round(amountUSD * _exchangeRate, 0);
        }
    }
}