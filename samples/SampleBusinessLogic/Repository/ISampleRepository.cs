namespace SampleBusinessLogic;

/// <summary>
/// Interface for sample repository operations.
/// </summary>
public interface ISampleRepository
{
    /// <summary>
    /// Saves sample data.
    /// </summary>
    /// <param name="data">The data to save.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveAsync(SampleData data);

    /// <summary>
    /// Gets sample data by identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The sample data if found; otherwise null.</returns>
    Task<SampleData?> GetByIdAsync(string id);

    /// <summary>
    /// Gets all sample data.
    /// </summary>
    /// <returns>A collection of sample data.</returns>
    Task<IEnumerable<SampleData>> GetAllAsync();

    /// <summary>
    /// Deletes sample data by identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    Task DeleteAsync(string id);
}