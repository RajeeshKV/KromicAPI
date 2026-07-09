using Kromic.Application.DTOs;
using Kromic.Domain.Entities;

namespace Kromic.Application.Interfaces;

public interface ITransactionalEmailService
{
    Task<string?> SendContactNotificationAsync(ContactSubmission submission, CancellationToken cancellationToken);
    Task<string?> SendContactResponseAsync(ContactSubmission submission, string responseText, CancellationToken cancellationToken);
    Task<string?> SendCustomEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string? heading,
        string body,
        string? callToActionText,
        string? callToActionUrl,
        CancellationToken cancellationToken);

    Task<string?> SendWeeklySummaryEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string? heading,
        string body,
        string? callToActionText,
        string? callToActionUrl,
        CancellationToken cancellationToken);

    Task<string?> SendGoldRateEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string heading,
        string rate1g,
        string rate8g,
        string change,
        string change8g,
        string changeClass,
        string fetchedAt,
        CancellationToken cancellationToken);

    Task<string?> SendWeeklySummaryEmailStructuredAsync(
        string toEmail,
        string toName,
        string subject,
        string heading,
        string startDate,
        string endDate,
        string averageRate,
        string highestRate,
        string highestDate,
        string lowestRate,
        string lowestDate,
        string trend,
        string trendAmount,
        string trendClass,
        string currentRate,
        CancellationToken cancellationToken);

    Task<string?> SendTelegramFeedbackAsync(
        TelegramFeedbackNotification feedback,
        CancellationToken cancellationToken);

    Task<string?> SendAdminNotificationAsync(
        string subject,
        string heading,
        string body,
        CancellationToken cancellationToken);
}
