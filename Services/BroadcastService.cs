using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Hubs;
using MovieWeb.Jobs;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class BroadcastService : IBroadcastService
    {
        private readonly MovieWebDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BroadcastService> _logger;

        public BroadcastService(
            MovieWebDbContext context,
            IBackgroundJobClient backgroundJobClient,
            IHubContext<NotificationHub> hubContext,
            IWebHostEnvironment env,
            ILogger<BroadcastService> logger)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
            _hubContext = hubContext;
            _env = env;
            _logger = logger;
        }

        public async Task<AdminBroadcast> CreateAndEnqueueBroadcastAsync(
            string title,
            string content,
            string? styleJson,
            string? url,
            string targetType,
            List<int>? targetUserIds,
            bool sendPush,
            int? createdBy)
        {
            targetType = targetType == "selected" ? "selected" : "all";

            var broadcast = new AdminBroadcast
            {
                Title = title,
                Content = content,
                StyleJson = styleJson,
                Url = url,
                TargetType = targetType,
                RecipientCount = 0,
                SendPush = sendPush,
                Status = "queued",
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.AdminBroadcasts.Add(broadcast);
            await _context.SaveChangesAsync();

            // Enqueue Hangfire background job
            _backgroundJobClient.Enqueue<BroadcastJob>(j => j.Execute(broadcast.Id, targetUserIds));
            _logger.LogInformation("Enqueued BroadcastJob for BroadcastId={Id}, TargetType={TargetType}", broadcast.Id, targetType);

            return broadcast;
        }

        public async Task ExecuteBroadcastJobAsync(int broadcastId, List<int>? targetUserIds = null)
        {
            var broadcast = await _context.AdminBroadcasts.FindAsync(broadcastId);
            if (broadcast == null)
            {
                _logger.LogWarning("Broadcast {Id} not found.", broadcastId);
                return;
            }

            try
            {
                broadcast.Status = "sending";
                await _context.SaveChangesAsync();

                List<int> userIds;
                if (broadcast.TargetType == "selected" && targetUserIds != null && targetUserIds.Any())
                {
                    userIds = targetUserIds.Distinct().ToList();
                }
                else
                {
                    userIds = await _context.Users
                        .Where(u => u.IsActive != false)
                        .Select(u => u.Id)
                        .ToListAsync();
                }

                const int batchSize = 1000;
                var totalSent = 0;

                for (int i = 0; i < userIds.Count; i += batchSize)
                {
                    var batch = userIds.Skip(i).Take(batchSize).ToList();
                    var notifications = batch.Select(uid => new Notification
                    {
                        UserId = uid,
                        BroadcastId = broadcast.Id,
                        Title = broadcast.Title,
                        Content = broadcast.Content,
                        Url = broadcast.Url,
                        Type = "SystemBroadcast",
                        IsRead = false,
                        CreatedAt = DateTime.Now // ✅ ĐÚNG như NotificationService đang dùng, KHÔNG dùng UtcNow
                    }).ToList();

                    _context.Notifications.AddRange(notifications);
                    await _context.SaveChangesAsync();

                    totalSent += notifications.Count;

                    // SignalR realtime per batch to user groups
                    // Gửi tới group "User_{id}" của từng user nhận
                    var groupNames = batch.Select(uid => $"User_{uid}").ToList();
                    var payload = new
                    {
                        notificationId = notifications.First().NotificationId,
                        title = broadcast.Title,
                        content = broadcast.Content,
                        type = "SystemBroadcast",
                        url = broadcast.Url,
                        isRead = false,
                        createdAt = DateTime.Now,
                        broadcastId = broadcast.Id,
                        style = broadcast.StyleJson
                    };

                    await _hubContext.Clients.Groups(groupNames).SendAsync("ReceiveNotification", payload);
                }

                broadcast.RecipientCount = totalSent;
                broadcast.Status = "done";
                broadcast.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Completed Broadcast {Id}: Sent to {Count} users", broadcast.Id, totalSent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed executing Broadcast {Id}", broadcast.Id);
                broadcast.Status = "failed";
                await _context.SaveChangesAsync();
                // Do not rethrow to avoid duplicate broadcasts
            }
        }

        public async Task<bool> RevokeBroadcastAsync(int broadcastId)
        {
            var broadcast = await _context.AdminBroadcasts.FindAsync(broadcastId);
            if (broadcast == null) return false;

            var notifications = await _context.Notifications
                .Where(n => n.BroadcastId == broadcastId)
                .ToListAsync();

            _context.Notifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();

            // Realtime notice of revocation
            await _hubContext.Clients.All.SendAsync("RevokeNotification", broadcastId);

            _logger.LogInformation("Revoked broadcast {Id}, removed {Count} notifications", broadcastId, notifications.Count);
            return true;
        }
    }
}
