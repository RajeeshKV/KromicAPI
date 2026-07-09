using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class BrevoTransactionalEmailService(HttpClient httpClient, IOptions<BrevoOptions> options) : ITransactionalEmailService
{
    private readonly BrevoOptions _options = options.Value;

    public Task<string?> SendContactNotificationAsync(ContactSubmission submission, CancellationToken cancellationToken)
    {
        EnsureConfigured(_options.ContactNotificationTemplateId, requireOwnerEmail: true);

        var request = CreateTemplateRequest(
            _options.OwnerEmail,
            _options.OwnerName,
            _options.ContactNotificationTemplateId,
            new
            {
                contactId = submission.Id,
                name = submission.Name,
                email = submission.Email,
                projectType = submission.ProjectType,
                expectedTimeline = submission.ExpectedTimeline,
                description = submission.Description,
                submittedAt = submission.CreatedAt
            },
            replyToEmail: submission.Email,
            replyToName: submission.Name);

        return SendAsync(request, cancellationToken);
    }

    public Task<string?> SendContactResponseAsync(ContactSubmission submission, string responseText, CancellationToken cancellationToken)
    {
        EnsureConfigured(_options.ContactResponseTemplateId);

        var request = CreateTemplateRequest(
            submission.Email,
            submission.Name,
            _options.ContactResponseTemplateId,
            new
            {
                contactId = submission.Id,
                name = submission.Name,
                email = submission.Email,
                projectType = submission.ProjectType,
                expectedTimeline = submission.ExpectedTimeline,
                description = submission.Description,
                responseText,
                respondedAt = DateTimeOffset.UtcNow
            });

        return SendAsync(request, cancellationToken);
    }

    public Task<string?> SendCustomEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string? heading,
        string body,
        string? callToActionText,
        string? callToActionUrl,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(_options.CustomEmailTemplateId);

        var request = CreateTemplateRequest(
            toEmail,
            toName,
            _options.CustomEmailTemplateId,
            new
            {
                name = toName,
                email = toEmail,
                subject,
                heading,
                body,
                callToActionText,
                callToActionUrl,
                sentAt = DateTimeOffset.UtcNow
            },
            subject: subject);

        return SendAsync(request, cancellationToken);
    }

    public Task<string?> SendWeeklySummaryEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string? heading,
        string body,
        string? callToActionText,
        string? callToActionUrl,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(_options.WeeklySummaryEmailTemplateId);

        var request = CreateTemplateRequest(
            toEmail,
            toName,
            _options.WeeklySummaryEmailTemplateId,
            new
            {
                name = toName,
                email = toEmail,
                subject,
                heading,
                body,
                callToActionText,
                callToActionUrl,
                sentAt = DateTimeOffset.UtcNow
            },
            subject: subject);

        return SendAsync(request, cancellationToken);
    }


    public Task<string?> SendTelegramFeedbackAsync(
        TelegramFeedbackNotification feedback,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(_options.FeedbackTemplateId);

        var displayName = string.Join(" ", new[] { feedback.FirstName, feedback.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)))
            .Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = string.IsNullOrWhiteSpace(feedback.Username) ? feedback.ChatId : feedback.Username;
        }

        var request = CreateTemplateRequest(
            _options.FeedbackRecipientEmail,
            string.IsNullOrWhiteSpace(_options.FeedbackRecipientName) ? _options.FeedbackRecipientEmail : _options.FeedbackRecipientName,
            _options.FeedbackTemplateId,
            new
            {
                subject = "Telegram feedback received",
                heading = "Telegram feedback received",
                chatId = feedback.ChatId,
                firstName = feedback.FirstName,
                lastName = feedback.LastName,
                username = feedback.Username,
                displayName,
                message = feedback.Message,
                receivedAt = feedback.ReceivedAt,
                sentAt = DateTimeOffset.UtcNow
            },
            subject: "Telegram feedback received");

        return SendAsync(request, cancellationToken);
    }
    public Task<string?> SendAdminNotificationAsync(
        string subject,
        string heading,
        string body,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(_options.CustomEmailTemplateId, requireOwnerEmail: true);

        var request = CreateTemplateRequest(
            _options.OwnerEmail,
            _options.OwnerName,
            _options.CustomEmailTemplateId,
            new
            {
                name = _options.OwnerName,
                email = _options.OwnerEmail,
                subject,
                heading,
                body,
                sentAt = DateTimeOffset.UtcNow
            },
            subject: subject);

        return SendAsync(request, cancellationToken);
    }

    private async Task<string?> SendAsync(BrevoSendEmailRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("api-key", _options.ApiKey);
        httpRequest.Headers.Add("accept", "application/json");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<BrevoSendEmailResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = result?.MessageId ?? await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Brevo email send failed: {detail}");
        }

        return result?.MessageId;
    }

    private BrevoSendEmailRequest CreateTemplateRequest(
        string toEmail,
        string toName,
        int templateId,
        object parameters,
        string? subject = null,
        string? replyToEmail = null,
        string? replyToName = null)
    {
        var request = new BrevoSendEmailRequest
        {
            Sender = new BrevoEmailAddress(_options.SenderEmail, _options.SenderName),
            To = [new BrevoEmailAddress(toEmail, toName)],
            TemplateId = templateId,
            Params = parameters,
            Subject = subject
        };

        if (!string.IsNullOrWhiteSpace(replyToEmail))
        {
            request.ReplyTo = new BrevoEmailAddress(replyToEmail, replyToName ?? replyToEmail);
        }

        return request;
    }

    private void EnsureConfigured(int templateId, bool requireOwnerEmail = false)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.SenderEmail) ||
            (requireOwnerEmail && string.IsNullOrWhiteSpace(_options.OwnerEmail)) ||
            templateId <= 0)
        {
            throw new InvalidOperationException("Brevo email configuration is incomplete.");
        }
    }

    private sealed class BrevoSendEmailRequest
    {
        [JsonPropertyName("sender")]
        public BrevoEmailAddress Sender { get; set; } = null!;

        [JsonPropertyName("to")]
        public List<BrevoEmailAddress> To { get; set; } = [];

        [JsonPropertyName("replyTo")]
        public BrevoEmailAddress? ReplyTo { get; set; }

        [JsonPropertyName("templateId")]
        public int TemplateId { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("params")]
        public object Params { get; set; } = new();
    }

    private sealed record BrevoEmailAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string Name);

    private sealed record BrevoSendEmailResponse([property: JsonPropertyName("messageId")] string? MessageId);
}
