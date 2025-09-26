using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SampleBusinessLogic.Services;

namespace SampleBusinessLogic.Controllers;

/// <summary>
/// Sample controller demonstrating API endpoints in a business module.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SampleController : ControllerBase
{
    private readonly ISampleService _sampleService;
    private readonly ILogger<SampleController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleController"/> class.
    /// </summary>
    /// <param name="sampleService">The sample service.</param>
    /// <param name="logger">The logger.</param>
    public SampleController(ISampleService sampleService, ILogger<SampleController> logger)
    {
        _sampleService = sampleService ?? throw new ArgumentNullException(nameof(sampleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a sample message from the business module.
    /// </summary>
    /// <returns>A sample message.</returns>
    [HttpGet("message")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<ActionResult<string>> GetMessage()
    {
        _logger.LogInformation("Getting sample message");

        try
        {
            var message = await _sampleService.GetSampleMessageAsync();
            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sample message");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while getting the message");
        }
    }

    /// <summary>
    /// Processes sample data.
    /// </summary>
    /// <param name="data">The sample data to process.</param>
    /// <returns>The processing result.</returns>
    [HttpPost("process")]
    [ProducesResponseType(typeof(SampleResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SampleResult>> ProcessData([FromBody] SampleData data)
    {
        if (data == null)
        {
            _logger.LogWarning("Received null data for processing");
            return BadRequest("Data cannot be null");
        }

        if (string.IsNullOrEmpty(data.Id))
        {
            _logger.LogWarning("Received data with empty ID");
            return BadRequest("Data ID cannot be empty");
        }

        if (string.IsNullOrEmpty(data.Name))
        {
            _logger.LogWarning("Received data with empty Name");
            return BadRequest("Data Name cannot be empty");
        }

        _logger.LogInformation("Processing data for ID: {Id}", data.Id);

        try
        {
            var result = await _sampleService.ProcessDataAsync(data);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process data for ID: {Id}", data.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the data");
        }
    }

    /// <summary>
    /// Gets health status of the sample module.
    /// This endpoint is explicitly marked with AllowAnonymous for health monitoring.
    /// </summary>
    /// <returns>Health status information.</returns>
    [HttpGet("health")]
    [AllowAnonymous] // Health endpoint should be accessible without authentication
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> GetHealth()
    {
        return Ok(new
        {
            Module = "SampleBusinessLogic",
            Status = "Healthy",
            Version = "1.0.0",
            Timestamp = DateTime.UtcNow
        });
    }
}