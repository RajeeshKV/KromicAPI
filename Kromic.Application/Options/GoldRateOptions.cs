namespace Kromic.Application.Options;

public sealed class GoldRateOptions
{
    public string Endpoint { get; set; } = "https://backend.josalukkasonline.com/api/Master/GetLatestGoldRate";
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientEmailsCsv { get; set; } = string.Empty;
    public List<string> RecipientEmails { get; set; } = [];
    public string RecipientName { get; set; } = "Kromic Admin";
    public bool DailyJobEnabled { get; set; } = true;
}
