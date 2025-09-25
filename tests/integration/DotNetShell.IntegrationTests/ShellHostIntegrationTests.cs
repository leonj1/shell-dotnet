using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace DotNetShell.IntegrationTests;

/// <summary>
/// Integration tests for the Shell Host application.
/// </summary>
public class ShellHostIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ShellHostIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Root_ShouldReturnSuccessAndCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Get_Root_ShouldReturnExpectedStructure()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNullOrEmpty();

        var jsonDocument = JsonDocument.Parse(content);
        jsonDocument.RootElement.TryGetProperty("Name", out var nameProperty).Should().BeTrue();
        nameProperty.GetString().Should().Be("DotNet Shell");

        jsonDocument.RootElement.TryGetProperty("Version", out var versionProperty).Should().BeTrue();
        versionProperty.GetString().Should().Be("1.0.0");

        jsonDocument.RootElement.TryGetProperty("Status", out var statusProperty).Should().BeTrue();
        statusProperty.GetString().Should().Be("Running");

        jsonDocument.RootElement.TryGetProperty("Timestamp", out var timestampProperty).Should().BeTrue();
        timestampProperty.ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Get_HealthLive_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task Get_HealthReady_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Get_HealthEndpoints_ShouldReturnSuccessStatusCode(string url)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_NonExistentEndpoint_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/non-existent-endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}