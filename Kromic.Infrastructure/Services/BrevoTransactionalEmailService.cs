using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
        EnsureConfigured(_options.ContactNotificationTemplateId);

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
        string? replyToEmail = null,
        string? replyToName = null)
    {
        var request = new BrevoSendEmailRequest
        {
            Sender = new BrevoEmailAddress(_options.SenderEmail, _options.SenderName),
            To = [new BrevoEmailAddress(toEmail, toName)],
            TemplateId = templateId,
            Params = parameters
        };

        if (!string.IsNullOrWhiteSpace(replyToEmail))
        {
            request.ReplyTo = new BrevoEmailAddress(replyToEmail, replyToName ?? replyToEmail);
        }

        return request;
    }

    private void EnsureConfigured(int templateId)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.SenderEmail) ||
            string.IsNullOrWhiteSpace(_options.OwnerEmail) ||
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

        [JsonPropertyName("params")]
        public object Params { get; set; } = new();
    }

    private sealed record BrevoEmailAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string Name);

    private sealed record BrevoSendEmailResponse([property: JsonPropertyName("messageId")] string? MessageId);
}
