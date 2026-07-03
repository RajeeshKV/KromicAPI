using System.ComponentModel.DataAnnotations;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kromic.Infrastructure.Services;

public sealed class GoldRateEmailSubscriptionService(KromicDbContext dbContext) : IGoldRateEmailSubscriptionService
{
    private static readonly TimeSpan CaptureWindow = TimeSpan.FromMinutes(1);
    private static readonly EmailAddressAttribute EmailValidator = new();

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

        return new EmailAlertSubscriptionResult(EmailAlertSubscriptionStatus.Subscribed, email);
    }

    public async Task<IReadOnlyList<GoldRateEmailSubscriber>> GetActiveSubscribersAsync(CancellationToken cancellationToken)
    {
        return await dbContext.GoldRateEmailSubscriptions
            .AsNoTracking()
            .Where(x => x.IsActive && x.Email != null && x.Email != "")
            .Select(x => new GoldRateEmailSubscriber(x.Email!, x.ChatId, x.UnsubscribeToken))
            .ToListAsync(cancellationToken);
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
        return true;
    }
}
