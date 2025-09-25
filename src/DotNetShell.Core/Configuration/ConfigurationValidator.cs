using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DotNetShell.Abstractions.Services;
using System.Text.Json;

namespace DotNetShell.Core.Configuration;

/// <summary>
/// Interface for configuration validation services.
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Validates a configuration section against a schema type.
    /// </summary>
    /// <param name="section">The configuration section to validate.</param>
    /// <param name="schemaType">The type that defines the validation schema.</param>
    /// <returns>A validation result.</returns>
    ConfigurationValidationResult Validate(IConfigurationSection section, Type schemaType);

    /// <summary>
    /// Validates a configuration object instance.
    /// </summary>
    /// <param name="instance">The configuration object to validate.</param>
    /// <param name="sectionKey">The configuration section key.</param>
    /// <returns>A validation result.</returns>
    ConfigurationValidationResult ValidateInstance(object instance, string sectionKey);

    /// <summary>
    /// Validates all registered configuration sections.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>A collection of validation results.</returns>
    IEnumerable<ConfigurationValidationResult> ValidateAll(IConfiguration configuration);
}

/// <summary>
/// Implementation of configuration validation using data annotations and custom validation rules.
/// </summary>
public class ConfigurationValidator : IConfigurationValidator
{
    private readonly Dictionary<Type, ConfigurationSchemaInfo> _schemaCache;
    private readonly List<IConfigurationValidationRule> _globalRules;

    public ConfigurationValidator()
    {
        _schemaCache = new Dictionary<Type, ConfigurationSchemaInfo>();
        _globalRules = new List<IConfigurationValidationRule>();

        // Add default global validation rules
        AddGlobalRule(new RequiredFieldsValidationRule());
        AddGlobalRule(new DataTypeValidationRule());
        AddGlobalRule(new RangeValidationRule());
        AddGlobalRule(new RegexValidationRule());
    }

    /// <summary>
    /// Adds a global validation rule.
    /// </summary>
    /// <param name="rule">The validation rule to add.</param>
    public void AddGlobalRule(IConfigurationValidationRule rule)
    {
        _globalRules.Add(rule);
    }

    /// <inheritdoc />
    public ConfigurationValidationResult Validate(IConfigurationSection section, Type schemaType)
    {
        try
        {
            // Create an instance of the schema type and bind the configuration
            var instance = Activator.CreateInstance(schemaType);
            if (instance == null)
            {
                return ConfigurationValidationResult.Failure(
                    section.Key,
                    new List<ConfigurationValidationError>
                    {
                        new ConfigurationValidationError
                        {
                            Key = section.Key,
                            Message = $"Could not create instance of type {schemaType.Name}",
                            ErrorCode = "INSTANTIATION_ERROR"
                        }
                    },
                    schemaType);
            }

            section.Bind(instance);
            return ValidateInstance(instance, section.Key);
        }
        catch (Exception ex)
        {
            return ConfigurationValidationResult.Failure(
                section.Key,
                new List<ConfigurationValidationError>
                {
                    new ConfigurationValidationError
                    {
                        Key = section.Key,
                        Message = $"Validation failed: {ex.Message}",
                        ErrorCode = "VALIDATION_EXCEPTION"
                    }
                },
                schemaType);
        }
    }

    /// <inheritdoc />
    public ConfigurationValidationResult ValidateInstance(object instance, string sectionKey)
    {
        if (instance == null)
        {
            return ConfigurationValidationResult.Failure(
                sectionKey,
                new List<ConfigurationValidationError>
                {
                    new ConfigurationValidationError
                    {
                        Key = sectionKey,
                        Message = "Instance cannot be null",
                        ErrorCode = "NULL_INSTANCE"
                    }
                });
        }

        var schemaType = instance.GetType();
        var schemaInfo = GetOrCreateSchemaInfo(schemaType);
        var errors = new List<ConfigurationValidationError>();
        var warnings = new List<ConfigurationValidationWarning>();

        // Validate using data annotations
        var validationContext = new ValidationContext(instance);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(instance, validationContext, validationResults, true))
        {
            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    errors.Add(new ConfigurationValidationError
                    {
                        Key = $"{sectionKey}:{memberName}",
                        Message = validationResult.ErrorMessage ?? "Validation error",
                        ErrorCode = "DATA_ANNOTATION_ERROR"
                    });
                }
            }
        }

        // Apply custom validation rules
        foreach (var rule in _globalRules)
        {
            var ruleResult = rule.Validate(instance, sectionKey, schemaInfo);
            errors.AddRange(ruleResult.Errors);
            warnings.AddRange(ruleResult.Warnings);
        }

        // Apply schema-specific validation rules
        foreach (var rule in schemaInfo.ValidationRules)
        {
            var ruleResult = rule.Validate(instance, sectionKey, schemaInfo);
            errors.AddRange(ruleResult.Errors);
            warnings.AddRange(ruleResult.Warnings);
        }

        return errors.Count == 0
            ? ConfigurationValidationResult.Success(sectionKey, schemaType, warnings)
            : ConfigurationValidationResult.Failure(sectionKey, errors, schemaType, warnings);
    }

    /// <inheritdoc />
    public IEnumerable<ConfigurationValidationResult> ValidateAll(IConfiguration configuration)
    {
        var results = new List<ConfigurationValidationResult>();

        // This would require a registry of all configuration sections to validate
        // For now, we'll implement a basic approach that validates known sections
        var knownSections = new Dictionary<string, Type>();

        // Add common configuration sections
        // These would typically be registered during application startup
        // knownSections["Shell"] = typeof(ShellOptions);
        // knownSections["Shell:Services:Authentication"] = typeof(AuthenticationOptions);

        foreach (var kvp in knownSections)
        {
            var section = configuration.GetSection(kvp.Key);
            if (section.Exists())
            {
                results.Add(Validate(section, kvp.Value));
            }
        }

        return results;
    }

    private ConfigurationSchemaInfo GetOrCreateSchemaInfo(Type schemaType)
    {
        if (_schemaCache.TryGetValue(schemaType, out var cachedInfo))
        {
            return cachedInfo;
        }

        var schemaInfo = new ConfigurationSchemaInfo
        {
            SchemaType = schemaType,
            Properties = new List<ConfigurationPropertyInfo>(),
            ValidationRules = new List<IConfigurationValidationRule>()
        };

        // Analyze the type for validation attributes and metadata
        foreach (var property in schemaType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propertyInfo = new ConfigurationPropertyInfo
            {
                Name = property.Name,
                Type = property.PropertyType,
                IsRequired = property.GetCustomAttribute<RequiredAttribute>() != null,
                ValidationAttributes = property.GetCustomAttributes<ValidationAttribute>().ToList()
            };

            // Check for custom validation attributes
            var configValidationAttr = property.GetCustomAttribute<ConfigurationValidationAttribute>();
            if (configValidationAttr != null)
            {
                propertyInfo.Description = configValidationAttr.Description;
                propertyInfo.Category = configValidationAttr.Category;
                propertyInfo.IsSensitive = configValidationAttr.IsSensitive;
            }

            schemaInfo.Properties.Add(propertyInfo);
        }

        // Check for class-level validation attributes
        var classValidationAttrs = schemaType.GetCustomAttributes<IConfigurationValidationRule>();
        schemaInfo.ValidationRules.AddRange(classValidationAttrs);

        _schemaCache[schemaType] = schemaInfo;
        return schemaInfo;
    }
}

/// <summary>
/// Attribute for configuration validation metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class ConfigurationValidationAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the description of the configuration property.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category of the configuration property.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets whether the configuration property contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets whether the configuration property supports hot reload.
    /// </summary>
    public bool SupportsHotReload { get; set; } = true;
}

/// <summary>
/// Interface for configuration validation rules.
/// </summary>
public interface IConfigurationValidationRule
{
    /// <summary>
    /// Validates a configuration instance.
    /// </summary>
    /// <param name="instance">The configuration instance to validate.</param>
    /// <param name="sectionKey">The configuration section key.</param>
    /// <param name="schemaInfo">Schema information for the configuration type.</param>
    /// <returns>A validation rule result.</returns>
    ConfigurationValidationRuleResult Validate(object instance, string sectionKey, ConfigurationSchemaInfo schemaInfo);
}

/// <summary>
/// Result of a configuration validation rule.
/// </summary>
public class ConfigurationValidationRuleResult
{
    public List<ConfigurationValidationError> Errors { get; set; } = new();
    public List<ConfigurationValidationWarning> Warnings { get; set; } = new();
}

/// <summary>
/// Schema information for a configuration type.
/// </summary>
public class ConfigurationSchemaInfo
{
    public Type SchemaType { get; set; } = typeof(object);
    public List<ConfigurationPropertyInfo> Properties { get; set; } = new();
    public List<IConfigurationValidationRule> ValidationRules { get; set; } = new();
}

/// <summary>
/// Information about a configuration property.
/// </summary>
public class ConfigurationPropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(object);
    public bool IsRequired { get; set; }
    public List<ValidationAttribute> ValidationAttributes { get; set; } = new();
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Validation rule for required fields.
/// </summary>
public class RequiredFieldsValidationRule : IConfigurationValidationRule
{
    public ConfigurationValidationRuleResult Validate(object instance, string sectionKey, ConfigurationSchemaInfo schemaInfo)
    {
        var result = new ConfigurationValidationRuleResult();

        foreach (var property in schemaInfo.Properties.Where(p => p.IsRequired))
        {
            var value = schemaInfo.SchemaType.GetProperty(property.Name)?.GetValue(instance);
            if (value == null || (value is string str && string.IsNullOrEmpty(str)))
            {
                result.Errors.Add(new ConfigurationValidationError
                {
                    Key = $"{sectionKey}:{property.Name}",
                    Message = $"Required field '{property.Name}' is missing or empty",
                    ErrorCode = "REQUIRED_FIELD_MISSING"
                });
            }
        }

        return result;
    }
}

/// <summary>
/// Validation rule for data type validation.
/// </summary>
public class DataTypeValidationRule : IConfigurationValidationRule
{
    public ConfigurationValidationRuleResult Validate(object instance, string sectionKey, ConfigurationSchemaInfo schemaInfo)
    {
        var result = new ConfigurationValidationRuleResult();

        foreach (var property in schemaInfo.Properties)
        {
            var value = schemaInfo.SchemaType.GetProperty(property.Name)?.GetValue(instance);
            if (value != null && !property.Type.IsAssignableFrom(value.GetType()))
            {
                result.Errors.Add(new ConfigurationValidationError
                {
                    Key = $"{sectionKey}:{property.Name}",
                    Message = $"Value '{value}' is not of expected type '{property.Type.Name}'",
                    Expected = property.Type.Name,
                    Actual = value.GetType().Name,
                    ErrorCode = "DATA_TYPE_MISMATCH"
                });
            }
        }

        return result;
    }
}

/// <summary>
/// Validation rule for range validation.
/// </summary>
public class RangeValidationRule : IConfigurationValidationRule
{
    public ConfigurationValidationRuleResult Validate(object instance, string sectionKey, ConfigurationSchemaInfo schemaInfo)
    {
        var result = new ConfigurationValidationRuleResult();

        foreach (var property in schemaInfo.Properties)
        {
            var rangeAttribute = property.ValidationAttributes.OfType<RangeAttribute>().FirstOrDefault();
            if (rangeAttribute != null)
            {
                var value = schemaInfo.SchemaType.GetProperty(property.Name)?.GetValue(instance);
                if (value != null && !rangeAttribute.IsValid(value))
                {
                    result.Errors.Add(new ConfigurationValidationError
                    {
                        Key = $"{sectionKey}:{property.Name}",
                        Message = $"Value '{value}' is not within the expected range [{rangeAttribute.Minimum}-{rangeAttribute.Maximum}]",
                        Expected = $"[{rangeAttribute.Minimum}-{rangeAttribute.Maximum}]",
                        Actual = value.ToString(),
                        ErrorCode = "VALUE_OUT_OF_RANGE"
                    });
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Validation rule for regular expression validation.
/// </summary>
public class RegexValidationRule : IConfigurationValidationRule
{
    public ConfigurationValidationRuleResult Validate(object instance, string sectionKey, ConfigurationSchemaInfo schemaInfo)
    {
        var result = new ConfigurationValidationRuleResult();

        foreach (var property in schemaInfo.Properties)
        {
            var regexAttribute = property.ValidationAttributes.OfType<RegularExpressionAttribute>().FirstOrDefault();
            if (regexAttribute != null)
            {
                var value = schemaInfo.SchemaType.GetProperty(property.Name)?.GetValue(instance);
                if (value is string stringValue && !regexAttribute.IsValid(stringValue))
                {
                    result.Errors.Add(new ConfigurationValidationError
                    {
                        Key = $"{sectionKey}:{property.Name}",
                        Message = $"Value '{stringValue}' does not match the expected pattern",
                        Expected = regexAttribute.Pattern,
                        Actual = stringValue,
                        ErrorCode = "REGEX_PATTERN_MISMATCH"
                    });
                }
            }
        }

        return result;
    }
}