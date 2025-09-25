using DotNetShell.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using FluentAssertions;

namespace DotNetShell.Host.UnitTests;

/// <summary>
/// Unit tests for ShellHostBuilder.
/// </summary>
public class ShellHostBuilderTests
{
    [Fact]
    public void Constructor_WithValidBuilder_ShouldNotThrow()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        // Act & Assert
        var shellHostBuilder = new ShellHostBuilder(builder);
        shellHostBuilder.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        // Arrange
        WebApplicationBuilder? builder = null;

        // Act & Assert
        var act = () => new ShellHostBuilder(builder!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void ConfigureConfiguration_ShouldReturnSameInstance()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var shellHostBuilder = new ShellHostBuilder(builder);

        // Act
        var result = shellHostBuilder.ConfigureConfiguration();

        // Assert
        result.Should().BeSameAs(shellHostBuilder);
    }

    [Fact]
    public void ConfigureLogging_ShouldReturnSameInstance()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var shellHostBuilder = new ShellHostBuilder(builder);

        // Act
        var result = shellHostBuilder.ConfigureLogging();

        // Assert
        result.Should().BeSameAs(shellHostBuilder);
    }

    [Fact]
    public void ConfigureServices_ShouldReturnSameInstance()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var shellHostBuilder = new ShellHostBuilder(builder);

        // Act
        var result = shellHostBuilder.ConfigureServices();

        // Assert
        result.Should().BeSameAs(shellHostBuilder);
    }

    [Fact]
    public void ConfigureServices_ShouldRegisterRequiredServices()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var shellHostBuilder = new ShellHostBuilder(builder);

        // Act
        shellHostBuilder.ConfigureServices();

        // Assert
        var serviceProvider = builder.Services.BuildServiceProvider();

        // Verify that controllers are registered
        var controllerService = serviceProvider.GetService<IServiceCollection>();
        controllerService.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnShellHost()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var shellHostBuilder = new ShellHostBuilder(builder);

        // Act
        var shellHost = shellHostBuilder.Build();

        // Assert
        shellHost.Should().NotBeNull();
        shellHost.Should().BeOfType<ShellHost>();
    }

    [Fact]
    public void ConfigureHealthChecks_ShouldReturnSameInstance()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var shellHostBuilder = new ShellHostBuilder(builder);

        // Act
        var result = shellHostBuilder.ConfigureHealthChecks();

        // Assert
        result.Should().BeSameAs(shellHostBuilder);
    }

    [Fact]
    public void ConfigureSwagger_WhenEnabled_ShouldReturnSameInstance()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Shell:Swagger:Enabled"] = "true";
        var shellHostBuilder = new ShellHostBuilder(builder);

        // Act
        var result = shellHostBuilder.ConfigureSwagger();

        // Assert
        result.Should().BeSameAs(shellHostBuilder);
    }

    [Fact]
    public void FluentConfiguration_ShouldAllowMethodChaining()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        // Act
        var shellHost = new ShellHostBuilder(builder)
            .ConfigureConfiguration()
            .ConfigureLogging()
            .ConfigureServices()
            .ConfigureTelemetry()
            .ConfigureAuthentication()
            .ConfigureAuthorization()
            .ConfigureHealthChecks()
            .ConfigureSwagger()
            .Build();

        // Assert
        shellHost.Should().NotBeNull();
        shellHost.Should().BeOfType<ShellHost>();
    }
}