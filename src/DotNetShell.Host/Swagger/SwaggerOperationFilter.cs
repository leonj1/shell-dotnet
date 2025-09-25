using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace DotNetShell.Host.Swagger;

/// <summary>
/// Custom Swagger operation filter that enhances API documentation.
/// </summary>
public class SwaggerOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies the filter to the specified operation using the given context.
    /// </summary>
    /// <param name="operation">The operation to apply the filter to.</param>
    /// <param name="context">The current operation filter context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add correlation ID header to all operations
        AddCorrelationIdHeader(operation);

        // Add common response types
        AddCommonResponseTypes(operation, context);

        // Add deprecation warnings
        AddDeprecationInfo(operation, context);

        // Add rate limiting information
        AddRateLimitingInfo(operation, context);

        // Add authentication requirements
        AddAuthenticationInfo(operation, context);

        // Enhance parameter descriptions
        EnhanceParameterDescriptions(operation, context);

        // Add example responses
        AddExampleResponses(operation, context);
    }

    private void AddCorrelationIdHeader(OpenApiOperation operation)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        // Add correlation ID parameter for all operations
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Correlation-ID",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
            Description = "Correlation ID for request tracking. If not provided, one will be generated."
        });
    }

    private void AddCommonResponseTypes(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add common error responses
        if (!operation.Responses.ContainsKey("400"))
        {
            operation.Responses.Add("400", new OpenApiResponse
            {
                Description = "Bad Request - The request was invalid or cannot be served",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "ErrorResponse"
                            }
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("401"))
        {
            operation.Responses.Add("401", new OpenApiResponse
            {
                Description = "Unauthorized - Authentication is required",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "ErrorResponse"
                            }
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("403"))
        {
            operation.Responses.Add("403", new OpenApiResponse
            {
                Description = "Forbidden - The request was valid but not authorized",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "ErrorResponse"
                            }
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse
            {
                Description = "Internal Server Error - An unexpected error occurred",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "ErrorResponse"
                            }
                        }
                    }
                }
            });
        }
    }

    private void AddDeprecationInfo(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check for ObsoleteAttribute
        var methodInfo = context.MethodInfo;
        var obsoleteAttribute = methodInfo.GetCustomAttribute<ObsoleteAttribute>();

        if (obsoleteAttribute != null)
        {
            operation.Deprecated = true;
            operation.Description = $"{operation.Description}\n\n**DEPRECATED**: {obsoleteAttribute.Message}".Trim();
        }
    }

    private void AddRateLimitingInfo(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check for rate limiting attributes (custom implementation would be needed)
        // For now, add general rate limiting information
        operation.Extensions.TryAdd("x-rate-limit", new OpenApiObject
        {
            ["enabled"] = new OpenApiBoolean(true),
            ["requests-per-minute"] = new OpenApiInteger(1000)
        });
    }

    private void AddAuthenticationInfo(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check for AllowAnonymous or Authorize attributes
        var methodInfo = context.MethodInfo;
        var controllerType = context.MethodInfo.DeclaringType;

        var allowAnonymous = methodInfo.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() != null ||
                            controllerType?.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() != null;

        var authorize = methodInfo.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() ??
                       controllerType?.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();

        if (authorize != null && !allowAnonymous)
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();

            var requirement = new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new List<string>()
                }
            };

            operation.Security.Add(requirement);

            // Add roles if specified
            if (!string.IsNullOrEmpty(authorize.Roles))
            {
                var roles = authorize.Roles.Split(',').Select(r => r.Trim()).ToArray();
                operation.Extensions.TryAdd("x-required-roles", new OpenApiArray
                {
                    Values = roles.Select(r => new OpenApiString(r)).Cast<IOpenApiAny>().ToList()
                });
            }

            // Add policy if specified
            if (!string.IsNullOrEmpty(authorize.Policy))
            {
                operation.Extensions.TryAdd("x-required-policy", new OpenApiString(authorize.Policy));
            }
        }
    }

    private void EnhanceParameterDescriptions(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var parameter in operation.Parameters ?? Enumerable.Empty<OpenApiParameter>())
        {
            // Enhance parameter descriptions based on common patterns
            if (parameter.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Description ??= "Unique identifier for the resource";
                parameter.Example = new OpenApiString("123");
            }
            else if (parameter.Name.Contains("page", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Description ??= "Page number for pagination (1-based)";
                parameter.Example = new OpenApiInteger(1);
                parameter.Schema.Minimum = 1;
            }
            else if (parameter.Name.Contains("size", StringComparison.OrdinalIgnoreCase) ||
                     parameter.Name.Contains("limit", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Description ??= "Number of items per page";
                parameter.Example = new OpenApiInteger(20);
                parameter.Schema.Minimum = 1;
                parameter.Schema.Maximum = 100;
            }
            else if (parameter.Name.Contains("sort", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Description ??= "Sort order (e.g., 'name', 'created_desc')";
                parameter.Example = new OpenApiString("created_desc");
            }
        }
    }

    private void AddExampleResponses(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add example responses for successful operations
        foreach (var response in operation.Responses.Where(r => r.Key.StartsWith("2")))
        {
            foreach (var content in response.Value.Content.Values)
            {
                if (content.Schema?.Reference?.Id != null)
                {
                    // Add examples based on schema type
                    AddSchemaExample(content, content.Schema.Reference.Id);
                }
            }
        }
    }

    private void AddSchemaExample(OpenApiMediaType mediaType, string schemaId)
    {
        // Add examples based on common schema patterns
        switch (schemaId.ToLowerInvariant())
        {
            case "errorresponse":
                mediaType.Example = new OpenApiObject
                {
                    ["statusCode"] = new OpenApiInteger(400),
                    ["error"] = new OpenApiString("Bad Request"),
                    ["message"] = new OpenApiString("The request was invalid"),
                    ["traceId"] = new OpenApiString("0HM2VQ2V3F3QD:00000001"),
                    ["timestamp"] = new OpenApiString(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                };
                break;
        }
    }
}