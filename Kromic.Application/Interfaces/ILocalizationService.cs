namespace Kromic.Application.Interfaces;

public interface ILocalizationService
{
    string GetString(string key, string language = "en");
    string GetString(string key, string language, params object[] args);
}
