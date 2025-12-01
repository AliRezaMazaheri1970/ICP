using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests;

public class CorrectionServiceTests : IDisposable
{
    private readonly IsatisDbContext _db;
    private readonly CorrectionService _correctionService;
    private readonly Mock<IChangeLogService> _changeLogServiceMock;
    private readonly Guid _testProjectId;

    public CorrectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<IsatisDbContext>()
            .UseInMemoryDatabase(databaseName: $"CorrectionTest_{Guid.NewGuid()}")
            .Options;

        _db = new IsatisDbContext(options);
        _changeLogServiceMock = new Mock<IChangeLogService>();
        var logger = new Mock<ILogger<CorrectionService>>();

        _correctionService = new CorrectionService(_db, _changeLogServiceMock.Object, logger.Object);
        _testProjectId = Guid.NewGuid();

        SeedTestData();
    }

    private void SeedTestData()
    {
        var project = new Project
        {
            ProjectId = _testProjectId,
            ProjectName = "Test Project",
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);

        // Add sample rows with different weights
        var samples = new (string Label, decimal Weight, decimal Volume, string Type)[]
        {
            ("Sample1", 0.5m, 50m, "Samp"),
            ("Sample2", 0.48m, 50m, "Samp"),
            ("Sample3", 0.52m, 50m, "Samp"),
            ("Sample4", 0.3m, 50m, "Samp"),
            ("Sample5", 0.5m, 45m, "Samp"),
            ("STD1", 0.5m, 50m, "Std")
        };

        foreach (var sample in samples)
        {
            var data = new Dictionary<string, object>
            {
                ["Solution Label"] = sample.Label,
                ["Act Wgt"] = sample.Weight,
                ["Act Vol"] = sample.Volume,
                ["Type"] = sample.Type,
                ["Corr Con"] = 100m,
                ["Fe"] = 45.5m,
                ["Cu"] = 0.12m
            };

            _db.RawDataRows.Add(new RawDataRow
            {
                ProjectId = _testProjectId,
                SampleId = sample.Label,
                ColumnData = JsonSerializer.Serialize(data)
            });
        }

        _db.SaveChanges();
    }

    [Fact]
    public async Task FindBadWeightsAsync_ShouldReturnSamplesOutsideRange()
    {
        // Arrange
        var request = new FindBadWeightsRequest(_testProjectId, 0.45m, 0.55m);

        // Act
        var result = await _correctionService.FindBadWeightsAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data, b => b.SolutionLabel == "Sample4");
    }

    [Fact]
    public async Task FindBadVolumesAsync_ShouldReturnSamplesWithWrongVolume()
    {
        // Arrange
        var request = new FindBadVolumesRequest(_testProjectId, 50m);

        // Act
        var result = await _correctionService.FindBadVolumesAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("Sample5", result.Data[0].SolutionLabel);
    }

    [Fact]
    public async Task FindEmptyRowsAsync_WithValidData_ShouldSucceed()
    {
        // Arrange
        var request = new FindEmptyRowsRequest(_testProjectId, 90m, null, true);

        // Act
        var result = await _correctionService.FindEmptyRowsAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ApplyWeightCorrectionAsync_ShouldUpdateWeightAndCorrCon()
    {
        // Arrange
        var request = new WeightCorrectionRequest(
            _testProjectId,
            new List<string> { "Sample4" },
            0.5m,
            "TestUser"
        );

        // Act
        var result = await _correctionService.ApplyWeightCorrectionAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Data?.CorrectedRows >= 1);

        // Verify the data was updated
        var updatedRow = await _db.RawDataRows
            .FirstOrDefaultAsync(r => r.SampleId == "Sample4");
        Assert.NotNull(updatedRow);

        using var doc = JsonDocument.Parse(updatedRow.ColumnData);
        var newWeight = doc.RootElement.GetProperty("Act Wgt").GetDecimal();
        Assert.Equal(0.5m, newWeight);
    }

    [Fact]
    public async Task ApplyVolumeCorrectionAsync_ShouldUpdateVolumeAndCorrCon()
    {
        // Arrange
        var request = new VolumeCorrectionRequest(
            _testProjectId,
            new List<string> { "Sample5" },
            50m,
            "TestUser"
        );

        // Act
        var result = await _correctionService.ApplyVolumeCorrectionAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Data?.CorrectedRows >= 1);
    }

    [Fact]
    public async Task FindBadWeightsAsync_WithNoData_ShouldFail()
    {
        // Arrange
        var request = new FindBadWeightsRequest(Guid.NewGuid(), 0.45m, 0.55m);

        // Act
        var result = await _correctionService.FindBadWeightsAsync(request);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ApplyWeightCorrectionAsync_WithInvalidWeight_ShouldFail()
    {
        // Arrange
        var request = new WeightCorrectionRequest(
            _testProjectId,
            new List<string> { "Sample1" },
            0m,
            "TestUser"
        );

        // Act
        var result = await _correctionService.ApplyWeightCorrectionAsync(request);

        // Assert
        Assert.False(result.Succeeded);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}