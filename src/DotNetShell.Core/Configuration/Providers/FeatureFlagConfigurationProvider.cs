using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;

namespace DotNetShell.Core.Configuration.Providers;

/// <summary>
/// Configuration provider for feature flags from an external service or local configuration.
/// </summary>
public class FeatureFlagConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly string _connectionString;
    private readonly HttpClient? _httpClient;
    private readonly Timer? _refreshTimer;
    private readonly bool _useHttpService;
    private bool _disposed;

    public FeatureFlagConfigurationProvider(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        // Determine if this is an HTTP service or a local configuration
        _useHttpService = _connectionString.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         _connectionString.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (_useHttpService)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_connectionString),
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Set up periodic refresh for HTTP-based feature flags
            _refreshTimer = new Timer(RefreshFeatureFlags, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }
    }

    public override void Load()
    {
        try
        {
            if (_useHttpService)
            {
                LoadFromHttpService().Wait();
            }
            else
            {
                LoadFromLocalConfiguration();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load feature flags: {ex.Message}");
        }
    }

    private async Task LoadFromHttpService()
    {
        if (_httpClient == null) return;

        try
        {
            var response = await _httpClient.GetAsync("/api/features");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var featureFlags = JsonSerializer.Deserialize<FeatureFlagResponse>(json);

                if (featureFlags?.Features != null)
                {
                    Data.Clear();
                    foreach (var feature in featureFlags.Features)
                    {
                        var key = $"Features:{feature.Name}";
                        Data[key] = feature.Enabled.ToString().ToLowerInvariant();

                        // Add additional metadata if available
                        if (!string.IsNullOrEmpty(feature.Description))
                        {
                            Data[$"{key}:Description"] = feature.Description;
                        }

                        if (feature.RolloutPercentage.HasValue)
                        {
                            Data[$"{key}:RolloutPercentage"] = feature.RolloutPercentage.Value.ToString();
                        }

                        if (feature.TargetAudience?.Any() == true)
                        {
                            for (int i = 0; i < feature.TargetAudience.Count; i++)
                            {
                                Data[$"{key}:TargetAudience:{i}"] = feature.TargetAudience[i];
                            }
                        }

                        if (feature.Rules?.Any() == true)
                        {
                            for (int i = 0; i < feature.Rules.Count; i++)
                            {
                                var rule = feature.Rules[i];
                                Data[$"{key}:Rules:{i}:Property"] = rule.Property ?? string.Empty;
                                Data[$"{key}:Rules:{i}:Operator"] = rule.Operator ?? string.Empty;
                                Data[$"{key}:Rules:{i}:Value"] = rule.Value ?? string.Empty;
                            }
                        }
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP error loading feature flags: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Feature flag service request timed out");
        }
    }

    private void LoadFromLocalConfiguration()
    {
        // Load from local file or configuration string
        if (File.Exists(_connectionString))
        {
            try
            {
                var json = File.ReadAllText(_connectionString);
                var featureFlags = JsonSerializer.Deserialize<FeatureFlagResponse>(json);

                if (featureFlags?.Features != null)
                {
                    Data.Clear();
                    foreach (var feature in featureFlags.Features)
                    {
                        var key = $"Features:{feature.Name}";
                        Data[key] = feature.Enabled.ToString().ToLowerInvariant();

                        // Add metadata
                        if (!string.IsNullOrEmpty(feature.Description))
                        {
                            Data[$"{key}:Description"] = feature.Description;
                        }

                        if (feature.RolloutPercentage.HasValue)
                        {
                            Data[$"{key}:RolloutPercentage"] = feature.RolloutPercentage.Value.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading feature flags from file: {ex.Message}");
            }
        }
        else
        {
            // Default feature flags
            LoadDefaultFeatureFlags();
        }
    }

    private void LoadDefaultFeatureFlags()
    {
        var defaultFeatures = new Dictionary<string, bool>
        {
            { "NewAuthFlow", false },
            { "EnableMetrics", true },
            { "UseDistributedCache", false },
            { "EnableSwagger", true },
            { "DetailedLogging", false }
        };

        foreach (var feature in defaultFeatures)
        {
            Data[$"Features:{feature.Key}"] = feature.Value.ToString().ToLowerInvariant();
        }
    }

    private void RefreshFeatureFlags(object? state)
    {
        if (_disposed) return;

        try
        {
            var oldData = new Dictionary<string, string?>(Data);
            Load();

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
/// Response model for feature flag service.
/// </summary>
public class FeatureFlagResponse
{
    public List<FeatureFlag> Features { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Model for a feature flag.
/// </summary>
public class FeatureFlag
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Description { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public double? RolloutPercentage { get; set; }
    public List<string>? TargetAudience { get; set; }
    public List<FeatureFlagRule>? Rules { get; set; }
}

/// <summary>
/// Model for feature flag rules.
/// </summary>
public class FeatureFlagRule
{
    public string? Property { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }
}

/// <summary>
/// Configuration source for feature flag provider.
/// </summary>
public class FeatureFlagConfigurationSource : IConfigurationSource
{
    public string ConnectionString { get; set; } = string.Empty;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new FeatureFlagConfigurationProvider(ConnectionString);
    }
}

/// <summary>
/// Extension methods for adding feature flag configuration.
/// </summary>
public static class FeatureFlagConfigurationExtensions
{
    /// <summary>
    /// Adds feature flag configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="connectionString">Feature flag service connection string or file path.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddFeatureFlags(
        this IConfigurationBuilder builder,
        string connectionString)
    {
        return builder.Add(new FeatureFlagConfigurationSource
        {
            ConnectionString = connectionString
        });
    }
}