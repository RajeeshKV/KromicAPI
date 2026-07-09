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
                var resources = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);

                if (resources != null)
                {
                    var flattened = FlattenDictionary(resources);
                    _resources[langCode] = flattened;
                    _logger.LogInformation("Loaded {Count} resources for language: {Lang}", flattened.Count, langCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading localization resources");
        }
    }

    private Dictionary<string, string> FlattenDictionary(Dictionary<string, object> dict, string prefix = "")
    {
        var result = new Dictionary<string, string>();

        foreach (var kvp in dict)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is Dictionary<string, object> nestedDict)
            {
                var nested = FlattenDictionary(nestedDict, key);
                foreach (var nestedKvp in nested)
                {
                    result[nestedKvp.Key] = nestedKvp.Value;
                }
            }
            else if (kvp.Value != null)
            {
                result[key] = kvp.Value.ToString() ?? string.Empty;
            }
        }

        return result;
    }
}
