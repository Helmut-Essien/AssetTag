using Shared.Constants;
using Shared.Models;
using Xunit;

namespace MobileApp.Tests.Shared;

public sealed class AssetFinancialTests
{
    [Fact]
    public void CalculatedUsefulLifeYears_UsesCategoryRate()
    {
        var asset = new Asset
        {
            AssetTag = "T1",
            Name = "Laptop",
            CategoryId = "c",
            LocationId = "l",
            DepartmentId = "d",
            Status = AssetConstants.Status.Available,
            Condition = AssetConstants.Condition.Good,
            Category = new Category { Name = "IT", DepreciationRate = 20m }
        };

        Assert.Equal(5, asset.CalculatedUsefulLifeYears);
    }

    [Fact]
    public void CalculatedUsefulLifeYears_OverrideWins()
    {
        var asset = new Asset
        {
            AssetTag = "T1",
            Name = "Laptop",
            CategoryId = "c",
            LocationId = "l",
            DepartmentId = "d",
            Status = AssetConstants.Status.Available,
            Condition = AssetConstants.Condition.Good,
            UsefulLifeYears = 8,
            Category = new Category { Name = "IT", DepreciationRate = 20m }
        };

        Assert.Equal(8, asset.CalculatedUsefulLifeYears);
    }

    [Fact]
    public void TotalCost_IsUnitTimesQuantity()
    {
        var asset = new Asset
        {
            AssetTag = "T1",
            Name = "Chair",
            CategoryId = "c",
            LocationId = "l",
            DepartmentId = "d",
            Status = AssetConstants.Status.Available,
            Condition = AssetConstants.Condition.Good,
            CostPerUnit = 150m,
            Quantity = 4
        };

        Assert.Equal(600m, asset.TotalCost);
    }

    [Fact]
    public void AccumulatedDepreciation_CapsAtPurchasePrice()
    {
        var asset = new Asset
        {
            AssetTag = "T1",
            Name = "Old Server",
            CategoryId = "c",
            LocationId = "l",
            DepartmentId = "d",
            Status = AssetConstants.Status.Available,
            Condition = AssetConstants.Condition.Fair,
            PurchasePrice = 1000m,
            PurchaseDate = DateTime.UtcNow.AddYears(-20),
            Category = new Category { Name = "IT", DepreciationRate = 20m }
        };

        Assert.Equal(1000m, asset.AccumulatedDepreciation);
        Assert.Equal(0m, asset.NetBookValue);
    }

    [Fact]
    public void GainLossOnDisposal_IsDisposalMinusBookValue()
    {
        var asset = new Asset
        {
            AssetTag = "T1",
            Name = "Van",
            CategoryId = "c",
            LocationId = "l",
            DepartmentId = "d",
            Status = AssetConstants.Status.Disposed,
            Condition = AssetConstants.Condition.Poor,
            PurchasePrice = 1000m,
            PurchaseDate = DateTime.UtcNow.AddYears(-1),
            DisposalDate = DateTime.UtcNow.AddDays(-1),
            DisposalValue = 900m,
            Category = new Category { Name = "Vehicles", DepreciationRate = 20m }
        };

        Assert.NotNull(asset.GainLossOnDisposal);
        Assert.Equal(asset.DisposalValue - asset.NetBookValue, asset.GainLossOnDisposal);
    }
}

public sealed class AssetConstantsTests
{
    [Fact]
    public void StatusAndConditionSets_ContainExpectedValues()
    {
        Assert.Contains(AssetConstants.Status.Available, AssetConstants.Status.All);
        Assert.Contains(AssetConstants.Status.InUse, AssetConstants.Status.All);
        Assert.Contains(AssetConstants.Condition.Broken, AssetConstants.Condition.RequiresMaintenance);
        Assert.DoesNotContain(AssetConstants.Condition.Excellent, AssetConstants.Condition.RequiresMaintenance);
    }
}
