using AssetTag.Data;
using Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using System.Security.Claims;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AssetsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AssetsController(ApplicationDbContext context) => _context = context;

    private async Task CreateAssetHistory(string assetId, string action, string description,
        string? oldLocationId = null, string? newLocationId = null,
        string? oldStatus = null, string? newStatus = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        var history = new AssetHistory
        {
            AssetId = assetId,
            UserId = userId,
            Action = action,
            Description = description,
            OldLocationId = oldLocationId,
            NewLocationId = newLocationId,
            OldStatus = oldStatus,
            NewStatus = newStatus
        };

        _context.AssetHistories.Add(history);
    }

    // GET: /api/assets
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssetReadDTO>>> Get(
    [FromQuery] string? searchTerm,
    [FromQuery] string? status,
    [FromQuery] string? condition,
    [FromQuery] string? categoryId,
    [FromQuery] string? locationId,
    [FromQuery] string? departmentId)
    {
        var query = _context.Assets.AsNoTracking().AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(a =>
                a.AssetTag.Contains(searchTerm) ||
                a.Name.Contains(searchTerm) ||
                a.Description.Contains(searchTerm) ||
                a.SerialNumber.Contains(searchTerm) ||
                a.VendorName.Contains(searchTerm) ||
                a.InvoiceNumber.Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);

        if (!string.IsNullOrEmpty(condition))
            query = query.Where(a => a.Condition == condition);

        if (!string.IsNullOrEmpty(categoryId))
            query = query.Where(a => a.CategoryId == categoryId);

        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(a => a.LocationId == locationId);

        if (!string.IsNullOrEmpty(departmentId))
            query = query.Where(a => a.DepartmentId == departmentId);

        var assets = await query
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
            .ToListAsync();
        return Ok(assets);
    }


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
                Category = null!,
                Location = null!,
                Department = null!,
                AssignedToUser = null,
                AssetHistories = new List<AssetHistory>()
            };

            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();

            // Record creation history
            await CreateAssetHistory(
                asset.AssetId,
                "CREATE",
                $"Asset '{asset.Name}' with tag '{asset.AssetTag}' was created",
                newLocationId: asset.LocationId,
                newStatus: asset.Status
            );
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

            // Store old values for history
            var oldLocationId = asset.LocationId;
            var oldStatus = asset.Status;
            var oldDepartmentId = asset.DepartmentId;
            var oldCategoryId = asset.CategoryId;
            var changes = new List<string>();

            if (dto.AssetTag is not null && dto.AssetTag != asset.AssetTag &&
                await _context.Assets.AnyAsync(a => a.AssetTag == dto.AssetTag))
                return Conflict("Asset tag already exists.");

            // Track changes for ALL fields
            if (dto.AssetTag is not null && dto.AssetTag != asset.AssetTag)
                changes.Add($"Asset tag changed from '{asset.AssetTag}' to '{dto.AssetTag}'");

            if (dto.Name is not null && dto.Name != asset.Name)
                changes.Add($"Name changed from '{asset.Name}' to '{dto.Name}'");

            if (dto.Description is not null && dto.Description != asset.Description)
                changes.Add($"Description changed");

            if (dto.CategoryId is not null && dto.CategoryId != asset.CategoryId)
            {
                var oldCategory = await _context.Categories.FindAsync(asset.CategoryId);
                var newCategory = await _context.Categories.FindAsync(dto.CategoryId);
                changes.Add($"Category changed from '{oldCategory?.Name ?? "Unknown"}' to '{newCategory?.Name ?? "Unknown"}'");
            }

            if (dto.LocationId is not null && dto.LocationId != asset.LocationId)
            {
                var oldLocation = await _context.Locations.FindAsync(asset.LocationId);
                var newLocation = await _context.Locations.FindAsync(dto.LocationId);
                changes.Add($"Location changed from '{oldLocation?.Name ?? "Unknown"}' to '{newLocation?.Name ?? "Unknown"}'");
            }

            if (dto.DepartmentId is not null && dto.DepartmentId != asset.DepartmentId)
            {
                var oldDepartment = await _context.Departments.FindAsync(asset.DepartmentId);
                var newDepartment = await _context.Departments.FindAsync(dto.DepartmentId);
                changes.Add($"Department changed from '{oldDepartment?.Name ?? "Unknown"}' to '{newDepartment?.Name ?? "Unknown"}'");
            }

            if (dto.Status is not null && dto.Status != asset.Status)
                changes.Add($"Status changed from '{asset.Status}' to '{dto.Status}'");

            if (dto.Condition is not null && dto.Condition != asset.Condition)
                changes.Add($"Condition changed from '{asset.Condition}' to '{dto.Condition}'");

            if (dto.AssignedToUserId is not null && dto.AssignedToUserId != asset.AssignedToUserId)
            {
                var oldUser = asset.AssignedToUserId != null ?
                    await _context.Users.FindAsync(asset.AssignedToUserId) : null;
                var newUser = await _context.Users.FindAsync(dto.AssignedToUserId);

                changes.Add($"Assignment changed from '{(oldUser != null ? $"{oldUser.FirstName} {oldUser.Surname}" : "Unassigned")}' to '{(newUser != null ? $"{newUser.FirstName} {newUser.Surname}" : "Unassigned")}'");
            }

            // Track additional fields
            if (dto.SerialNumber is not null && dto.SerialNumber != asset.SerialNumber)
                changes.Add($"Serial number changed");

            if (dto.VendorName is not null && dto.VendorName != asset.VendorName)
                changes.Add($"Vendor changed to '{dto.VendorName}'");

            if (dto.InvoiceNumber is not null && dto.InvoiceNumber != asset.InvoiceNumber)
                changes.Add($"Invoice number changed");

            if (dto.PurchaseDate is not null && dto.PurchaseDate != asset.PurchaseDate)
                changes.Add($"Purchase date changed to '{dto.PurchaseDate?.ToString("MMM dd, yyyy")}'");

            if (dto.PurchasePrice is not null && dto.PurchasePrice != asset.PurchasePrice)
                changes.Add($"Purchase price changed to {dto.PurchasePrice?.ToString("C")}");

            if (dto.CurrentValue is not null && dto.CurrentValue != asset.CurrentValue)
                changes.Add($"Current value changed to {dto.CurrentValue?.ToString("C")}");

            if (dto.Quantity.HasValue && dto.Quantity.Value != asset.Quantity)
                changes.Add($"Quantity changed from {asset.Quantity} to {dto.Quantity.Value}");

            if (dto.CostPerUnit is not null && dto.CostPerUnit != asset.CostPerUnit)
                changes.Add($"Cost per unit changed to {dto.CostPerUnit?.ToString("C")}");

            if (dto.TotalCost is not null && dto.TotalCost != asset.TotalCost)
                changes.Add($"Total cost changed to {dto.TotalCost?.ToString("C")}");

            if (dto.WarrantyExpiry is not null && dto.WarrantyExpiry != asset.WarrantyExpiry)
                changes.Add($"Warranty expiry changed to '{dto.WarrantyExpiry?.ToString("MMM dd, yyyy")}'");

            if (dto.Remarks is not null && dto.Remarks != asset.Remarks)
                changes.Add($"Remarks updated");

            // Update asset properties
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

            // Record update history if there were ANY changes
            if (changes.Any())
            {
                await CreateAssetHistory(
                    asset.AssetId,
                    "UPDATE",
                    $"Asset updated: {string.Join("; ", changes)}",
                    oldLocationId: oldLocationId,
                    newLocationId: asset.LocationId,
                    oldStatus: oldStatus,
                    newStatus: asset.Status
                );
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        // DELETE: /api/assets/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset is null) return NotFound();

            // Record deletion history
            await CreateAssetHistory(
                asset.AssetId,
                "DELETE",
                $"Asset '{asset.Name}' with tag '{asset.AssetTag}' was deleted"
            );

            _context.Assets.Remove(asset);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    } 
