using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace DotNetShell.Host.Swagger;

/// <summary>
/// Custom Swagger schema filter that enhances model documentation.
/// </summary>
public class SwaggerSchemaFilter : ISchemaFilter
{
    /// <summary>
    /// Applies the filter to the specified schema using the given context.
    /// </summary>
    /// <param name="schema">The schema to apply the filter to.</param>
    /// <param name="context">The current schema filter context.</param>
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Add examples to schemas
        AddSchemaExamples(schema, context);

        // Enhance property descriptions
        EnhancePropertyDescriptions(schema, context);

        // Add validation information
        AddValidationInfo(schema, context);

        // Handle enums
        EnhanceEnumSchemas(schema, context);

        // Add custom extensions
        AddCustomExtensions(schema, context);
    }

    private void AddSchemaExamples(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null) return;

        // Add examples based on type
        if (context.Type == typeof(DateTime) || context.Type == typeof(DateTime?))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiString(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }
        else if (context.Type == typeof(Guid) || context.Type == typeof(Guid?))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiString(Guid.NewGuid().ToString());
        }
        else if (context.Type == typeof(string) && context.Type.Name.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiString("user@example.com");
        }
        else if (context.Type == typeof(string) && context.Type.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiString("+1-555-123-4567");
        }

        // Add examples for custom types
        AddCustomTypeExamples(schema, context);
    }

    private void AddCustomTypeExamples(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null) return;

        var typeName = context.Type.Name;

        // Add examples based on common model patterns
        switch (typeName.ToLowerInvariant())
        {
            case "errorresponse":
                schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
                {
                    ["statusCode"] = new Microsoft.OpenApi.Any.OpenApiInteger(400),
                    ["error"] = new Microsoft.OpenApi.Any.OpenApiString("Bad Request"),
                    ["message"] = new Microsoft.OpenApi.Any.OpenApiString("The request contains invalid data"),
                    ["traceId"] = new Microsoft.OpenApi.Any.OpenApiString("0HM2VQ2V3F3QD:00000001"),
                    ["timestamp"] = new Microsoft.OpenApi.Any.OpenApiString(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                };
                break;

            case "pagedresult":
            case "pagedresponse":
                if (schema.Properties != null)
                {
                    var exampleObject = new Microsoft.OpenApi.Any.OpenApiObject
                    {
                        ["totalCount"] = new Microsoft.OpenApi.Any.OpenApiInteger(100),
                        ["pageNumber"] = new Microsoft.OpenApi.Any.OpenApiInteger(1),
                        ["pageSize"] = new Microsoft.OpenApi.Any.OpenApiInteger(20),
                        ["totalPages"] = new Microsoft.OpenApi.Any.OpenApiInteger(5),
                        ["hasNextPage"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true),
                        ["hasPreviousPage"] = new Microsoft.OpenApi.Any.OpenApiBoolean(false)
                    };

                    schema.Example = exampleObject;
                }
                break;
        }
    }

    private void EnhancePropertyDescriptions(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties == null || context.Type == null) return;

        var properties = context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var propertyName = GetPropertyName(property);
            if (schema.Properties.TryGetValue(propertyName, out var propertySchema))
            {
                // Enhance description based on property name patterns
                EnhancePropertyDescription(propertySchema, property);

                // Add format information
                AddFormatInfo(propertySchema, property);

                // Add validation attributes information
                AddPropertyValidationInfo(propertySchema, property);
            }
        }
    }

    private string GetPropertyName(PropertyInfo property)
    {
        // Handle JSON property name attributes
        var jsonPropertyAttr = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
        if (jsonPropertyAttr != null)
        {
            return jsonPropertyAttr.Name;
        }

        // Default to camelCase
        return char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
    }

    private void EnhancePropertyDescription(OpenApiSchema propertySchema, PropertyInfo property)
    {
        var propertyName = property.Name.ToLowerInvariant();

        // Add descriptions for common properties
        if (string.IsNullOrEmpty(propertySchema.Description))
        {
            propertySchema.Description = propertyName switch
            {
                "id" => "Unique identifier",
                "name" => "Name of the entity",
                "title" => "Title of the entity",
                "description" => "Description of the entity",
                "createdat" => "Timestamp when the entity was created",
                "updatedat" => "Timestamp when the entity was last updated",
                "deletedat" => "Timestamp when the entity was deleted (soft delete)",
                "createdby" => "User who created the entity",
                "updatedby" => "User who last updated the entity",
                "version" => "Version number for optimistic concurrency control",
                "isactive" => "Indicates if the entity is active",
                "isenabled" => "Indicates if the entity is enabled",
                "isdeleted" => "Indicates if the entity is deleted",
                "status" => "Current status of the entity",
                "type" => "Type or category of the entity",
                "email" => "Email address",
                "phone" => "Phone number",
                "address" => "Physical address",
                "url" => "URL or web address",
                _ => null
            };
        }

        // Add XML documentation if available
        var xmlDoc = property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        if (xmlDoc != null && string.IsNullOrEmpty(propertySchema.Description))
        {
            propertySchema.Description = xmlDoc.Description;
        }
    }

    private void AddFormatInfo(OpenApiSchema propertySchema, PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        // Handle nullable types
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            propertyType = propertyType.GetGenericArguments()[0];
        }

        // Add format based on type and name
        if (propertyType == typeof(DateTime))
        {
            propertySchema.Format = "date-time";
        }
        else if (propertyType == typeof(DateOnly))
        {
            propertySchema.Format = "date";
        }
        else if (propertyType == typeof(TimeOnly))
        {
            propertySchema.Format = "time";
        }
        else if (propertyType == typeof(string))
        {
            var propertyName = property.Name.ToLowerInvariant();
            if (propertyName.Contains("email"))
                propertySchema.Format = "email";
            else if (propertyName.Contains("url") || propertyName.Contains("uri"))
                propertySchema.Format = "uri";
            else if (propertyName.Contains("phone"))
                propertySchema.Format = "tel";
            else if (propertyName.Contains("password"))
                propertySchema.Format = "password";
        }
        else if (propertyType == typeof(Guid))
        {
            propertySchema.Format = "uuid";
        }
    }

    private void AddValidationInfo(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null) return;

        // Add validation information from data annotations
        var validationAttributes = context.Type.GetCustomAttributes()
            .Where(attr => attr.GetType().Namespace == "System.ComponentModel.DataAnnotations")
            .ToList();

        foreach (var attr in validationAttributes)
        {
            // Process validation attributes and add to schema
            ProcessValidationAttribute(schema, attr);
        }
    }

    private void AddPropertyValidationInfo(OpenApiSchema propertySchema, PropertyInfo property)
    {
        var validationAttributes = property.GetCustomAttributes()
            .Where(attr => attr.GetType().Namespace == "System.ComponentModel.DataAnnotations")
            .ToList();

        foreach (var attr in validationAttributes)
        {
            ProcessValidationAttribute(propertySchema, attr);
        }
    }

    private void ProcessValidationAttribute(OpenApiSchema schema, Attribute attribute)
    {
        switch (attribute)
        {
            case System.ComponentModel.DataAnnotations.RequiredAttribute:
                // This is typically handled by the required array in the parent schema
                break;

            case System.ComponentModel.DataAnnotations.StringLengthAttribute stringLength:
                schema.MaxLength = stringLength.MaximumLength;
                if (stringLength.MinimumLength > 0)
                    schema.MinLength = stringLength.MinimumLength;
                break;

            case System.ComponentModel.DataAnnotations.MinLengthAttribute minLength:
                schema.MinLength = minLength.Length;
                break;

            case System.ComponentModel.DataAnnotations.MaxLengthAttribute maxLength:
                schema.MaxLength = maxLength.Length;
                break;

            case System.ComponentModel.DataAnnotations.RangeAttribute range:
                if (range.Minimum is int minInt)
                    schema.Minimum = minInt;
                if (range.Maximum is int maxInt)
                    schema.Maximum = maxInt;
                break;

            case System.ComponentModel.DataAnnotations.RegularExpressionAttribute regex:
                schema.Pattern = regex.Pattern;
                break;
        }
    }

    private void EnhanceEnumSchemas(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null || !context.Type.IsEnum) return;

        var enumValues = Enum.GetValues(context.Type);
        var enumNames = Enum.GetNames(context.Type);

        // Add enum descriptions
        var descriptions = new List<string>();
        for (int i = 0; i < enumNames.Length; i++)
        {
            var enumValue = enumValues.GetValue(i);
            var enumName = enumNames[i];
            var enumMember = context.Type.GetMember(enumName)[0];

            var descriptionAttr = enumMember.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            var description = descriptionAttr?.Description ?? enumName;

            descriptions.Add($"{enumValue} = {description}");
        }

        if (descriptions.Any())
        {
            schema.Description = $"{schema.Description}\n\nPossible values:\n- {string.Join("\n- ", descriptions)}".Trim();
        }
    }

    private void AddCustomExtensions(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null) return;

        // Add custom extensions based on attributes or conventions
        var typeName = context.Type.Name;

        // Add deprecation information
        var obsoleteAttr = context.Type.GetCustomAttribute<ObsoleteAttribute>();
        if (obsoleteAttr != null)
        {
            schema.Deprecated = true;
            schema.Extensions.Add("x-deprecated-message", new Microsoft.OpenApi.Any.OpenApiString(obsoleteAttr.Message ?? "This schema is deprecated"));
        }

        // Add version information if available
        var assemblyVersion = context.Type.Assembly.GetName().Version?.ToString();
        if (!string.IsNullOrEmpty(assemblyVersion))
        {
            schema.Extensions.Add("x-schema-version", new Microsoft.OpenApi.Any.OpenApiString(assemblyVersion));
        }
    }
}