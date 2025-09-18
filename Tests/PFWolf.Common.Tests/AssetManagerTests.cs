using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CSharpFunctionalExtensions;
using PFWolf.Common;
using PFWolf.Common.Assets;
using Xunit;

namespace PFWolf.Common.Tests;
public class AssetManagerTests
{
    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenSelectedGamePackIsNone()
    {
        // Arrange
        var assetManager = new AssetManager(new List<string>());

        // Act
        var result = assetManager.LoadGamePacks(Maybe<string>.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cannot determine default game pack at this time.", result.Error);
    }

    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenGamePackInfoNotFound()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Create)) { }
        var assetManager = new AssetManager(new List<string> { tempFile });

        // Act
        var result = assetManager.LoadGamePacks("testpack");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("No gamepacks/gamepack-info found", result.Error);
        File.Delete(tempFile);
    }

    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenMultipleGamePackInfoFound()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Update))
        {
            zip.CreateEntry("gamepacks/gamepack-info");
            zip.CreateEntry("gamepacks/gamepack-info2");
        }
        var assetManager = new AssetManager(new List<string> { tempFile });

        // Act
        var result = assetManager.LoadGamePacks("testpack");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("More than 1 gamepacks/gamepack-info found", result.Error);
        File.Delete(tempFile);
    }

    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenYamlExceptionThrown()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Update))
        {
            var entry = zip.CreateEntry("gamepacks/gamepack-info");
            using var stream = entry.Open();
            stream.Write(new byte[] { 0xFF, 0xFF, 0xFF }, 0, 3); // Invalid YAML
        }
        var assetManager = new AssetManager(new List<string> { tempFile });

        // Act
        var result = assetManager.LoadGamePacks("testpack");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Error parsing game pack definitions", result.Error);
        File.Delete(tempFile);
    }

    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenGamePackNotFound()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Update))
        {
            var entry = zip.CreateEntry("gamepacks/gamepack-info");
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write("gamepacks: {}");
        }
        var assetManager = new AssetManager(new List<string> { tempFile });

        // Act
        var result = assetManager.LoadGamePacks("notfoundpack");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("not found in loaded packages", result.Error);
        File.Delete(tempFile);
    }

    // Additional tests for success scenarios would require more setup and possibly fakes/mocks for Asset, GamePackDefinitions, etc.
}
