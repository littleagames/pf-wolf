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
        //C:\Users\Drew\AppData\Local\Temp\tmpymaic4.tmp
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Update)) { }
            var assetManager = new AssetManager(new List<string> { tempFile });

            // Act
            var result = assetManager.LoadGamePacks("testpack");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains("No gamepacks/gamepack-info found", result.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenMultipleGamePackInfoFound()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Update))
            {
                var entry = zip.CreateEntry("gamepacks/gamepack-info");
                using (var stream = entry.Open())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(@"wolf3d-apogee:
    title: ""Wolfenstein 3D""
    map-definitions:
        - mapdefs/wolf3d/walls
        - mapdefs/wolf3d/doors
        - mapdefs/wolf3d/player
        - mapdefs/wolf3d/decorations
        - mapdefs/wolf3d/enemies
    game-palette: wolfpal
    starting-scene: ""wolf3d:GameLoopScene""
    game-pack-asset-reference: gamepacks/wolf3d-apogee-map");
                }
                var entry2 = zip.CreateEntry("gamepacks/gamepack-info2");
                using (var stream = entry2.Open())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(@"wolf3d-activision:
    title: ""Wolfenstein 3D""
    map-definitions:
        - mapdefs/wolf3d/walls
        - mapdefs/wolf3d/doors
        - mapdefs/wolf3d/player
        - mapdefs/wolf3d/decorations
        - mapdefs/wolf3d/enemies
    game-palette: wolfpal
    starting-scene: ""wolf3d:GameLoopScene""
    game-pack-asset-reference: gamepacks/wolf3d-activision-map");
                }
            }
            var assetManager = new AssetManager(new List<string> { tempFile });

            // Act
            var result = assetManager.LoadGamePacks("testpack");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains("More than 1 gamepacks/gamepack-info found", result.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenYamlExceptionThrown()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
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
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadGamePacks_ReturnsFailure_WhenGamePackNotFound()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
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
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Additional tests for success scenarios would require more setup and possibly fakes/mocks for Asset, GamePackDefinitions, etc.
}