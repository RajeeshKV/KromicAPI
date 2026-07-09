using System.Text.Json;
using Kromic.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Kromic.Infrastructure.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private readonly string _resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
    private readonly ILogger<LocalizationService> _logger;

    public LocalizationService(ILogger<LocalizationService> logger)
    {
        _logger = logger;
        LoadResources();
    }

    public string GetString(string key, string language = "en")
    {
        if (_resources.TryGetValue(language, out var langResources))
        {
            if (langResources.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Fallback to English if key not found in requested language
        if (language != "en" && _resources.TryGetValue("en", out var enResources))
        {
            if (enResources.TryGetValue(key, out var enValue))
            {
                return enValue;
            }
        }

        _logger.LogWarning("Localization key not found: {Key} for language: {Language}", key, language);
        return key; // Return key as fallback
    }

    public string GetString(string key, string language, params object[] args)
    {
        var template = GetString(key, language);
        return string.Format(template, args);
    }

    private void LoadResources()
    {
        try
        {
            if (!Directory.Exists(_resourcesPath))
            {
                _logger.LogWarning("Resources directory not found: {Path}", _resourcesPath);
                return;
            }

            var jsonFiles = Directory.GetFiles(_resourcesPath, "*.json");
            foreach (var file in jsonFiles)
            {
                var langCode = Path.GetFileNameWithoutExtension(file);
                var jsonContent = File.ReadAllText(file);
                var resources = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                if (resources != null)
                {
                    _resources[langCode] = resources;
                    _logger.LogInformation("Loaded {Count} resources for language: {Lang}", resources.Count, langCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading localization resources");
        }
    }
}
