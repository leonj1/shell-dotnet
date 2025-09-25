namespace DotNetShell.Abstractions.Models;

/// <summary>
/// Generic result type for operations that can succeed or fail.
/// </summary>
/// <typeparam name="T">The type of the result value on success.</typeparam>
public class Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    private Result(T? value, string? error, bool isSuccess)
    {
        _value = value;
        _error = error;
        IsSuccess = isSuccess;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the result value (only available if IsSuccess is true).
    /// </summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
                throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {_error}");

            return _value!;
        }
    }

    /// <summary>
    /// Gets the error message (only available if IsSuccess is false).
    /// </summary>
    public string Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error of a successful result.");

            return _error!;
        }
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">The result value.</param>
    /// <returns>A successful result containing the value.</returns>
    public static Result<T> Success(T value)
    {
        return new Result<T>(value, null, true);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed result containing the error.</returns>
    public static Result<T> Failure(string error)
    {
        return new Result<T>(default, error, false);
    }

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    /// <param name="action">The action to execute with the result value.</param>
    /// <returns>This result for method chaining.</returns>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(Value);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    /// <param name="action">The action to execute with the error message.</param>
    /// <returns>This result for method chaining.</returns>
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }

    /// <summary>
    /// Maps the result value to a new type if successful.
    /// </summary>
    /// <typeparam name="TNew">The new result type.</typeparam>
    /// <param name="mapper">The function to map the value.</param>
    /// <returns>A new result with the mapped value or the same error.</returns>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess ? Result<TNew>.Success(mapper(Value)) : Result<TNew>.Failure(Error);
    }

    /// <summary>
    /// Chains another operation that returns a result.
    /// </summary>
    /// <typeparam name="TNew">The new result type.</typeparam>
    /// <param name="binder">The function to bind the next operation.</param>
    /// <returns>The result of the bound operation or the current error.</returns>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return IsSuccess ? binder(Value) : Result<TNew>.Failure(Error);
    }

    /// <summary>
    /// Gets the value or returns a default value if the result is a failure.
    /// </summary>
    /// <param name="defaultValue">The default value to return on failure.</param>
    /// <returns>The result value or the default value.</returns>
    public T GetValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value : defaultValue;
    }

    /// <summary>
    /// Gets the value or executes a function to get a default value if the result is a failure.
    /// </summary>
    /// <param name="defaultValueFactory">The function to get the default value.</param>
    /// <returns>The result value or the default value from the factory.</returns>
    public T GetValueOrDefault(Func<string, T> defaultValueFactory)
    {
        return IsSuccess ? Value : defaultValueFactory(Error);
    }
}

/// <summary>
/// Result type for operations that can succeed or fail without a return value.
/// </summary>
public class Result
{
    private readonly string? _error;

    private Result(string? error, bool isSuccess)
    {
        _error = error;
        IsSuccess = isSuccess;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message (only available if IsSuccess is false).
    /// </summary>
    public string Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error of a successful result.");

            return _error!;
        }
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Success()
    {
        return new Result(null, true);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed result containing the error.</returns>
    public static Result Failure(string error)
    {
        return new Result(error, false);
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="value">The result value.</param>
    /// <returns>A successful result containing the value.</returns>
    public static Result<T> Success<T>(T value)
    {
        return Result<T>.Success(value);
    }

    /// <summary>
    /// Creates a failed result with a value type.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="error">The error message.</param>
    /// <returns>A failed result containing the error.</returns>
    public static Result<T> Failure<T>(string error)
    {
        return Result<T>.Failure(error);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>This result for method chaining.</returns>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
            action();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    /// <param name="action">The action to execute with the error message.</param>
    /// <returns>This result for method chaining.</returns>
    public Result OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }

    /// <summary>
    /// Chains another operation that returns a result.
    /// </summary>
    /// <param name="binder">The function to bind the next operation.</param>
    /// <returns>The result of the bound operation or the current error.</returns>
    public Result Bind(Func<Result> binder)
    {
        return IsSuccess ? binder() : this;
    }

    /// <summary>
    /// Chains another operation that returns a result with a value.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="binder">The function to bind the next operation.</param>
    /// <returns>The result of the bound operation or the current error.</returns>
    public Result<T> Bind<T>(Func<Result<T>> binder)
    {
        return IsSuccess ? binder() : Result<T>.Failure(Error);
    }
}

/// <summary>
/// Represents an operation result with additional metadata.
/// </summary>
/// <typeparam name="T">The type of the result value on success.</typeparam>
public class OperationResult<T>
{
    private readonly T? _value;
    private readonly string? _error;
    /// <summary>
    /// Gets the operation ID for tracking.
    /// </summary>
    public string OperationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the timestamp when the operation completed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the duration of the operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets additional metadata about the operation.
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the exception if the operation failed due to an exception.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets warnings that occurred during the operation.
    /// </summary>
    public IList<string> Warnings { get; init; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the result value (only available if IsSuccess is true).
    /// </summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
                throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {_error}");

            return _value!;
        }
    }

    /// <summary>
    /// Gets the error message (only available if IsSuccess is false).
    /// </summary>
    public string Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error of a successful result.");

            return _error!;
        }
    }

    private OperationResult(T? value, string? error, bool isSuccess, TimeSpan duration, Exception? exception = null)
    {
        _value = value;
        _error = error;
        IsSuccess = isSuccess;
        Duration = duration;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    /// <param name="value">The result value.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <param name="operationId">Optional operation ID.</param>
    /// <returns>A successful operation result.</returns>
    public static new OperationResult<T> Success(T value, TimeSpan duration = default, IDictionary<string, object>? metadata = null, IList<string>? warnings = null, string? operationId = null)
    {
        return new OperationResult<T>(value, null, true, duration)
        {
            Metadata = metadata ?? new Dictionary<string, object>(),
            Warnings = warnings ?? new List<string>(),
            OperationId = operationId ?? Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Creates a failed operation result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="exception">Optional exception that caused the failure.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="operationId">Optional operation ID.</param>
    /// <returns>A failed operation result.</returns>
    public static new OperationResult<T> Failure(string error, TimeSpan duration = default, Exception? exception = null, IDictionary<string, object>? metadata = null, string? operationId = null)
    {
        return new OperationResult<T>(default, error, false, duration, exception)
        {
            Metadata = metadata ?? new Dictionary<string, object>(),
            OperationId = operationId ?? Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Adds metadata to the operation result.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>This operation result for method chaining.</returns>
    public OperationResult<T> WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Adds a warning to the operation result.
    /// </summary>
    /// <param name="warning">The warning message.</param>
    /// <returns>This operation result for method chaining.</returns>
    public OperationResult<T> WithWarning(string warning)
    {
        Warnings.Add(warning);
        return this;
    }
}

/// <summary>
/// Extension methods for working with results.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Task&lt;Result&lt;T&gt;&gt; to support async/await patterns.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="resultTask">The task returning a result.</param>
    /// <returns>The result from the task.</returns>
    public static async Task<Result<T>> ConfigureAwait<T>(this Task<Result<T>> resultTask)
    {
        return await resultTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Converts a Task&lt;Result&gt; to support async/await patterns.
    /// </summary>
    /// <param name="resultTask">The task returning a result.</param>
    /// <returns>The result from the task.</returns>
    public static async Task<Result> ConfigureAwait(this Task<Result> resultTask)
    {
        return await resultTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an async action if the result is successful.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The async action to execute.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task<Result<T>> OnSuccessAsync<T>(this Result<T> result, Func<T, Task> action)
    {
        if (result.IsSuccess)
            await action(result.Value);
        return result;
    }

    /// <summary>
    /// Executes an async action if the result is a failure.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The async action to execute.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task<Result<T>> OnFailureAsync<T>(this Result<T> result, Func<string, Task> action)
    {
        if (result.IsFailure)
            await action(result.Error);
        return result;
    }

    /// <summary>
    /// Maps the result value to a new type asynchronously if successful.
    /// </summary>
    /// <typeparam name="T">The original result type.</typeparam>
    /// <typeparam name="TNew">The new result type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="mapper">The async function to map the value.</param>
    /// <returns>A new result with the mapped value or the same error.</returns>
    public static async Task<Result<TNew>> MapAsync<T, TNew>(this Result<T> result, Func<T, Task<TNew>> mapper)
    {
        if (result.IsSuccess)
        {
            var mappedValue = await mapper(result.Value);
            return Result<TNew>.Success(mappedValue);
        }
        return Result<TNew>.Failure(result.Error);
    }

    /// <summary>
    /// Chains another async operation that returns a result.
    /// </summary>
    /// <typeparam name="T">The original result type.</typeparam>
    /// <typeparam name="TNew">The new result type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="binder">The async function to bind the next operation.</param>
    /// <returns>The result of the bound operation or the current error.</returns>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(this Result<T> result, Func<T, Task<Result<TNew>>> binder)
    {
        return result.IsSuccess ? await binder(result.Value) : Result<TNew>.Failure(result.Error);
    }

    /// <summary>
    /// Combines multiple results into a single result.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="results">The results to combine.</param>
    /// <returns>A successful result with all values if all are successful, otherwise the first failure.</returns>
    public static Result<IEnumerable<T>> Combine<T>(this IEnumerable<Result<T>> results)
    {
        var resultList = results.ToList();
        var failures = resultList.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var combinedError = string.Join("; ", failures.Select(f => f.Error));
            return Result<IEnumerable<T>>.Failure(combinedError);
        }

        var values = resultList.Select(r => r.Value);
        return Result<IEnumerable<T>>.Success(values);
    }

    /// <summary>
    /// Converts an exception to a failed result.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="exception">The exception to convert.</param>
    /// <returns>A failed result containing the exception message.</returns>
    public static Result<T> ToResult<T>(this Exception exception)
    {
        return Result<T>.Failure(exception.Message);
    }

    /// <summary>
    /// Executes a function and catches any exceptions, returning them as failed results.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <returns>A result containing the function result or any exception that occurred.</returns>
    public static Result<T> Try<T>(Func<T> function)
    {
        try
        {
            return Result<T>.Success(function());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async function and catches any exceptions, returning them as failed results.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="function">The async function to execute.</param>
    /// <returns>A result containing the function result or any exception that occurred.</returns>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> function)
    {
        try
        {
            var result = await function();
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex.Message);
        }
    }
}

/// <summary>
/// Represents validation errors for a model.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IList<ValidationError> Errors { get; init; } = new List<ValidationError>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful validation result.</returns>
    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(IList<ValidationError> errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    /// <param name="propertyName">The property name that failed validation.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(string propertyName, string errorMessage)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = new List<ValidationError>
            {
                new ValidationError(propertyName, errorMessage)
            }
        };
    }
}

/// <summary>
/// Represents a validation error for a specific property.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="errorMessage">The error message.</param>
    public ValidationError(string propertyName, string errorMessage)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the property name that failed validation.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets or sets additional error metadata.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the severity of the error.
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

/// <summary>
/// Enumeration of validation severity levels.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning message.
    /// </summary>
    Warning,

    /// <summary>
    /// Error message.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error message.
    /// </summary>
    Critical
}