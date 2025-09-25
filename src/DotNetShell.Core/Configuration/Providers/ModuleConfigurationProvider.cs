using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace DotNetShell.Core.Configuration.Providers;

/// <summary>
/// Configuration provider that loads module-specific configuration files
/// from the modules directory structure.
/// </summary>
public class ModuleConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly string _modulesPath;
    private readonly Dictionary<string, FileSystemWatcher> _watchers;
    private readonly object _lock = new();
    private bool _disposed;

    public ModuleConfigurationProvider(string modulesPath)
    {
        _modulesPath = modulesPath ?? throw new ArgumentNullException(nameof(modulesPath));
        _watchers = new Dictionary<string, FileSystemWatcher>();
    }

    public override void Load()
    {
        lock (_lock)
        {
            try
            {
                LoadModuleConfigurations();
                SetupFileWatchers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load module configurations: {ex.Message}");
            }
        }
    }

    private void LoadModuleConfigurations()
    {
        Data.Clear();

        if (!Directory.Exists(_modulesPath))
        {
            Console.WriteLine($"Modules directory not found: {_modulesPath}");
            return;
        }

        // Scan for module directories
        var moduleDirectories = Directory.GetDirectories(_modulesPath);

        foreach (var moduleDir in moduleDirectories)
        {
            var moduleName = Path.GetFileName(moduleDir);
            LoadModuleConfiguration(moduleName, moduleDir);
        }
    }

    private void LoadModuleConfiguration(string moduleName, string moduleDirectory)
    {
        // Look for various configuration files in the module directory
        var configFiles = new[]
        {
            Path.Combine(moduleDirectory, "appsettings.json"),
            Path.Combine(moduleDirectory, "module.json"),
            Path.Combine(moduleDirectory, $"{moduleName}.json")
        };

        foreach (var configFile in configFiles)
        {
            if (File.Exists(configFile))
            {
                LoadConfigurationFile(moduleName, configFile);
            }
        }

        // Also check for environment-specific configuration files
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var envConfigFiles = new[]
        {
            Path.Combine(moduleDirectory, $"appsettings.{environmentName}.json"),
            Path.Combine(moduleDirectory, $"{moduleName}.{environmentName}.json")
        };

        foreach (var configFile in envConfigFiles)
        {
            if (File.Exists(configFile))
            {
                LoadConfigurationFile(moduleName, configFile);
            }
        }
    }

    private void LoadConfigurationFile(string moduleName, string configFile)
    {
        try
        {
            var json = File.ReadAllText(configFile);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (configData != null)
            {
                // Prefix all configuration keys with the module name
                FlattenModuleConfiguration(configData, $"Modules:{moduleName}");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Warning: Invalid JSON in {configFile}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error loading {configFile}: {ex.Message}");
        }
    }

    private void FlattenModuleConfiguration(Dictionary<string, object> source, string prefix)
    {
        foreach (var kvp in source)
        {
            var key = $"{prefix}:{kvp.Key}";

            if (kvp.Value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        var nestedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                        if (nestedDict != null)
                        {
                            FlattenModuleConfiguration(nestedDict, key);
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

    private void SetupFileWatchers()
    {
        if (!Directory.Exists(_modulesPath))
            return;

        // Clean up existing watchers
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
        _watchers.Clear();

        // Set up watchers for each module directory
        var moduleDirectories = Directory.GetDirectories(_modulesPath);

        foreach (var moduleDir in moduleDirectories)
        {
            var moduleName = Path.GetFileName(moduleDir);
            SetupModuleWatcher(moduleName, moduleDir);
        }

        // Also watch for new module directories
        var mainWatcher = new FileSystemWatcher(_modulesPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName
        };

        mainWatcher.Created += OnModuleDirectoryCreated;
        mainWatcher.Deleted += OnModuleDirectoryDeleted;
        mainWatcher.EnableRaisingEvents = true;

        _watchers["_main"] = mainWatcher;
    }

    private void SetupModuleWatcher(string moduleName, string moduleDirectory)
    {
        var watcher = new FileSystemWatcher(moduleDirectory, "*.json")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        watcher.Changed += (sender, args) => OnModuleConfigurationChanged(moduleName, args.FullPath);
        watcher.Created += (sender, args) => OnModuleConfigurationChanged(moduleName, args.FullPath);
        watcher.Deleted += (sender, args) => OnModuleConfigurationChanged(moduleName, args.FullPath);
        watcher.EnableRaisingEvents = true;

        _watchers[moduleName] = watcher;
    }

    private void OnModuleDirectoryCreated(object sender, FileSystemEventArgs e)
    {
        var moduleName = Path.GetFileName(e.FullPath);
        if (Directory.Exists(e.FullPath))
        {
            LoadModuleConfiguration(moduleName, e.FullPath);
            SetupModuleWatcher(moduleName, e.FullPath);
            OnReload();
        }
    }

    private void OnModuleDirectoryDeleted(object sender, FileSystemEventArgs e)
    {
        var moduleName = Path.GetFileName(e.FullPath);

        // Remove configuration for this module
        var keysToRemove = Data.Keys.Where(k => k.StartsWith($"Modules:{moduleName}:")).ToList();
        foreach (var key in keysToRemove)
        {
            Data.Remove(key);
        }

        // Clean up watcher
        if (_watchers.TryGetValue(moduleName, out var watcher))
        {
            watcher.Dispose();
            _watchers.Remove(moduleName);
        }

        OnReload();
    }

    private void OnModuleConfigurationChanged(string moduleName, string filePath)
    {
        // Debounce file system events
        Task.Delay(100).ContinueWith(_ =>
        {
            lock (_lock)
            {
                if (_disposed) return;

                try
                {
                    // Remove existing configuration for this module
                    var keysToRemove = Data.Keys.Where(k => k.StartsWith($"Modules:{moduleName}:")).ToList();
                    foreach (var key in keysToRemove)
                    {
                        Data.Remove(key);
                    }

                    // Reload configuration for this module
                    var moduleDirectory = Path.GetDirectoryName(filePath);
                    if (moduleDirectory != null)
                    {
                        LoadModuleConfiguration(moduleName, moduleDirectory);
                    }

                    OnReload();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reloading module configuration for {moduleName}: {ex.Message}");
                }
            }
        });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            lock (_lock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    watcher.Dispose();
                }
                _watchers.Clear();
                _disposed = true;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration source for module configuration provider.
/// </summary>
public class ModuleConfigurationSource : IConfigurationSource
{
    public string ModulesPath { get; set; } = string.Empty;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ModuleConfigurationProvider(ModulesPath);
    }
}

/// <summary>
/// Extension methods for adding module configuration.
/// </summary>
public static class ModuleConfigurationExtensions
{
    /// <summary>
    /// Adds module-specific configuration from the modules directory.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="modulesPath">Path to the modules directory.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddModuleConfigurations(
        this IConfigurationBuilder builder,
        string modulesPath)
    {
        return builder.Add(new ModuleConfigurationSource
        {
            ModulesPath = modulesPath
        });
    }
}