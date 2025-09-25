using Microsoft.Extensions.Logging;

namespace SampleBusinessLogic;

/// <summary>
/// Implementation of sample business service.
/// </summary>
public class SampleService : ISampleService
{
    private readonly ISampleRepository _repository;
    private readonly ILogger<SampleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleService"/> class.
    /// </summary>
    /// <param name="repository">The sample repository.</param>
    /// <param name="logger">The logger.</param>
    public SampleService(ISampleRepository repository, ILogger<SampleService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> GetSampleMessageAsync()
    {
        _logger.LogInformation("Getting sample message");

        await Task.Delay(10); // Simulate async operation

        var message = "Hello from Sample Business Logic Module!";

        _logger.LogDebug("Sample message generated: {Message}", message);

        return message;
    }

    /// <inheritdoc />
    public async Task<SampleResult> ProcessDataAsync(SampleData data)
    {
        if (data == null)
        {
            _logger.LogWarning("Attempted to process null data");
            return new SampleResult
            {
                Success = false,
                Message = "Data cannot be null"
            };
        }

        _logger.LogInformation("Processing data for ID: {Id}", data.Id);

        try
        {
            // Simulate some business logic processing
            await Task.Delay(50);

            // Save to repository
            await _repository.SaveAsync(data);

            var result = new SampleResult
            {
                Success = true,
                Message = $"Successfully processed data for {data.Name}",
                Data = new
                {
                    data.Id,
                    data.Name,
                    ProcessedValue = data.Value * 2, // Some business logic
                    data.CreatedAt
                }
            };

            _logger.LogInformation("Successfully processed data for ID: {Id}", data.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process data for ID: {Id}", data.Id);
            return new SampleResult
            {
                Success = false,
                Message = $"Failed to process data: {ex.Message}"
            };
        }
    }
}