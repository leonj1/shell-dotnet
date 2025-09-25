namespace SampleBusinessLogic.Services;

/// <summary>
/// Interface for sample business service.
/// </summary>
public interface ISampleService
{
    /// <summary>
    /// Gets a sample message.
    /// </summary>
    /// <returns>A sample message string.</returns>
    Task<string> GetSampleMessageAsync();

    /// <summary>
    /// Processes sample data.
    /// </summary>
    /// <param name="data">The data to process.</param>
    /// <returns>The processed result.</returns>
    Task<SampleResult> ProcessDataAsync(SampleData data);
}

/// <summary>
/// Sample data model.
/// </summary>
public class SampleData
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sample result model.
/// </summary>
public class SampleResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processed data.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the processing timestamp.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}