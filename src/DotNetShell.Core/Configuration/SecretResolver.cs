using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace DotNetShell.Core.Configuration;

/// <summary>
/// Interface for resolving secret placeholders in configuration values.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Gets a value indicating whether the resolver supports encryption.
    /// </summary>
    bool SupportsEncryption { get; }

    /// <summary>
    /// Resolves secret placeholders in a configuration value.
    /// </summary>
    /// <param name="value">The configuration value that may contain placeholders.</param>
    /// <returns>The resolved value with secrets substituted.</returns>
    string? ResolveSecrets(string? value);

    /// <summary>
    /// Resolves secret placeholders in a configuration value asynchronously.
    /// </summary>
    /// <param name="value">The configuration value that may contain placeholders.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved value with secrets substituted.</returns>
    Task<string?> ResolveSecretsAsync(string? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves secret placeholders in all string properties of an object.
    /// </summary>
    /// <param name="obj">The object to process.</param>
    void ResolveSecretsInObject(object obj);

    /// <summary>
    /// Checks if a value contains secret placeholders.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value contains placeholders; otherwise, false.</returns>
    bool ContainsSecretPlaceholders(string? value);

    /// <summary>
    /// Adds a secret provider to the resolver.
    /// </summary>
    /// <param name="provider">The secret provider to add.</param>
    void AddProvider(ISecretProvider provider);
}

/// <summary>
/// Interface for secret providers.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Gets the provider name/type.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the supported placeholder prefix (e.g., "@KeyVault:", "@Env:").
    /// </summary>
    string PlaceholderPrefix { get; }

    /// <summary>
    /// Gets a value indicating whether the provider supports encryption.
    /// </summary>
    bool SupportsEncryption { get; }

    /// <summary>
    /// Retrieves a secret value.
    /// </summary>
    /// <param name="secretName">The name/key of the secret.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretAsync(string secretName);

    /// <summary>
    /// Stores a secret value (if supported).
    /// </summary>
    /// <param name="secretName">The name/key of the secret.</param>
    /// <param name="secretValue">The secret value to store.</param>
    /// <returns>A task representing the operation.</returns>
    Task SetSecretAsync(string secretName, string secretValue);

    /// <summary>
    /// Checks if the provider is available and healthy.
    /// </summary>
    /// <returns>True if the provider is healthy; otherwise, false.</returns>
    Task<bool> IsHealthyAsync();
}

/// <summary>
/// Implementation of ISecretResolver that supports multiple secret providers
/// and various placeholder syntaxes.
/// </summary>
public class SecretResolver : ISecretResolver
{
    private static readonly Regex PlaceholderRegex = new(
        @"@(?<provider>\w+):(?<secret>[^@\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<ISecretProvider> _providers;
    private readonly Dictionary<string, ISecretProvider> _providerMap;

    public SecretResolver()
    {
        _providers = new List<ISecretProvider>();
        _providerMap = new Dictionary<string, ISecretProvider>(StringComparer.OrdinalIgnoreCase);

        // Add default providers
        AddProvider(new EnvironmentVariableSecretProvider());
        AddProvider(new InMemorySecretProvider());
    }

    /// <inheritdoc />
    public bool SupportsEncryption => _providers.Any(p => p.SupportsEncryption);

    /// <inheritdoc />
    public string? ResolveSecrets(string? value)
    {
        if (string.IsNullOrEmpty(value) || !ContainsSecretPlaceholders(value))
        {
            return value;
        }

        return PlaceholderRegex.Replace(value, match =>
        {
            var providerName = match.Groups["provider"].Value;
            var secretName = match.Groups["secret"].Value;

            if (_providerMap.TryGetValue(providerName, out var provider))
            {
                try
                {
                    // For synchronous resolution, we'll use GetAwaiter().GetResult()
                    // In a real implementation, you might want to cache resolved secrets
                    return provider.GetSecretAsync(secretName).GetAwaiter().GetResult() ?? match.Value;
                }
                catch
                {
                    // Return the original placeholder if resolution fails
                    return match.Value;
                }
            }

            return match.Value;
        });
    }

    /// <inheritdoc />
    public async Task<string?> ResolveSecretsAsync(string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(value) || !ContainsSecretPlaceholders(value))
        {
            return value;
        }

        var matches = PlaceholderRegex.Matches(value);
        var result = value;

        foreach (Match match in matches)
        {
            var providerName = match.Groups["provider"].Value;
            var secretName = match.Groups["secret"].Value;

            if (_providerMap.TryGetValue(providerName, out var provider))
            {
                try
                {
                    var secretValue = await provider.GetSecretAsync(secretName);
                    if (secretValue != null)
                    {
                        result = result.Replace(match.Value, secretValue);
                    }
                }
                catch
                {
                    // Keep the original placeholder if resolution fails
                    continue;
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void ResolveSecretsInObject(object obj)
    {
        if (obj == null) return;

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var property in properties)
        {
            try
            {
                if (property.PropertyType == typeof(string))
                {
                    var value = (string?)property.GetValue(obj);
                    var resolvedValue = ResolveSecrets(value);
                    property.SetValue(obj, resolvedValue);
                }
                else if (!property.PropertyType.IsPrimitive && property.PropertyType != typeof(DateTime) &&
                         property.PropertyType != typeof(TimeSpan) && property.PropertyType != typeof(Guid))
                {
                    // Recursively resolve secrets in complex objects
                    var value = property.GetValue(obj);
                    if (value != null)
                    {
                        ResolveSecretsInObject(value);
                    }
                }
            }
            catch
            {
                // Continue processing other properties if one fails
                continue;
            }
        }
    }

    /// <inheritdoc />
    public bool ContainsSecretPlaceholders(string? value)
    {
        return !string.IsNullOrEmpty(value) && PlaceholderRegex.IsMatch(value);
    }

    /// <inheritdoc />
    public void AddProvider(ISecretProvider provider)
    {
        _providers.Add(provider);
        _providerMap[ExtractProviderKeyFromPrefix(provider.PlaceholderPrefix)] = provider;
    }

    private static string ExtractProviderKeyFromPrefix(string prefix)
    {
        // Extract provider key from prefix like "@KeyVault:" -> "KeyVault"
        return prefix.TrimStart('@').TrimEnd(':');
    }
}

/// <summary>
/// Secret provider that resolves secrets from environment variables.
/// </summary>
public class EnvironmentVariableSecretProvider : ISecretProvider
{
    public string ProviderName => "Environment Variables";
    public string PlaceholderPrefix => "@Env:";
    public bool SupportsEncryption => false;

    public Task<string?> GetSecretAsync(string secretName)
    {
        var value = Environment.GetEnvironmentVariable(secretName);
        return Task.FromResult(value);
    }

    public Task SetSecretAsync(string secretName, string secretValue)
    {
        Environment.SetEnvironmentVariable(secretName, secretValue);
        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }
}

/// <summary>
/// In-memory secret provider for development and testing.
/// </summary>
public class InMemorySecretProvider : ISecretProvider
{
    private readonly Dictionary<string, string> _secrets;

    public InMemorySecretProvider()
    {
        _secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string ProviderName => "In-Memory";
    public string PlaceholderPrefix => "@Memory:";
    public bool SupportsEncryption => false;

    public Task<string?> GetSecretAsync(string secretName)
    {
        _secrets.TryGetValue(secretName, out var value);
        return Task.FromResult(value);
    }

    public Task SetSecretAsync(string secretName, string secretValue)
    {
        _secrets[secretName] = secretValue;
        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Adds a secret to the in-memory store.
    /// </summary>
    /// <param name="secretName">The secret name.</param>
    /// <param name="secretValue">The secret value.</param>
    public void AddSecret(string secretName, string secretValue)
    {
        _secrets[secretName] = secretValue;
    }
}

/// <summary>
/// Azure Key Vault secret provider (placeholder implementation).
/// </summary>
public class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly string _keyVaultUrl;
    private readonly bool _isHealthy;

    public AzureKeyVaultSecretProvider(string keyVaultUrl)
    {
        _keyVaultUrl = keyVaultUrl ?? throw new ArgumentNullException(nameof(keyVaultUrl));
        _isHealthy = !string.IsNullOrEmpty(_keyVaultUrl);
    }

    public string ProviderName => "Azure Key Vault";
    public string PlaceholderPrefix => "@KeyVault:";
    public bool SupportsEncryption => true;

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (!_isHealthy)
            return null;

        try
        {
            // Placeholder implementation
            // In a real implementation, you would use Azure.Security.KeyVault.Secrets
            // var client = new SecretClient(new Uri(_keyVaultUrl), new DefaultAzureCredential());
            // var response = await client.GetSecretAsync(secretName);
            // return response.Value.Value;

            await Task.Delay(10); // Simulate network call
            return null; // Placeholder return
        }
        catch
        {
            return null;
        }
    }

    public async Task SetSecretAsync(string secretName, string secretValue)
    {
        if (!_isHealthy)
            throw new InvalidOperationException("Key Vault provider is not healthy");

        try
        {
            // Placeholder implementation
            // var client = new SecretClient(new Uri(_keyVaultUrl), new DefaultAzureCredential());
            // await client.SetSecretAsync(secretName, secretValue);

            await Task.Delay(10); // Simulate network call
        }
        catch
        {
            throw;
        }
    }

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(_isHealthy);
    }
}

/// <summary>
/// File-based secret provider for development scenarios.
/// </summary>
public class FileSecretProvider : ISecretProvider
{
    private readonly string _secretsFilePath;
    private readonly Dictionary<string, string> _secrets;
    private readonly FileSystemWatcher? _fileWatcher;
    private DateTime _lastModified;

    public FileSecretProvider(string secretsFilePath)
    {
        _secretsFilePath = secretsFilePath ?? throw new ArgumentNullException(nameof(secretsFilePath));
        _secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        LoadSecrets();

        // Set up file watcher for hot reload
        if (File.Exists(_secretsFilePath))
        {
            _fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(_secretsFilePath) ?? ".",
                Path.GetFileName(_secretsFilePath));
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.EnableRaisingEvents = true;
        }
    }

    public string ProviderName => "File";
    public string PlaceholderPrefix => "@File:";
    public bool SupportsEncryption => false;

    public Task<string?> GetSecretAsync(string secretName)
    {
        CheckForFileChanges();
        _secrets.TryGetValue(secretName, out var value);
        return Task.FromResult(value);
    }

    public async Task SetSecretAsync(string secretName, string secretValue)
    {
        _secrets[secretName] = secretValue;
        await SaveSecrets();
    }

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(File.Exists(_secretsFilePath) || Directory.Exists(Path.GetDirectoryName(_secretsFilePath)));
    }

    private void LoadSecrets()
    {
        try
        {
            if (!File.Exists(_secretsFilePath))
                return;

            var json = File.ReadAllText(_secretsFilePath);
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (secrets != null)
            {
                _secrets.Clear();
                foreach (var kvp in secrets)
                {
                    _secrets[kvp.Key] = kvp.Value;
                }
            }

            _lastModified = File.GetLastWriteTime(_secretsFilePath);
        }
        catch
        {
            // Ignore errors loading secrets file
        }
    }

    private async Task SaveSecrets()
    {
        try
        {
            var directory = Path.GetDirectoryName(_secretsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_secrets, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_secretsFilePath, json);
            _lastModified = File.GetLastWriteTime(_secretsFilePath);
        }
        catch
        {
            // Ignore errors saving secrets file
        }
    }

    private void CheckForFileChanges()
    {
        if (File.Exists(_secretsFilePath))
        {
            var currentModified = File.GetLastWriteTime(_secretsFilePath);
            if (currentModified > _lastModified)
            {
                LoadSecrets();
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce file changes
        Task.Delay(500).ContinueWith(_ => LoadSecrets());
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}