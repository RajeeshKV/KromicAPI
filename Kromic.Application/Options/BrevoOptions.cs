namespace Kromic.Application.Options;

public sealed class BrevoOptions
{
    public string BaseUrl { get; set; } = "https://api.brevo.com/v3";
    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Kromic";
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerName { get; set; } = "Kromic Admin";
    public int ContactNotificationTemplateId { get; set; }
    public int ContactResponseTemplateId { get; set; }
    public int CustomEmailTemplateId { get; set; }
    public int WeeklySummaryEmailTemplateId { get; set; }
    public int FeedbackTemplateId { get; set; }
    public string FeedbackRecipientEmail { get; set; } = "rajeeshkva2z@gmail.com";
    public string FeedbackRecipientName { get; set; } = "Rajeesh";
}
