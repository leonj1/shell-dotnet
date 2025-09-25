using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;

namespace DotNetShell.Core.Configuration.Providers;

/// <summary>
/// Configuration provider that loads configuration from an external HTTP service.
/// </summary>
public class ExternalConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly string _serviceUrl;
    private readonly TimeSpan _refreshInterval;
    private readonly HttpClient _httpClient;
    private readonly Timer? _refreshTimer;
    private bool _disposed;

    public ExternalConfigurationProvider(string serviceUrl, TimeSpan refreshInterval)
    {
        _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
        _refreshInterval = refreshInterval;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Set up periodic refresh
        _refreshTimer = new Timer(RefreshConfiguration, null, TimeSpan.Zero, _refreshInterval);
    }

    public override void Load()
    {
        try
        {
            LoadFromExternalService().Wait();
        }
        catch (Exception ex)
        {
            // Log error but don't fail startup
            Console.WriteLine($"Warning: Failed to load from external configuration service: {ex.Message}");
        }
    }

    private async Task LoadFromExternalService()
    {
        try
        {
            var response = await _httpClient.GetAsync(_serviceUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (configData != null)
                {
                    Data.Clear();
                    FlattenDictionary(configData, string.Empty);
                }
            }
        }
        catch (HttpRequestException)
        {
            // External service is not available - keep existing configuration
        }
        catch (TaskCanceledException)
        {
            // Request timed out - keep existing configuration
        }
    }

    private void FlattenDictionary(Dictionary<string, object> source, string prefix)
    {
        foreach (var kvp in source)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}:{kvp.Key}";

            if (kvp.Value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        var nestedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                        if (nestedDict != null)
                        {
                            FlattenDictionary(nestedDict, key);
                        }
                        break;

                    case JsonValueKind.Array:
                        var arrayIndex = 0;
                        foreach (var item in element.EnumerateArray())
                        {
                            Data[$"{key}:{arrayIndex}"] = item.ToString();
                            arrayIndex++;
                        }
                        break;

                    default:
                        Data[key] = element.ToString();
                        break;
                }
            }
            else
            {
                Data[key] = kvp.Value?.ToString();
            }
        }
    }

    private void RefreshConfiguration(object? state)
    {
        if (_disposed) return;

        try
        {
            var oldData = new Dictionary<string, string?>(Data);
            LoadFromExternalService().Wait();

            // Check for changes and trigger reload token if needed
            if (!DictionariesEqual(oldData, Data))
            {
                OnReload();
            }
        }
        catch
        {
            // Ignore refresh errors to prevent timer from stopping
        }
    }

    private static bool DictionariesEqual(Dictionary<string, string?> dict1, IDictionary<string, string?> dict2)
    {
        if (dict1.Count != dict2.Count)
            return false;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                return false;
        }

        return true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _refreshTimer?.Dispose();
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration source for external service provider.
/// </summary>
public class ExternalConfigurationSource : IConfigurationSource
{
    public string ServiceUrl { get; set; } = string.Empty;
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ExternalConfigurationProvider(ServiceUrl, RefreshInterval);
    }
}

/// <summary>
/// Extension methods for adding external configuration service.
/// </summary>
public static class ExternalConfigurationExtensions
{
    /// <summary>
    /// Adds external configuration service provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="serviceUrl">The external service URL.</param>
    /// <param name="refreshInterval">How often to refresh the configuration.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddExternalService(
        this IConfigurationBuilder builder,
        string serviceUrl,
        TimeSpan refreshInterval)
    {
        return builder.Add(new ExternalConfigurationSource
        {
            ServiceUrl = serviceUrl,
            RefreshInterval = refreshInterval
        });
    }
}