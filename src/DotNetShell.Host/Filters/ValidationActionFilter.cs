using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace DotNetShell.Host.Filters;

/// <summary>
/// Action filter that validates model state and returns standardized validation error responses.
/// </summary>
public class ValidationActionFilter : ActionFilterAttribute
{
    /// <summary>
    /// Called before the action method is invoked.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var validationErrors = ExtractValidationErrors(context.ModelState);
            var errorResponse = new ValidationErrorResponse
            {
                StatusCode = 400,
                Error = "Validation Failed",
                Message = "One or more validation errors occurred.",
                TraceId = context.HttpContext.TraceIdentifier,
                Timestamp = DateTime.UtcNow,
                Errors = validationErrors
            };

            context.Result = new BadRequestObjectResult(errorResponse);
        }

        base.OnActionExecuting(context);
    }

    private Dictionary<string, List<string>> ExtractValidationErrors(ModelStateDictionary modelState)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var kvp in modelState)
        {
            var key = kvp.Key;
            var modelStateEntry = kvp.Value;

            if (modelStateEntry.Errors.Count > 0)
            {
                var errorMessages = modelStateEntry.Errors
                    .Select(error => !string.IsNullOrEmpty(error.ErrorMessage)
                        ? error.ErrorMessage
                        : error.Exception?.Message ?? "Invalid value")
                    .Where(message => !string.IsNullOrEmpty(message))
                    .ToList();

                if (errorMessages.Any())
                {
                    errors[key] = errorMessages;
                }
            }
        }

        return errors;
    }
}

/// <summary>
/// Represents a validation error response with detailed field-level errors.
/// </summary>
public class ValidationErrorResponse
{
    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the error type.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the field-level validation errors.
    /// </summary>
    public Dictionary<string, List<string>> Errors { get; set; } = new Dictionary<string, List<string>>();
}