namespace Kromic.Application.DTOs;

public enum EmailAlertSubscriptionStatus
{
    NoPendingRequest,
    Subscribed,
    InvalidEmail,
    Expired
}

public sealed record EmailAlertSubscriptionResult(
    EmailAlertSubscriptionStatus Status,
    string? Email = null);

public sealed record GoldRateEmailSubscriber(
    string Email,
    string ChatId,
    string UnsubscribeToken);
