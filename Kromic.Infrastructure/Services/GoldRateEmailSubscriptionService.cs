using System.ComponentModel.DataAnnotations;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Kromic.Infrastructure.Services;

public sealed class GoldRateEmailSubscriptionService(
    KromicDbContext dbContext,
    IMemoryCache memoryCache,
    ILogger<GoldRateEmailSubscriptionService> logger) : IGoldRateEmailSubscriptionService
{
    private const string ActiveSubscribersCacheKey = "goldrate:emailsubscribers";
    private static readonly TimeSpan CaptureWindow = TimeSpan.FromMinutes(1);
    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly IMemoryCache _memoryCache = memoryCache;

    private static string ByChatIdKey(string chatId) => $"goldrate:emailsubscription:{chatId}";

    public async Task StartEmailCaptureAsync(string chatId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = await dbContext.GoldRateEmailSubscriptions
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (subscription is null)
        {
            subscription = new GoldRateEmailSubscription
            {
                ChatId = chatId,
                CreatedAt = now
            };
            dbContext.GoldRateEmailSubscriptions.Add(subscription);
        }

        subscription.PendingRequestedAt = now;
        subscription.PendingExpiresAt = now.Add(CaptureWindow);
        subscription.UpdatedAt = now;

        if (string.IsNullOrWhiteSpace(subscription.UnsubscribeToken))
        {
            subscription.UnsubscribeToken = Guid.NewGuid().ToString("N");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Invalidate(chatId);
    }

    public async Task<EmailAlertSubscriptionResult> TryCompleteEmailCaptureAsync(
        string chatId,
        string messageText,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.GoldRateEmailSubscriptions
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (subscription?.PendingExpiresAt is null)
        {
            return new EmailAlertSubscriptionResult(EmailAlertSubscriptionStatus.NoPendingRequest);
        }

        var now = DateTimeOffset.UtcNow;
        if (subscription.PendingExpiresAt < now)
        {
            subscription.PendingRequestedAt = null;
            subscription.PendingExpiresAt = null;
            subscription.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            Invalidate(chatId);

            return new EmailAlertSubscriptionResult(EmailAlertSubscriptionStatus.Expired);
        }

        var email = messageText.Trim();
        if (!EmailValidator.IsValid(email))
        {
            return new EmailAlertSubscriptionResult(EmailAlertSubscriptionStatus.InvalidEmail);
        }

        subscription.Email = email;
        subscription.IsActive = true;
        subscription.PendingRequestedAt = null;
        subscription.PendingExpiresAt = null;
        subscription.SubscribedAt = now;
        subscription.UnsubscribedAt = null;
        subscription.UpdatedAt = now;

        if (string.IsNullOrWhiteSpace(subscription.UnsubscribeToken))
        {
            subscription.UnsubscribeToken = Guid.NewGuid().ToString("N");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Invalidate(chatId);

        return new EmailAlertSubscriptionResult(EmailAlertSubscriptionStatus.Subscribed, email);
    }

    public async Task<IReadOnlyList<GoldRateEmailSubscriber>> GetActiveSubscribersAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue<IReadOnlyList<GoldRateEmailSubscriber>>(ActiveSubscribersCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var subscribers = await dbContext.GoldRateEmailSubscriptions
            .AsNoTracking()
            .Where(x => x.IsActive && x.Email != null && x.Email != "")
            .Select(x => new GoldRateEmailSubscriber(x.Email!, x.ChatId, x.UnsubscribeToken))
            .ToListAsync(cancellationToken);

        _memoryCache.Set(ActiveSubscribersCacheKey, (IReadOnlyList<GoldRateEmailSubscriber>)subscribers);
        logger.LogDebug("Resolved {SubscriberCount} active gold/silver email subscribers (cached until a subscription changes).", subscribers.Count);

        return subscribers;
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var subscription = await dbContext.GoldRateEmailSubscriptions
            .FirstOrDefaultAsync(x => x.UnsubscribeToken == token && x.IsActive, cancellationToken);

        if (subscription is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        subscription.IsActive = false;
        subscription.UnsubscribedAt = now;
        subscription.PendingRequestedAt = null;
        subscription.PendingExpiresAt = null;
        subscription.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        Invalidate(subscription.ChatId);
        return true;
    }

    public async Task<GoldRateEmailSubscription?> GetByChatIdAsync(string chatId, CancellationToken cancellationToken)
    {
        var key = ByChatIdKey(chatId);
        if (_memoryCache.TryGetValue<GoldRateEmailSubscription?>(key, out var cached))
        {
            return cached;
        }

        var entity = await dbContext.GoldRateEmailSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        _memoryCache.Set(key, entity);
        return entity;
    }

    private void Invalidate(string chatId)
    {
        _memoryCache.Remove(ActiveSubscribersCacheKey);
        _memoryCache.Remove(ByChatIdKey(chatId));
    }
}
