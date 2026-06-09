using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kromic.Infrastructure.Services;

public sealed class CustomEmailService(KromicDbContext dbContext, ITransactionalEmailService emailService) : ICustomEmailService
{
    private const int MaxRecipientsPerRequest = 100;

    public async Task<CustomEmailSendResponse> SendAsync(CustomEmailRequest request, CancellationToken cancellationToken)
    {
        var subject = request.Subject.Trim();
        var body = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Subject and body are required.");
        }

        var recipients = await ResolveRecipientsAsync(request, cancellationToken);
        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("At least one recipient is required.");
        }

        if (recipients.Count > MaxRecipientsPerRequest)
        {
            throw new InvalidOperationException($"A maximum of {MaxRecipientsPerRequest} recipients can be emailed at once.");
        }

        var heading = TrimToNull(request.Heading);
        var callToActionText = TrimToNull(request.CallToActionText);
        var callToActionUrl = TrimToNull(request.CallToActionUrl);
        var sentRecipients = new List<CustomEmailRecipientResponse>(recipients.Count);

        foreach (var recipient in recipients)
        {
            var messageId = await emailService.SendCustomEmailAsync(
                recipient.Email,
                recipient.Name,
                subject,
                heading,
                body,
                callToActionText,
                callToActionUrl,
                cancellationToken);

            sentRecipients.Add(new CustomEmailRecipientResponse(recipient.Email, recipient.Name, messageId));
        }

        return new CustomEmailSendResponse(sentRecipients.Count, sentRecipients);
    }

    private async Task<List<ResolvedEmailRecipient>> ResolveRecipientsAsync(CustomEmailRequest request, CancellationToken cancellationToken)
    {
        var recipientsByEmail = new Dictionary<string, ResolvedEmailRecipient>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipient in request.Recipients)
        {
            AddRecipient(recipientsByEmail, recipient.Email, recipient.Name);
        }

        if (request.IncludeAllContactedUsers)
        {
            var contacts = await dbContext.ContactSubmissions
                .AsNoTracking()
                .Select(x => new { x.Email, x.Name })
                .ToListAsync(cancellationToken);

            foreach (var contact in contacts)
            {
                AddRecipient(recipientsByEmail, contact.Email, contact.Name);
            }
        }

        var contactIds = request.ContactIds.Distinct().ToArray();
        if (contactIds.Length > 0)
        {
            var selectedContacts = await dbContext.ContactSubmissions
                .AsNoTracking()
                .Where(x => contactIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Email, x.Name })
                .ToListAsync(cancellationToken);

            if (selectedContacts.Count != contactIds.Length)
            {
                var foundIds = selectedContacts.Select(x => x.Id).ToHashSet();
                var missingIds = contactIds.Where(id => !foundIds.Contains(id));
                throw new InvalidOperationException($"Contact recipients were not found: {string.Join(", ", missingIds)}");
            }

            foreach (var contact in selectedContacts)
            {
                AddRecipient(recipientsByEmail, contact.Email, contact.Name);
            }
        }

        return recipientsByEmail.Values
            .OrderBy(x => x.Email)
            .ToList();
    }

    private static void AddRecipient(
        IDictionary<string, ResolvedEmailRecipient> recipientsByEmail,
        string email,
        string? name)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var displayName = TrimToNull(name) ?? normalizedEmail;
        recipientsByEmail.TryAdd(normalizedEmail, new ResolvedEmailRecipient(normalizedEmail, displayName));
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ResolvedEmailRecipient(string Email, string Name);
}
