using System.Text.Json;
using Kromic.Application.Interfaces;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Kromic.Infrastructure.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private readonly string _resourcesPath;
    private readonly ILogger<LocalizationService> _logger;
    private readonly bool _useDatabase;
    private readonly IServiceProvider _serviceProvider;

    public LocalizationService(IHostEnvironment environment, ILogger<LocalizationService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _resourcesPath = Path.Combine(environment.ContentRootPath, "Resources");
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<KromicDbContext>();
        _useDatabase = dbContext != null;

        if (_useDatabase)
        {
            LoadResourcesFromDatabase(dbContext!);
        }
        else
        {
            LoadResourcesFromJson();
        }
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

        _logger.LogWarning("Localization key not found: {Key} for language: {Language}. Available keys: {AvailableKeys}", key, language, string.Join(", ", _resources.Keys));
        return key; // Return key as fallback
    }

    public string GetString(string key, string language, params object[] args)
    {
        try
        {
            var template = GetString(key, language);
            var result = string.Format(template, args);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting localization string for key: {Key}, language: {Language}", key, language);
            // Return a fallback message with the args
            if (args != null && args.Length > 0)
            {
                return $"{key}: {string.Join(", ", args)}";
            }
            return key;
        }
    }

    private void LoadResourcesFromDatabase(KromicDbContext dbContext)
    {
        try
        {
            if (dbContext == null)
            {
                _logger.LogWarning("DbContext is null, falling back to JSON files");
                LoadResourcesFromJson();
                return;
            }

            var resources = dbContext.LocalizationResources
                .AsNoTracking()
                .ToList();

            foreach (var resource in resources)
            {
                if (!_resources.ContainsKey(resource.Language))
                {
                    _resources[resource.Language] = new Dictionary<string, string>();
                }
                _resources[resource.Language][resource.Key] = resource.Value;
            }

            _logger.LogInformation("Loaded {Count} localization resources from database", resources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading localization resources from database, falling back to JSON files");
            LoadResourcesFromJson();
        }
    }

    private void LoadResourcesFromJson()
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
                    _logger.LogInformation("Loaded {Count} resources for language: {Lang} from JSON", flattened.Count, langCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading localization resources from JSON files");
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
