using Xunit;
using FluentAssertions;

namespace DotNetShell.Core.UnitTests;

/// <summary>
/// Placeholder test class for DotNetShell.Core unit tests.
/// This will be replaced with actual tests as the Core library is implemented.
/// </summary>
public class PlaceholderTests
{
    [Fact]
    public void Placeholder_Test_ShouldPass()
    {
        // Arrange
        var expectedValue = true;

        // Act
        var actualValue = true;

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 1, 0)]
    public void Add_WithVariousInputs_ShouldReturnCorrectSum(int a, int b, int expected)
    {
        // Arrange & Act
        var result = a + b;

        // Assert
        result.Should().Be(expected);
    }
}