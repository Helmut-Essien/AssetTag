using AssetTag.Data;
using Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using System.Security.Claims;
using ClosedXML.Excel;
using System.Globalization;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AssetsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AssetsController(ApplicationDbContext context) => _context = context;

    private Task CreateAssetHistory(string assetId, string action, string description,
        string? oldLocationId = null, string? newLocationId = null,
        string? oldStatus = null, string? newStatus = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Task.CompletedTask;

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
        return Task.CompletedTask;
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
                (a.Description != null && a.Description.Contains(searchTerm)) ||
                (a.SerialNumber != null && a.SerialNumber.Contains(searchTerm)) ||
                (a.DigitalAssetTag != null && a.DigitalAssetTag.Contains(searchTerm)) ||
                (a.VendorName != null && a.VendorName.Contains(searchTerm)) ||
                (a.InvoiceNumber != null && a.InvoiceNumber.Contains(searchTerm)));
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
                .Include(a => a.Category)  // Include Category to access DepreciationRate
                .ToListAsync();
        
        // Map to DTOs with computed properties
        var assetDtos = assets.Select(a => new AssetReadDTO
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
            DigitalAssetTag = a.DigitalAssetTag,
            Condition = a.Condition,
            VendorName = a.VendorName,
            InvoiceNumber = a.InvoiceNumber,
            Quantity = a.Quantity,
            CostPerUnit = a.CostPerUnit,
            UsefulLifeYears = a.UsefulLifeYears,
            WarrantyExpiry = a.WarrantyExpiry,
            DisposalDate = a.DisposalDate,
            DisposalValue = a.DisposalValue,
            Remarks = a.Remarks,
            // Calculated fields from Category and computed properties
            DepreciationRate = a.Category?.DepreciationRate,
            CalculatedUsefulLifeYears = a.CalculatedUsefulLifeYears,
            TotalCost = a.TotalCost,
            AccumulatedDepreciation = a.AccumulatedDepreciation,
            NetBookValue = a.NetBookValue,
            GainLossOnDisposal = a.GainLossOnDisposal
        }).ToList();
        
        return Ok(assetDtos);
    }


    // GET: /api/assets/{id}
    [HttpGet("{id}")]
        public async Task<ActionResult<AssetReadDTO>> Get(string id)
        {
            var asset = await _context.Assets
                .Include(a => a.Category)  // Include Category to access DepreciationRate
                .FirstOrDefaultAsync(a => a.AssetId == id);
            
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
                DigitalAssetTag = asset.DigitalAssetTag,
                Condition = asset.Condition,
                VendorName = asset.VendorName,
                InvoiceNumber = asset.InvoiceNumber,
                Quantity = asset.Quantity,
                CostPerUnit = asset.CostPerUnit,
                UsefulLifeYears = asset.UsefulLifeYears,
                WarrantyExpiry = asset.WarrantyExpiry,
                DisposalDate = asset.DisposalDate,
                DisposalValue = asset.DisposalValue,
                Remarks = asset.Remarks,
                // Get depreciation rate from Category
                DepreciationRate = asset.Category?.DepreciationRate,
                // Calculated fields
                CalculatedUsefulLifeYears = asset.CalculatedUsefulLifeYears,
                TotalCost = asset.TotalCost,
                AccumulatedDepreciation = asset.AccumulatedDepreciation,
                NetBookValue = asset.NetBookValue,
                GainLossOnDisposal = asset.GainLossOnDisposal
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
                DigitalAssetTag = dto.DigitalAssetTag,
                Condition = dto.Condition,
                VendorName = dto.VendorName,
                InvoiceNumber = dto.InvoiceNumber,
                Quantity = dto.Quantity,
                CostPerUnit = dto.CostPerUnit,
                UsefulLifeYears = dto.UsefulLifeYears,
                WarrantyExpiry = dto.WarrantyExpiry,
                DisposalDate = dto.DisposalDate,
                DisposalValue = dto.DisposalValue,
                Remarks = dto.Remarks,
                // Calculated fields (TotalCost, AccumulatedDepreciation, NetBookValue) are computed properties
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

            // Reload asset with Category to get computed properties
            var createdAsset = await _context.Assets
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.AssetId == asset.AssetId);

            return CreatedAtAction(nameof(Get), new { id = asset.AssetId },
                new AssetReadDTO
                {
                    AssetId = createdAsset!.AssetId,
                    AssetTag = createdAsset.AssetTag,
                    Name = createdAsset.Name,
                    Description = createdAsset.Description,
                    CategoryId = createdAsset.CategoryId,
                    LocationId = createdAsset.LocationId,
                    DepartmentId = createdAsset.DepartmentId,
                    PurchaseDate = createdAsset.PurchaseDate,
                    PurchasePrice = createdAsset.PurchasePrice,
                    CurrentValue = createdAsset.CurrentValue,
                    Status = createdAsset.Status,
                    AssignedToUserId = createdAsset.AssignedToUserId,
                    CreatedAt = createdAsset.CreatedAt,
                    DateModified = createdAsset.DateModified,
                    SerialNumber = createdAsset.SerialNumber,
                    DigitalAssetTag = createdAsset.DigitalAssetTag,
                    Condition = createdAsset.Condition,
                    VendorName = createdAsset.VendorName,
                    InvoiceNumber = createdAsset.InvoiceNumber,
                    Quantity = createdAsset.Quantity,
                    CostPerUnit = createdAsset.CostPerUnit,
                    UsefulLifeYears = createdAsset.UsefulLifeYears,
                    WarrantyExpiry = createdAsset.WarrantyExpiry,
                    DisposalDate = createdAsset.DisposalDate,
                    DisposalValue = createdAsset.DisposalValue,
                    Remarks = createdAsset.Remarks,
                    // Get from Category and computed properties
                    DepreciationRate = createdAsset.Category?.DepreciationRate,
                    TotalCost = createdAsset.TotalCost,
                    AccumulatedDepreciation = createdAsset.AccumulatedDepreciation,
                    NetBookValue = createdAsset.NetBookValue,
                    GainLossOnDisposal = createdAsset.GainLossOnDisposal
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

            if (dto.DigitalAssetTag is not null && dto.DigitalAssetTag != asset.DigitalAssetTag)
                changes.Add($"Digital asset tag changed to '{dto.DigitalAssetTag}'");

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
            asset.DigitalAssetTag = dto.DigitalAssetTag ?? asset.DigitalAssetTag;
            asset.Condition = dto.Condition ?? asset.Condition;
            asset.VendorName = dto.VendorName ?? asset.VendorName;
            asset.InvoiceNumber = dto.InvoiceNumber ?? asset.InvoiceNumber;
            if (dto.Quantity.HasValue) asset.Quantity = dto.Quantity.Value;
            asset.CostPerUnit = dto.CostPerUnit ?? asset.CostPerUnit;
            if (dto.UsefulLifeYears.HasValue) asset.UsefulLifeYears = dto.UsefulLifeYears.Value;
            asset.WarrantyExpiry = dto.WarrantyExpiry ?? asset.WarrantyExpiry;
            asset.DisposalDate = dto.DisposalDate ?? asset.DisposalDate;
            asset.DisposalValue = dto.DisposalValue ?? asset.DisposalValue;
            asset.Remarks = dto.Remarks ?? asset.Remarks;
            // DepreciationRate comes from Category (not stored in Asset)
            // TotalCost, AccumulatedDepreciation, and NetBookValue are computed properties

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

        // POST: /api/assets/batch-import
        [HttpPost("batch-import")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
        public async Task<ActionResult<AssetImportResultDTO>> BatchImport(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not ".xlsx")
                return BadRequest(new { error = "Only .xlsx files are supported." });

            var errors = new List<ImportErrorDTO>();
            var pendingRows = new List<(Asset asset, bool isNew, bool isUpdated)>();

            // Preload reference data into dictionaries (case-insensitive by trimmed name)
            var categories    = (await _context.Categories.ToListAsync())
                                   .ToDictionary(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase);
            var locations     = (await _context.Locations.ToListAsync())
                                   .ToDictionary(l => l.Name.Trim(), StringComparer.OrdinalIgnoreCase);
            var departments   = (await _context.Departments.ToListAsync())
                                   .ToDictionary(d => d.Name.Trim(), StringComparer.OrdinalIgnoreCase);
            var existingTags  = await _context.Assets.Select(a => a.AssetTag).ToHashSetAsync();
            var existingAssets = await _context.Assets.ToDictionaryAsync(a => a.AssetTag);
            var batchTags     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);
                var usedRange = worksheet.RangeUsed();
                if (usedRange is null)
                    return BadRequest(new { error = "The file appears to be empty." });
                var rows = usedRange.RowsUsed().ToList();

                if (rows.Count < 2)
                    return BadRequest(new { error = "The file has no data rows (only header found)." });

                var headerRow = rows[0];
                var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int ci = 0; ci < headerRow.CellCount(); ci++)
                {
                    var cell = headerRow.Cell(ci + 1);
                    if (!cell.IsEmpty())
                        headerMap[cell.GetString().Trim()] = ci;
                }

                for (int ri = 1; ri < rows.Count; ri++)
                {
                    var row = rows[ri];
                    int excelRow = ri + 1;
                    var rowErrors = new List<string>();

                    string GetCell(string columnName)
                    {
                        if (headerMap.TryGetValue(columnName, out var ci))
                        {
                            var cell = row.Cell(ci + 1);
                            return cell.IsEmpty() ? string.Empty : cell.GetString().Trim();
                        }
                        return string.Empty;
                    }

                    var assetTag = GetCell("AssetTag");
                    var name     = GetCell("Name");
                    var desc     = GetCell("Description");
                    var catName  = GetCell("Category");
                    var locName  = GetCell("Location");
                    var deptName = GetCell("Department");
                    var status   = GetCell("Status");
                    var condition = GetCell("Condition");
                    var sPurchaseDate = GetCell("PurchaseDate");
                    var sPurchasePrice = GetCell("PurchasePrice");
                    var sCurrentValue  = GetCell("CurrentValue");
                    var serialNum = GetCell("SerialNumber");
                    var digitalTag = GetCell("DigitalAssetTag");
                    var vendor = GetCell("VendorName");
                    var invoice = GetCell("InvoiceNumber");
                    var sQuantity = GetCell("Quantity");
                    var sCostPerUnit = GetCell("CostPerUnit");
                    var sUsefulLife = GetCell("UsefulLifeYears");
                    var sWarranty = GetCell("WarrantyExpiry");
                    var sDisposalDate = GetCell("DisposalDate");
                    var sDisposalValue = GetCell("DisposalValue");
                    var remarks = GetCell("Remarks");

                    // AssetTag always required
                    if (string.IsNullOrEmpty(assetTag))
                    {
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "AssetTag", Message = "AssetTag is required." });
                        continue;
                    }

                    // In-batch duplicate check
                    if (!batchTags.Add(assetTag))
                    {
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "AssetTag", Message = $"AssetTag '{assetTag}' is duplicated within this file." });
                        continue;
                    }

                    bool exists = existingTags.Contains(assetTag);

                    // Required fields for new assets only
                    if (!exists)
                    {
                        if (string.IsNullOrEmpty(name))
                        {
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Name", Message = "Name is required for new assets." });
                            continue;
                        }
                        if (string.IsNullOrEmpty(catName))
                        {
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Category", Message = "Category is required for new assets." });
                            continue;
                        }
                        if (string.IsNullOrEmpty(locName))
                        {
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Location", Message = "Location is required for new assets." });
                            continue;
                        }
                        if (string.IsNullOrEmpty(deptName))
                        {
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Department", Message = "Department is required for new assets." });
                            continue;
                        }
                        if (string.IsNullOrEmpty(status))
                        {
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Status", Message = "Status is required for new assets." });
                            continue;
                        }
                        if (string.IsNullOrEmpty(condition))
                        {
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Condition", Message = "Condition is required for new assets." });
                            continue;
                        }
                    }

                    // Resolve FK names → IDs
                    string? categoryId = null;
                    if (!string.IsNullOrEmpty(catName))
                    {
                        if (categories.TryGetValue(catName, out var cat))
                            categoryId = cat.CategoryId;
                        else
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Category", Message = $"Category '{catName}' not found." });
                    }

                    string? locationId = null;
                    if (!string.IsNullOrEmpty(locName))
                    {
                        if (locations.TryGetValue(locName, out var loc))
                            locationId = loc.LocationId;
                        else
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Location", Message = $"Location '{locName}' not found." });
                    }

                    string? departmentId = null;
                    if (!string.IsNullOrEmpty(deptName))
                    {
                        if (departments.TryGetValue(deptName, out var dept))
                            departmentId = dept.DepartmentId;
                        else
                            errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Department", Message = $"Department '{deptName}' not found." });
                    }

                    // Validate enums
                    var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        { "Available", "In Use", "Under Maintenance", "Lost", "Disposed", "Stolen" };
                    if (!string.IsNullOrEmpty(status) && !validStatuses.Contains(status))
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Status", Message = $"Invalid status '{status}'. Valid: {string.Join(", ", validStatuses)}" });

                    var validConditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        { "New", "Good", "Fair", "Poor", "Broken" };
                    if (!string.IsNullOrEmpty(condition) && !validConditions.Contains(condition))
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Condition", Message = $"Invalid condition '{condition}'. Valid: {string.Join(", ", validConditions)}" });

                    // Parse dates
                    DateTime? purchaseDate = TryParseDate(sPurchaseDate);
                    if (!string.IsNullOrEmpty(sPurchaseDate) && purchaseDate is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "PurchaseDate", Message = $"Invalid date format: '{sPurchaseDate}'." });

                    DateTime? warrantyExpiry = TryParseDate(sWarranty);
                    if (!string.IsNullOrEmpty(sWarranty) && warrantyExpiry is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "WarrantyExpiry", Message = $"Invalid date format: '{sWarranty}'." });

                    DateTime? disposalDate = TryParseDate(sDisposalDate);
                    if (!string.IsNullOrEmpty(sDisposalDate) && disposalDate is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "DisposalDate", Message = $"Invalid date format: '{sDisposalDate}'." });

                    // Parse decimals
                    decimal? purchasePrice = TryParseDecimal(sPurchasePrice);
                    if (!string.IsNullOrEmpty(sPurchasePrice) && purchasePrice is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "PurchasePrice", Message = $"Invalid number: '{sPurchasePrice}'." });

                    decimal? currentValue = TryParseDecimal(sCurrentValue);
                    if (!string.IsNullOrEmpty(sCurrentValue) && currentValue is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "CurrentValue", Message = $"Invalid number: '{sCurrentValue}'." });

                    int? quantity = TryParseInt(sQuantity);
                    if (!string.IsNullOrEmpty(sQuantity) && quantity is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Quantity", Message = $"Invalid integer: '{sQuantity}'." });

                    decimal? costPerUnit = TryParseDecimal(sCostPerUnit);
                    if (!string.IsNullOrEmpty(sCostPerUnit) && costPerUnit is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "CostPerUnit", Message = $"Invalid number: '{sCostPerUnit}'." });

                    int? usefulLife = TryParseInt(sUsefulLife);
                    if (!string.IsNullOrEmpty(sUsefulLife) && usefulLife is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "UsefulLifeYears", Message = $"Invalid integer: '{sUsefulLife}'." });

                    decimal? disposalValue = TryParseDecimal(sDisposalValue);
                    if (!string.IsNullOrEmpty(sDisposalValue) && disposalValue is null)
                        errors.Add(new ImportErrorDTO { Row = excelRow, Field = "DisposalValue", Message = $"Invalid number: '{sDisposalValue}'." });

                    // Stop if any errors for this row
                    if (errors.Any(e => e.Row == excelRow))
                        continue;

                    if (exists)
                    {
                        var existing = existingAssets[assetTag];
                        bool updated = false;

                        if (!string.IsNullOrEmpty(name)) { existing.Name = name; updated = true; }
                        if (!string.IsNullOrEmpty(desc)) { existing.Description = desc; updated = true; }
                        if (categoryId is not null) { existing.CategoryId = categoryId; updated = true; }
                        if (locationId is not null) { existing.LocationId = locationId; updated = true; }
                        if (departmentId is not null) { existing.DepartmentId = departmentId; updated = true; }
                        if (!string.IsNullOrEmpty(status)) { existing.Status = status; updated = true; }
                        if (!string.IsNullOrEmpty(condition)) { existing.Condition = condition; updated = true; }
                        if (purchaseDate.HasValue) { existing.PurchaseDate = purchaseDate; updated = true; }
                        if (purchasePrice.HasValue) { existing.PurchasePrice = purchasePrice; updated = true; }
                        if (currentValue.HasValue) { existing.CurrentValue = currentValue; updated = true; }
                        if (!string.IsNullOrEmpty(serialNum)) { existing.SerialNumber = serialNum; updated = true; }
                        if (!string.IsNullOrEmpty(digitalTag)) { existing.DigitalAssetTag = digitalTag; updated = true; }
                        if (!string.IsNullOrEmpty(vendor)) { existing.VendorName = vendor; updated = true; }
                        if (!string.IsNullOrEmpty(invoice)) { existing.InvoiceNumber = invoice; updated = true; }
                        if (quantity.HasValue) { existing.Quantity = quantity.Value; updated = true; }
                        if (costPerUnit.HasValue) { existing.CostPerUnit = costPerUnit; updated = true; }
                        if (usefulLife.HasValue) { existing.UsefulLifeYears = usefulLife; updated = true; }
                        if (warrantyExpiry.HasValue) { existing.WarrantyExpiry = warrantyExpiry; updated = true; }
                        if (disposalDate.HasValue) { existing.DisposalDate = disposalDate; updated = true; }
                        if (disposalValue.HasValue) { existing.DisposalValue = disposalValue; updated = true; }
                        if (!string.IsNullOrEmpty(remarks)) { existing.Remarks = remarks; updated = true; }

                        if (updated)
                            existing.DateModified = DateTime.UtcNow;

                        pendingRows.Add((existing, false, updated));
                    }
                    else
                    {
                        var newAsset = new Asset
                        {
                            AssetTag = assetTag,
                            Name = name!,
                            Description = string.IsNullOrEmpty(desc) ? null : desc,
                            CategoryId = categoryId!,
                            LocationId = locationId!,
                            DepartmentId = departmentId!,
                            Status = status!,
                            Condition = condition!,
                            PurchaseDate = purchaseDate,
                            PurchasePrice = purchasePrice,
                            CurrentValue = currentValue,
                            SerialNumber = string.IsNullOrEmpty(serialNum) ? null : serialNum,
                            DigitalAssetTag = string.IsNullOrEmpty(digitalTag) ? null : digitalTag,
                            VendorName = string.IsNullOrEmpty(vendor) ? null : vendor,
                            InvoiceNumber = string.IsNullOrEmpty(invoice) ? null : invoice,
                            Quantity = quantity ?? 1,
                            CostPerUnit = costPerUnit,
                            UsefulLifeYears = usefulLife,
                            WarrantyExpiry = warrantyExpiry,
                            DisposalDate = disposalDate,
                            DisposalValue = disposalValue,
                            Remarks = string.IsNullOrEmpty(remarks) ? null : remarks,
                            Category = null!,
                            Location = null!,
                            Department = null!,
                            AssignedToUser = null,
                            AssetHistories = new List<AssetHistory>()
                        };
                        pendingRows.Add((newAsset, true, false));
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to parse Excel file: {ex.Message}" });
            }

            var successCount = 0;

            if (pendingRows.Count > 0)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                try
                {
                    successCount = await strategy.ExecuteAsync(async () =>
                    {
                        var createdCount = 0;
                        var upsertedCount = 0;

                        await using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            foreach (var (asset, isNew, isUpdated) in pendingRows)
                            {
                                if (isNew)
                                {
                                    _context.Assets.Add(asset);
                                    await _context.SaveChangesAsync();

                                    await CreateAssetHistory(
                                        asset.AssetId,
                                        "CREATE",
                                        $"Asset '{asset.Name}' with tag '{asset.AssetTag}' was created via batch import",
                                        newLocationId: asset.LocationId,
                                        newStatus: asset.Status
                                    );
                                    createdCount++;
                                }
                                else if (isUpdated)
                                {
                                    await _context.SaveChangesAsync();

                                    await CreateAssetHistory(
                                        asset.AssetId,
                                        "UPDATE",
                                        $"Asset '{asset.Name}' with tag '{asset.AssetTag}' was updated via batch import",
                                        newLocationId: asset.LocationId,
                                        newStatus: asset.Status
                                    );
                                    upsertedCount++;
                                }
                            }

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            return createdCount + upsertedCount;
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = $"Failed to import assets: {ex.Message}" });
                }
            }

            return Ok(new AssetImportResultDTO
            {
                TotalRows = errors.Count + successCount,
                SuccessCount = successCount,
                FailureCount = errors.Count,
                Errors = errors
            });
        }

        private static readonly HashSet<string> DateFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy/MM/dd",
            "dd-MMM-yyyy", "MMM dd, yyyy", "dd MMM yyyy",
            "M/d/yyyy", "d/M/yyyy", "yyyy-M-d"
        };

        private static DateTime? TryParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var dt)
                ? dt
                : null;
        }

        private static decimal? TryParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                ? d
                : null;
        }

        private static int? TryParseInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                ? i
                : null;
        }
    } 
