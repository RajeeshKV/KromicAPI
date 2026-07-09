using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface IGoldRateEmailSubscriptionService
{
    Task StartEmailCaptureAsync(string chatId, CancellationToken cancellationToken);
    Task<EmailAlertSubscriptionResult> TryCompleteEmailCaptureAsync(string chatId, string messageText, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoldRateEmailSubscriber>> GetActiveSubscribersAsync(CancellationToken cancellationToken);
    Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken);
    Task<Domain.Entities.GoldRateEmailSubscription?> GetByChatIdAsync(string chatId, CancellationToken cancellationToken);
}
