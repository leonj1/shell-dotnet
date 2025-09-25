using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotNetShell.Abstractions;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Service responsible for extracting metadata from plugin assemblies and embedded resources.
/// </summary>
public class PluginMetadataReader : IPluginMetadataReader
{
    private readonly ILogger<PluginMetadataReader> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginMetadataReader"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public PluginMetadataReader(ILogger<PluginMetadataReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Reads comprehensive metadata from a plugin assembly.
    /// </summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The extracted plugin metadata.</returns>
    public async Task<PluginMetadata> ReadMetadataAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        _logger.LogDebug("Reading metadata from assembly: {AssemblyPath}", assemblyPath);

        try
        {
            var metadata = new PluginMetadata
            {
                AssemblyPath = assemblyPath,
                ExtractedAt = DateTime.UtcNow
            };

            using var loadContext = new PluginLoadContext(assemblyPath, isCollectible: true);
            var assembly = loadContext.LoadPluginAssembly();

            // Extract basic assembly metadata
            await ExtractAssemblyMetadataAsync(assembly, metadata, cancellationToken);

            // Extract embedded manifest
            await ExtractEmbeddedManifestAsync(assembly, metadata, cancellationToken);

            // Extract module metadata
            await ExtractModuleMetadataAsync(assembly, metadata, cancellationToken);

            // Extract dependency information
            await ExtractDependencyMetadataAsync(assembly, metadata, cancellationToken);

            // Extract resource information
            await ExtractResourceMetadataAsync(assembly, metadata, cancellationToken);

            // Extract custom attributes
            await ExtractCustomAttributesAsync(assembly, metadata, cancellationToken);

            _logger.LogDebug("Successfully extracted metadata from assembly: {AssemblyPath}", assemblyPath);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read metadata from assembly: {AssemblyPath}", assemblyPath);
            throw new PluginLoadException($"Failed to read metadata from assembly '{assemblyPath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads metadata from a plugin manifest embedded as a resource.
    /// </summary>
    /// <param name="assembly">The plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The embedded manifest, or null if not found.</returns>
    public async Task<PluginManifest?> ReadEmbeddedManifestAsync(Assembly assembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            _logger.LogTrace("Looking for embedded manifest in assembly: {AssemblyName}", assembly.GetName().Name);

            var manifestResourceNames = new[]
            {
                "plugin.json",
                "manifest.json",
                "module.json",
                $"{assembly.GetName().Name}.plugin.json",
                $"{assembly.GetName().Name}.manifest.json"
            };

            foreach (var resourceName in manifestResourceNames)
            {
                var fullResourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

                if (fullResourceName != null)
                {
                    _logger.LogTrace("Found embedded manifest resource: {ResourceName}", fullResourceName);

                    using var stream = assembly.GetManifestResourceStream(fullResourceName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var manifestContent = await reader.ReadToEndAsync(cancellationToken);

                        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestContent, _jsonOptions);
                        if (manifest != null)
                        {
                            _logger.LogDebug("Successfully loaded embedded manifest from: {ResourceName}", fullResourceName);
                            return manifest;
                        }
                    }
                }
            }

            _logger.LogTrace("No embedded manifest found in assembly: {AssemblyName}", assembly.GetName().Name);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading embedded manifest from assembly: {AssemblyName}", assembly.GetName().Name);
            return null;
        }
    }

    /// <summary>
    /// Extracts version information from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to analyze.</param>
    /// <returns>The version information.</returns>
    public AssemblyVersionInfo ExtractVersionInfo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            var assemblyName = assembly.GetName();
            var versionInfo = new AssemblyVersionInfo
            {
                AssemblyVersion = assemblyName.Version?.ToString(),
                FileVersion = GetFileVersion(assembly),
                InformationalVersion = GetInformationalVersion(assembly),
                ProductVersion = GetProductVersion(assembly)
            };

            return versionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting version info from assembly: {AssemblyName}", assembly.GetName().Name);
            return new AssemblyVersionInfo();
        }
    }

    /// <summary>
    /// Extracts dependency information from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to analyze.</param>
    /// <returns>The dependency information.</returns>
    public IEnumerable<AssemblyDependencyInfo> ExtractDependencyInfo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            var dependencies = new List<AssemblyDependencyInfo>();
            var referencedAssemblies = assembly.GetReferencedAssemblies();

            foreach (var reference in referencedAssemblies)
            {
                var dependencyInfo = new AssemblyDependencyInfo
                {
                    Name = reference.Name ?? "Unknown",
                    Version = reference.Version?.ToString(),
                    PublicKeyToken = reference.GetPublicKeyToken()?.Length > 0
                        ? Convert.ToHexString(reference.GetPublicKeyToken())
                        : null,
                    Culture = string.IsNullOrEmpty(reference.CultureName) ? "neutral" : reference.CultureName,
                    ProcessorArchitecture = reference.ProcessorArchitecture.ToString()
                };

                dependencies.Add(dependencyInfo);
            }

            return dependencies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting dependency info from assembly: {AssemblyName}", assembly.GetName().Name);
            return Array.Empty<AssemblyDependencyInfo>();
        }
    }

    private async Task ExtractAssemblyMetadataAsync(Assembly assembly, PluginMetadata metadata, CancellationToken cancellationToken)
    {
        var assemblyName = assembly.GetName();

        metadata.AssemblyName = assemblyName.Name ?? "Unknown";
        metadata.AssemblyVersion = assemblyName.Version?.ToString() ?? "Unknown";
        metadata.AssemblyFullName = assembly.FullName ?? "Unknown";
        metadata.Location = assembly.Location;

        // Extract assembly attributes
        metadata.Title = GetAssemblyAttribute<AssemblyTitleAttribute>(assembly)?.Title;
        metadata.Description = GetAssemblyAttribute<AssemblyDescriptionAttribute>(assembly)?.Description;
        metadata.Company = GetAssemblyAttribute<AssemblyCompanyAttribute>(assembly)?.Company;
        metadata.Product = GetAssemblyAttribute<AssemblyProductAttribute>(assembly)?.Product;
        metadata.Copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>(assembly)?.Copyright;
        metadata.FileVersion = GetAssemblyAttribute<AssemblyFileVersionAttribute>(assembly)?.Version;
        metadata.InformationalVersion = GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion;

        // Extract target framework
        var targetFrameworkAttribute = GetAssemblyAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>(assembly);
        if (targetFrameworkAttribute != null)
        {
            metadata.TargetFramework = targetFrameworkAttribute.FrameworkName;
            metadata.TargetFrameworkDisplayName = targetFrameworkAttribute.FrameworkDisplayName;
        }

        // Extract build metadata
        metadata.BuildMetadata = ExtractBuildMetadata(assembly);
    }

    private async Task ExtractEmbeddedManifestAsync(Assembly assembly, PluginMetadata metadata, CancellationToken cancellationToken)
    {
        var embeddedManifest = await ReadEmbeddedManifestAsync(assembly, cancellationToken);
        if (embeddedManifest != null)
        {
            metadata.EmbeddedManifest = embeddedManifest;
            metadata.HasEmbeddedManifest = true;

            _logger.LogDebug("Found embedded manifest in assembly: {AssemblyName}", assembly.GetName().Name);
        }
    }

    private async Task ExtractModuleMetadataAsync(Assembly assembly, PluginMetadata metadata, CancellationToken cancellationToken)
    {
        try
        {
            // Find IBusinessLogicModule implementations
            var moduleTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IBusinessLogicModule).IsAssignableFrom(t))
                .ToList();

            metadata.ModuleTypes = moduleTypes.Select(t => new ModuleTypeInfo
            {
                FullName = t.FullName ?? t.Name,
                Name = t.Name,
                IsPublic = t.IsPublic,
                HasParameterlessConstructor = t.GetConstructor(Type.EmptyTypes) != null,
                Attributes = t.GetCustomAttributes(false).Select(attr => attr.GetType().Name).ToList()
            }).ToList();

            if (moduleTypes.Count > 0)
            {
                metadata.PrimaryModuleType = metadata.ModuleTypes.First();
                _logger.LogDebug("Found {Count} module types in assembly: {AssemblyName}",
                    moduleTypes.Count, assembly.GetName().Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting module metadata from assembly: {AssemblyName}", assembly.GetName().Name);
        }
    }

    private async Task ExtractDependencyMetadataAsync(Assembly assembly, PluginMetadata metadata, CancellationToken cancellationToken)
    {
        metadata.Dependencies = ExtractDependencyInfo(assembly).ToList();
        metadata.DependencyCount = metadata.Dependencies.Count;

        _logger.LogTrace("Extracted {Count} dependencies from assembly: {AssemblyName}",
            metadata.Dependencies.Count, assembly.GetName().Name);
    }

    private async Task ExtractResourceMetadataAsync(Assembly assembly, PluginMetadata metadata, CancellationToken cancellationToken)
    {
        try
        {
            var resourceNames = assembly.GetManifestResourceNames();
            metadata.EmbeddedResources = resourceNames.Select(name => new EmbeddedResourceInfo
            {
                Name = name,
                Size = GetResourceSize(assembly, name)
            }).ToList();

            metadata.ResourceCount = metadata.EmbeddedResources.Count;

            _logger.LogTrace("Found {Count} embedded resources in assembly: {AssemblyName}",
                metadata.EmbeddedResources.Count, assembly.GetName().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting resource metadata from assembly: {AssemblyName}", assembly.GetName().Name);
        }
    }

    private async Task ExtractCustomAttributesAsync(Assembly assembly, PluginMetadata metadata, CancellationToken cancellationToken)
    {
        try
        {
            var customAttributes = assembly.GetCustomAttributesData();
            metadata.CustomAttributes = customAttributes.Select(attr => new CustomAttributeInfo
            {
                TypeName = attr.AttributeType.FullName ?? attr.AttributeType.Name,
                Arguments = attr.ConstructorArguments.Select(arg => arg.Value?.ToString()).ToList(),
                NamedArguments = attr.NamedArguments.ToDictionary(
                    arg => arg.MemberName,
                    arg => arg.TypedValue.Value?.ToString()
                )
            }).ToList();

            _logger.LogTrace("Extracted {Count} custom attributes from assembly: {AssemblyName}",
                metadata.CustomAttributes.Count, assembly.GetName().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting custom attributes from assembly: {AssemblyName}", assembly.GetName().Name);
        }
    }

    private T? GetAssemblyAttribute<T>(Assembly assembly) where T : Attribute
    {
        return assembly.GetCustomAttribute<T>();
    }

    private string? GetFileVersion(Assembly assembly)
    {
        return GetAssemblyAttribute<AssemblyFileVersionAttribute>(assembly)?.Version;
    }

    private string? GetInformationalVersion(Assembly assembly)
    {
        return GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion;
    }

    private string? GetProductVersion(Assembly assembly)
    {
        // Try to get from AssemblyInformationalVersionAttribute first, then fallback to file version
        return GetInformationalVersion(assembly) ?? GetFileVersion(assembly);
    }

    private BuildMetadata ExtractBuildMetadata(Assembly assembly)
    {
        var metadata = new BuildMetadata();

        try
        {
            // Look for common build metadata attributes
            var metadataAttribute = assembly.GetCustomAttributes(false)
                .FirstOrDefault(attr => attr.GetType().Name.Contains("Metadata"));

            if (metadataAttribute != null)
            {
                // Extract build information from metadata attribute
                var properties = metadataAttribute.GetType().GetProperties();
                foreach (var property in properties)
                {
                    try
                    {
                        var value = property.GetValue(metadataAttribute)?.ToString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            switch (property.Name.ToLower())
                            {
                                case "builddate":
                                case "buildtime":
                                    if (DateTime.TryParse(value, out var buildTime))
                                        metadata.BuildTime = buildTime;
                                    break;
                                case "buildnumber":
                                    metadata.BuildNumber = value;
                                    break;
                                case "commitid":
                                case "commithash":
                                    metadata.CommitHash = value;
                                    break;
                                case "branch":
                                    metadata.Branch = value;
                                    break;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore individual property extraction errors
                    }
                }
            }

            // Try to extract from file system metadata
            if (File.Exists(assembly.Location))
            {
                var fileInfo = new FileInfo(assembly.Location);
                metadata.FileSize = fileInfo.Length;
                metadata.FileCreatedTime = fileInfo.CreationTimeUtc;
                metadata.FileModifiedTime = fileInfo.LastWriteTimeUtc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not extract build metadata from assembly: {AssemblyName}", assembly.GetName().Name);
        }

        return metadata;
    }

    private long GetResourceSize(Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            return stream?.Length ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Interface for plugin metadata reader service.
/// </summary>
public interface IPluginMetadataReader
{
    /// <summary>
    /// Reads comprehensive metadata from a plugin assembly.
    /// </summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The extracted plugin metadata.</returns>
    Task<PluginMetadata> ReadMetadataAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads metadata from a plugin manifest embedded as a resource.
    /// </summary>
    /// <param name="assembly">The plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The embedded manifest, or null if not found.</returns>
    Task<PluginManifest?> ReadEmbeddedManifestAsync(Assembly assembly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts version information from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to analyze.</param>
    /// <returns>The version information.</returns>
    AssemblyVersionInfo ExtractVersionInfo(Assembly assembly);

    /// <summary>
    /// Extracts dependency information from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to analyze.</param>
    /// <returns>The dependency information.</returns>
    IEnumerable<AssemblyDependencyInfo> ExtractDependencyInfo(Assembly assembly);
}

/// <summary>
/// Comprehensive metadata extracted from a plugin assembly.
/// </summary>
public class PluginMetadata
{
    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly version.
    /// </summary>
    public string AssemblyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full assembly name.
    /// </summary>
    public string AssemblyFullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly location.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the assembly description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the assembly company.
    /// </summary>
    public string? Company { get; set; }

    /// <summary>
    /// Gets or sets the assembly product.
    /// </summary>
    public string? Product { get; set; }

    /// <summary>
    /// Gets or sets the assembly copyright.
    /// </summary>
    public string? Copyright { get; set; }

    /// <summary>
    /// Gets or sets the file version.
    /// </summary>
    public string? FileVersion { get; set; }

    /// <summary>
    /// Gets or sets the informational version.
    /// </summary>
    public string? InformationalVersion { get; set; }

    /// <summary>
    /// Gets or sets the target framework.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the target framework display name.
    /// </summary>
    public string? TargetFrameworkDisplayName { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly has an embedded manifest.
    /// </summary>
    public bool HasEmbeddedManifest { get; set; }

    /// <summary>
    /// Gets or sets the embedded manifest.
    /// </summary>
    public PluginManifest? EmbeddedManifest { get; set; }

    /// <summary>
    /// Gets or sets the module types found in the assembly.
    /// </summary>
    public List<ModuleTypeInfo> ModuleTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the primary module type.
    /// </summary>
    public ModuleTypeInfo? PrimaryModuleType { get; set; }

    /// <summary>
    /// Gets or sets the assembly dependencies.
    /// </summary>
    public List<AssemblyDependencyInfo> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the dependency count.
    /// </summary>
    public int DependencyCount { get; set; }

    /// <summary>
    /// Gets or sets the embedded resources.
    /// </summary>
    public List<EmbeddedResourceInfo> EmbeddedResources { get; set; } = new();

    /// <summary>
    /// Gets or sets the resource count.
    /// </summary>
    public int ResourceCount { get; set; }

    /// <summary>
    /// Gets or sets the custom attributes.
    /// </summary>
    public List<CustomAttributeInfo> CustomAttributes { get; set; } = new();

    /// <summary>
    /// Gets or sets the build metadata.
    /// </summary>
    public BuildMetadata BuildMetadata { get; set; } = new();

    /// <summary>
    /// Gets or sets when the metadata was extracted.
    /// </summary>
    public DateTime ExtractedAt { get; set; }

    /// <summary>
    /// Gets or sets additional metadata properties.
    /// </summary>
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

/// <summary>
/// Information about a module type found in an assembly.
/// </summary>
public class ModuleTypeInfo
{
    /// <summary>
    /// Gets or sets the full type name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the type is public.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Gets or sets whether the type has a parameterless constructor.
    /// </summary>
    public bool HasParameterlessConstructor { get; set; }

    /// <summary>
    /// Gets or sets the type attributes.
    /// </summary>
    public List<string> Attributes { get; set; } = new();
}

/// <summary>
/// Information about an assembly dependency.
/// </summary>
public class AssemblyDependencyInfo
{
    /// <summary>
    /// Gets or sets the dependency name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dependency version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the public key token.
    /// </summary>
    public string? PublicKeyToken { get; set; }

    /// <summary>
    /// Gets or sets the culture.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets the processor architecture.
    /// </summary>
    public string? ProcessorArchitecture { get; set; }
}

/// <summary>
/// Information about an embedded resource.
/// </summary>
public class EmbeddedResourceInfo
{
    /// <summary>
    /// Gets or sets the resource name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resource size in bytes.
    /// </summary>
    public long Size { get; set; }
}

/// <summary>
/// Information about a custom attribute.
/// </summary>
public class CustomAttributeInfo
{
    /// <summary>
    /// Gets or sets the attribute type name.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the constructor arguments.
    /// </summary>
    public List<string?> Arguments { get; set; } = new();

    /// <summary>
    /// Gets or sets the named arguments.
    /// </summary>
    public Dictionary<string, string?> NamedArguments { get; set; } = new();
}

/// <summary>
/// Build metadata information.
/// </summary>
public class BuildMetadata
{
    /// <summary>
    /// Gets or sets the build time.
    /// </summary>
    public DateTime? BuildTime { get; set; }

    /// <summary>
    /// Gets or sets the build number.
    /// </summary>
    public string? BuildNumber { get; set; }

    /// <summary>
    /// Gets or sets the commit hash.
    /// </summary>
    public string? CommitHash { get; set; }

    /// <summary>
    /// Gets or sets the branch name.
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// Gets or sets the file size.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the file created time.
    /// </summary>
    public DateTime FileCreatedTime { get; set; }

    /// <summary>
    /// Gets or sets the file modified time.
    /// </summary>
    public DateTime FileModifiedTime { get; set; }
}

/// <summary>
/// Assembly version information.
/// </summary>
public class AssemblyVersionInfo
{
    /// <summary>
    /// Gets or sets the assembly version.
    /// </summary>
    public string? AssemblyVersion { get; set; }

    /// <summary>
    /// Gets or sets the file version.
    /// </summary>
    public string? FileVersion { get; set; }

    /// <summary>
    /// Gets or sets the informational version.
    /// </summary>
    public string? InformationalVersion { get; set; }

    /// <summary>
    /// Gets or sets the product version.
    /// </summary>
    public string? ProductVersion { get; set; }
}