namespace Kromic.Application.DTOs;

public sealed class TelegramMenuButton
{
    public string Text { get; set; } = string.Empty;
    public string CallbackData { get; set; } = string.Empty;
}

public sealed class TelegramMenuRow
{
    public List<TelegramMenuButton> Buttons { get; set; } = new();
}

public enum MenuLevel
{
    Main,
    Reports,
    Settings,
    Email,
    Feedback
}
