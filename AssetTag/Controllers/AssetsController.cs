using AssetTag.Data;
using AssetTag.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AssetsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AssetsController(ApplicationDbContext context) => _context = context;

    // GET: /api/assets
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssetReadDTO>>> Get() =>
        Ok(await _context.Assets
            .AsNoTracking()
            .Select(a => new AssetReadDTO
            {
                AssetId = a.AssetId,
                AssetTag = a.AssetTag,
                Name = a.Name,
                Description = a.Description,
                CategoryId = a.CategoryId,
                LocationId = a.LocationId,
                DepartmentId = a.DepartmentId,
                PurchaseDate = a.PurchaseDate,
                PurchasePrice = a.PurchasePrice,
                CurrentValue = a.CurrentValue,
                Status = a.Status,
                AssignedToUserId = a.AssignedToUserId,
                CreatedAt = a.CreatedAt,
                DateModified = a.DateModified,
                SerialNumber = a.SerialNumber,
                Condition = a.Condition,
                VendorName = a.VendorName,
                InvoiceNumber = a.InvoiceNumber,
                Quantity = a.Quantity,
                CostPerUnit = a.CostPerUnit,
                TotalCost = a.TotalCost,
                DepreciationRate = a.DepreciationRate,
                AccumulatedDepreciation = a.AccumulatedDepreciation,
                NetBookValue = a.NetBookValue,
                UsefulLifeYears = a.UsefulLifeYears,
                WarrantyExpiry = a.WarrantyExpiry,
                DisposalDate = a.DisposalDate,
                DisposalValue = a.DisposalValue,
                Remarks = a.Remarks
            })
            .ToListAsync());

    // GET: /api/assets/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<AssetReadDTO>> Get(string id)
    {
        var asset = await _context.Assets.FindAsync(id);
        if (asset is null) return NotFound();

        return Ok(new AssetReadDTO
        {
            AssetId = asset.AssetId,
            AssetTag = asset.AssetTag,
            Name = asset.Name,
            Description = asset.Description,
            CategoryId = asset.CategoryId,
            LocationId = asset.LocationId,
            DepartmentId = asset.DepartmentId,
            PurchaseDate = asset.PurchaseDate,
            PurchasePrice = asset.PurchasePrice,
            CurrentValue = asset.CurrentValue,
            Status = asset.Status,
            AssignedToUserId = asset.AssignedToUserId,
            CreatedAt = asset.CreatedAt,
            DateModified = asset.DateModified,
            SerialNumber = asset.SerialNumber,
            Condition = asset.Condition,
            VendorName = asset.VendorName,
            InvoiceNumber = asset.InvoiceNumber,
            Quantity = asset.Quantity,
            CostPerUnit = asset.CostPerUnit,
            TotalCost = asset.TotalCost,
            DepreciationRate = asset.DepreciationRate,
            AccumulatedDepreciation = asset.AccumulatedDepreciation,
            NetBookValue = asset.NetBookValue,
            UsefulLifeYears = asset.UsefulLifeYears,
            WarrantyExpiry = asset.WarrantyExpiry,
            DisposalDate = asset.DisposalDate,
            DisposalValue = asset.DisposalValue,
            Remarks = asset.Remarks
        });
    }

    // POST: /api/assets
    [HttpPost]
    public async Task<ActionResult<AssetReadDTO>> Post([FromBody] AssetCreateDTO dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _context.Assets.AnyAsync(a => a.AssetTag == dto.AssetTag))
            return Conflict("Asset tag already exists.");

        var asset = new Asset
        {
            AssetTag = dto.AssetTag,
            Name = dto.Name,
            Description = dto.Description,
            CategoryId = dto.CategoryId,
            LocationId = dto.LocationId,
            DepartmentId = dto.DepartmentId,
            PurchaseDate = dto.PurchaseDate,
            PurchasePrice = dto.PurchasePrice,
            CurrentValue = dto.CurrentValue,
            Status = dto.Status,
            AssignedToUserId = dto.AssignedToUserId,
            SerialNumber = dto.SerialNumber,
            Condition = dto.Condition,
            VendorName = dto.VendorName,
            InvoiceNumber = dto.InvoiceNumber,
            Quantity = dto.Quantity,
            CostPerUnit = dto.CostPerUnit,
            TotalCost = dto.TotalCost,
            DepreciationRate = dto.DepreciationRate,
            AccumulatedDepreciation = dto.AccumulatedDepreciation,
            NetBookValue = dto.NetBookValue,
            UsefulLifeYears = dto.UsefulLifeYears,
            WarrantyExpiry = dto.WarrantyExpiry,
            DisposalDate = dto.DisposalDate,
            DisposalValue = dto.DisposalValue,
            Remarks = dto.Remarks,
            // Satisfy required navigation properties
            Category = null!,
            Location = null!,
            Department = null!,
            AssignedToUser = null,
            AssetHistories = new List<AssetHistory>()
        };

        _context.Assets.Add(asset);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = asset.AssetId },
            new AssetReadDTO
            {
                AssetId = asset.AssetId,
                AssetTag = asset.AssetTag,
                Name = asset.Name,
                Description = asset.Description,
                CategoryId = asset.CategoryId,
                LocationId = asset.LocationId,
                DepartmentId = asset.DepartmentId,
                PurchaseDate = asset.PurchaseDate,
                PurchasePrice = asset.PurchasePrice,
                CurrentValue = asset.CurrentValue,
                Status = asset.Status,
                AssignedToUserId = asset.AssignedToUserId,
                CreatedAt = asset.CreatedAt,
                DateModified = asset.DateModified,
                SerialNumber = asset.SerialNumber,
                Condition = asset.Condition,
                VendorName = asset.VendorName,
                InvoiceNumber = asset.InvoiceNumber,
                Quantity = asset.Quantity,
                CostPerUnit = asset.CostPerUnit,
                TotalCost = asset.TotalCost,
                DepreciationRate = asset.DepreciationRate,
                AccumulatedDepreciation = asset.AccumulatedDepreciation,
                NetBookValue = asset.NetBookValue,
                UsefulLifeYears = asset.UsefulLifeYears,
                WarrantyExpiry = asset.WarrantyExpiry,
                DisposalDate = asset.DisposalDate,
                DisposalValue = asset.DisposalValue,
                Remarks = asset.Remarks
            });
    }

    // PUT: /api/assets/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(string id, [FromBody] AssetUpdateDTO dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.AssetId) return BadRequest();

        var asset = await _context.Assets.FindAsync(id);
        if (asset is null) return NotFound();

        if (dto.AssetTag is not null && dto.AssetTag != asset.AssetTag &&
            await _context.Assets.AnyAsync(a => a.AssetTag == dto.AssetTag))
            return Conflict("Asset tag already exists.");

        asset.AssetTag = dto.AssetTag ?? asset.AssetTag;
        asset.Name = dto.Name ?? asset.Name;
        asset.Description = dto.Description ?? asset.Description;
        asset.CategoryId = dto.CategoryId ?? asset.CategoryId;
        asset.LocationId = dto.LocationId ?? asset.LocationId;
        asset.DepartmentId = dto.DepartmentId ?? asset.DepartmentId;
        asset.PurchaseDate = dto.PurchaseDate ?? asset.PurchaseDate;
        asset.PurchasePrice = dto.PurchasePrice ?? asset.PurchasePrice;
        asset.CurrentValue = dto.CurrentValue ?? asset.CurrentValue;
        asset.Status = dto.Status ?? asset.Status;
        asset.AssignedToUserId = dto.AssignedToUserId ?? asset.AssignedToUserId;
        asset.SerialNumber = dto.SerialNumber ?? asset.SerialNumber;
        asset.Condition = dto.Condition ?? asset.Condition;
        asset.VendorName = dto.VendorName ?? asset.VendorName;
        asset.InvoiceNumber = dto.InvoiceNumber ?? asset.InvoiceNumber;
        if (dto.Quantity.HasValue) asset.Quantity = dto.Quantity.Value;
        asset.CostPerUnit = dto.CostPerUnit ?? asset.CostPerUnit;
        asset.TotalCost = dto.TotalCost ?? asset.TotalCost;
        asset.DepreciationRate = dto.DepreciationRate ?? asset.DepreciationRate;
        asset.AccumulatedDepreciation = dto.AccumulatedDepreciation ?? asset.AccumulatedDepreciation;
        asset.NetBookValue = dto.NetBookValue ?? asset.NetBookValue;
        if (dto.UsefulLifeYears.HasValue) asset.UsefulLifeYears = dto.UsefulLifeYears.Value;
        asset.WarrantyExpiry = dto.WarrantyExpiry ?? asset.WarrantyExpiry;
        asset.DisposalDate = dto.DisposalDate ?? asset.DisposalDate;
        asset.DisposalValue = dto.DisposalValue ?? asset.DisposalValue;
        asset.Remarks = dto.Remarks ?? asset.Remarks;

        asset.DateModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: /api/assets/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var asset = await _context.Assets.FindAsync(id);
        if (asset is null) return NotFound();

        _context.Assets.Remove(asset);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}