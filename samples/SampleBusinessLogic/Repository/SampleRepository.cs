using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SampleBusinessLogic;

/// <summary>
/// In-memory implementation of sample repository for demonstration purposes.
/// In a real application, this would typically use Entity Framework, Dapper, or another data access technology.
/// </summary>
public class SampleRepository : ISampleRepository
{
    private readonly ILogger<SampleRepository> _logger;
    private readonly ConcurrentDictionary<string, SampleData> _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleRepository"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SampleRepository(ILogger<SampleRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storage = new ConcurrentDictionary<string, SampleData>();
    }

    /// <inheritdoc />
    public Task SaveAsync(SampleData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        _logger.LogDebug("Saving sample data with ID: {Id}", data.Id);

        _storage.AddOrUpdate(data.Id, data, (key, existingValue) => data);

        _logger.LogInformation("Successfully saved sample data with ID: {Id}", data.Id);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SampleData?> GetByIdAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("ID cannot be null or empty", nameof(id));

        _logger.LogDebug("Getting sample data with ID: {Id}", id);

        _storage.TryGetValue(id, out var data);

        if (data != null)
        {
            _logger.LogDebug("Found sample data with ID: {Id}", id);
        }
        else
        {
            _logger.LogDebug("Sample data not found with ID: {Id}", id);
        }

        return Task.FromResult(data);
    }

    /// <inheritdoc />
    public Task<IEnumerable<SampleData>> GetAllAsync()
    {
        _logger.LogDebug("Getting all sample data, count: {Count}", _storage.Count);

        var allData = _storage.Values.AsEnumerable();
        return Task.FromResult(allData);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("ID cannot be null or empty", nameof(id));

        _logger.LogDebug("Deleting sample data with ID: {Id}", id);

        var removed = _storage.TryRemove(id, out _);

        if (removed)
        {
            _logger.LogInformation("Successfully deleted sample data with ID: {Id}", id);
        }
        else
        {
            _logger.LogWarning("Sample data not found for deletion with ID: {Id}", id);
        }

        return Task.CompletedTask;
    }
}