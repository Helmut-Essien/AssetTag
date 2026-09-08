using System.Globalization;
using AssetTag.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.DTOs;
using Shared.Models;

namespace AssetTag.Services;

public sealed class AssetImportService : IAssetImportService
{
    public const int MaxImportRows = 5_000;
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private static readonly string[] StrictDateFormats =
    {
        "yyyy-MM-dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "yyyy/MM/dd",
        "yyyy/M/d"
    };

    private static readonly Dictionary<string, string> CanonicalStatuses =
        AssetConstants.Status.All.ToDictionary(s => s, s => s, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> CanonicalConditions =
        AssetConstants.Condition.All.ToDictionary(c => c, c => c, StringComparer.OrdinalIgnoreCase);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<AssetImportService> _logger;

    public AssetImportService(ApplicationDbContext context, ILogger<AssetImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AssetImportResultDTO> ImportAsync(
        Stream stream,
        string? userId,
        bool validateOnly = false,
        CancellationToken cancellationToken = default)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new AssetImportException($"Failed to parse Excel file: {ex.Message}", ex);
        }

        using (workbook)
        {
            return await ImportWorkbookAsync(workbook, userId, validateOnly, cancellationToken);
        }
    }

    private async Task<AssetImportResultDTO> ImportWorkbookAsync(
        XLWorkbook workbook,
        string? userId,
        bool validateOnly,
        CancellationToken cancellationToken)
    {
        var errors = new List<ImportErrorDTO>();
        var pendingRows = new List<PendingImportRow>();

        var worksheet = workbook.Worksheet(1);
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
            throw new AssetImportException("The file appears to be empty.");

        var rows = usedRange.RowsUsed().ToList();
        if (rows.Count < 2)
            throw new AssetImportException("The file has no data rows (only header found).");

        var totalDataRows = rows.Count - 1;
        if (totalDataRows > MaxImportRows)
            throw new AssetImportException($"File has {totalDataRows} data rows; the limit is {MaxImportRows}.");

        var headerRow = rows[0];
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var ci = 0; ci < headerRow.CellCount(); ci++)
        {
            var cell = headerRow.Cell(ci + 1);
            if (!cell.IsEmpty())
                headerMap[cell.GetString().Trim()] = ci;
        }

        // First pass: collect tags for targeted DB load (avoid loading entire Assets table).
        var fileTags = new List<string>(totalDataRows);
        var batchTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var ri = 1; ri < rows.Count; ri++)
        {
            var excelRow = ri + 1;
            var assetTag = GetCellString(rows[ri], headerMap, "AssetTag");
            if (string.IsNullOrEmpty(assetTag))
            {
                errors.Add(new ImportErrorDTO { Row = excelRow, Field = "AssetTag", Message = "AssetTag is required." });
                continue;
            }

            if (!batchTags.Add(assetTag))
            {
                errors.Add(new ImportErrorDTO
                {
                    Row = excelRow,
                    Field = "AssetTag",
                    Message = $"AssetTag '{assetTag}' is duplicated within this file."
                });
                continue;
            }

            fileTags.Add(assetTag);
        }

        var categories = (await _context.Categories.AsNoTracking().ToListAsync(cancellationToken))
            .ToDictionary(c => c.Name.Trim(), c => c, StringComparer.OrdinalIgnoreCase);
        var departments = (await _context.Departments.AsNoTracking().ToListAsync(cancellationToken))
            .ToDictionary(d => d.Name.Trim(), d => d, StringComparer.OrdinalIgnoreCase);
        var locationList = await _context.Locations.AsNoTracking().ToListAsync(cancellationToken);
        var locationsByNameCampus = BuildLocationLookup(locationList);

        var existingAssets = await LoadExistingAssetsByTagsAsync(fileTags, track: !validateOnly, cancellationToken);

        // Second pass: full validation / staging (skip rows already marked for AssetTag errors).
        var rowsWithTagErrors = errors.Select(e => e.Row).ToHashSet();
        for (var ri = 1; ri < rows.Count; ri++)
        {
            var excelRow = ri + 1;
            if (rowsWithTagErrors.Contains(excelRow))
                continue;

            var row = rows[ri];
            var assetTag = GetCellString(row, headerMap, "AssetTag");
            var name = GetCellString(row, headerMap, "Name");
            var desc = GetCellString(row, headerMap, "Description");
            var catName = GetCellString(row, headerMap, "Category");
            var locName = GetCellString(row, headerMap, "Location");
            var campus = GetCellString(row, headerMap, "Campus");
            var deptName = GetCellString(row, headerMap, "Department");
            var statusRaw = GetCellString(row, headerMap, "Status");
            var conditionRaw = GetCellString(row, headerMap, "Condition");
            var serialNum = GetCellString(row, headerMap, "SerialNumber");
            var digitalTag = GetCellString(row, headerMap, "DigitalAssetTag");
            var vendor = GetCellString(row, headerMap, "VendorName");
            var invoice = GetCellString(row, headerMap, "InvoiceNumber");
            var remarks = GetCellString(row, headerMap, "Remarks");

            var rowErrorCountBefore = errors.Count;
            var exists = existingAssets.ContainsKey(assetTag);

            if (!exists)
            {
                if (string.IsNullOrEmpty(name))
                    errors.Add(RequiredError(excelRow, "Name"));
                if (string.IsNullOrEmpty(catName))
                    errors.Add(RequiredError(excelRow, "Category"));
                if (string.IsNullOrEmpty(locName))
                    errors.Add(RequiredError(excelRow, "Location"));
                if (string.IsNullOrEmpty(deptName))
                    errors.Add(RequiredError(excelRow, "Department"));
                if (string.IsNullOrEmpty(statusRaw))
                    errors.Add(RequiredError(excelRow, "Status"));
                if (string.IsNullOrEmpty(conditionRaw))
                    errors.Add(RequiredError(excelRow, "Condition"));
            }

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
                var locResult = ResolveLocation(locationsByNameCampus, locName, campus);
                if (locResult.Error is not null)
                    errors.Add(new ImportErrorDTO { Row = excelRow, Field = locResult.ErrorField!, Message = locResult.Error });
                else
                    locationId = locResult.LocationId;
            }

            string? departmentId = null;
            if (!string.IsNullOrEmpty(deptName))
            {
                if (departments.TryGetValue(deptName, out var dept))
                    departmentId = dept.DepartmentId;
                else
                    errors.Add(new ImportErrorDTO { Row = excelRow, Field = "Department", Message = $"Department '{deptName}' not found." });
            }

            string? status = null;
            if (!string.IsNullOrEmpty(statusRaw))
            {
                if (CanonicalStatuses.TryGetValue(statusRaw, out var canonicalStatus))
                    status = canonicalStatus;
                else
                    errors.Add(new ImportErrorDTO
                    {
                        Row = excelRow,
                        Field = "Status",
                        Message = $"Invalid status '{statusRaw}'. Valid: {string.Join(", ", AssetConstants.Status.All)}"
                    });
            }

            string? condition = null;
            if (!string.IsNullOrEmpty(conditionRaw))
            {
                if (CanonicalConditions.TryGetValue(conditionRaw, out var canonicalCondition))
                    condition = canonicalCondition;
                else
                    errors.Add(new ImportErrorDTO
                    {
                        Row = excelRow,
                        Field = "Condition",
                        Message = $"Invalid condition '{conditionRaw}'. Valid: {string.Join(", ", CanonicalConditions.Values.Distinct())}"
                    });
            }

            var purchaseDate = GetDateCell(row, headerMap, "PurchaseDate", excelRow, errors);
            var warrantyExpiry = GetDateCell(row, headerMap, "WarrantyExpiry", excelRow, errors);
            var disposalDate = GetDateCell(row, headerMap, "DisposalDate", excelRow, errors);

            var purchasePrice = GetDecimalCell(row, headerMap, "PurchasePrice", excelRow, errors);
            var currentValue = GetDecimalCell(row, headerMap, "CurrentValue", excelRow, errors);
            var costPerUnit = GetDecimalCell(row, headerMap, "CostPerUnit", excelRow, errors);
            var disposalValue = GetDecimalCell(row, headerMap, "DisposalValue", excelRow, errors);
            var quantity = GetIntCell(row, headerMap, "Quantity", excelRow, errors);
            var usefulLife = GetIntCell(row, headerMap, "UsefulLifeYears", excelRow, errors);

            if (errors.Count > rowErrorCountBefore)
                continue;

            if (exists)
            {
                var existing = existingAssets[assetTag];
                var oldLocationId = existing.LocationId;
                var oldStatus = existing.Status;
                var changedFields = new List<string>();

                if (!string.IsNullOrEmpty(name) && existing.Name != name)
                {
                    existing.Name = name;
                    changedFields.Add("Name");
                }
                if (!string.IsNullOrEmpty(desc) && existing.Description != desc)
                {
                    existing.Description = desc;
                    changedFields.Add("Description");
                }
                if (categoryId is not null && existing.CategoryId != categoryId)
                {
                    existing.CategoryId = categoryId;
                    changedFields.Add("Category");
                }
                if (locationId is not null && existing.LocationId != locationId)
                {
                    existing.LocationId = locationId;
                    changedFields.Add("Location");
                }
                if (departmentId is not null && existing.DepartmentId != departmentId)
                {
                    existing.DepartmentId = departmentId;
                    changedFields.Add("Department");
                }
                if (status is not null && !string.Equals(existing.Status, status, StringComparison.Ordinal))
                {
                    existing.Status = status;
                    changedFields.Add("Status");
                }
                if (condition is not null && !string.Equals(existing.Condition, condition, StringComparison.Ordinal))
                {
                    existing.Condition = condition;
                    changedFields.Add("Condition");
                }
                if (purchaseDate.HasValue && existing.PurchaseDate != purchaseDate)
                {
                    existing.PurchaseDate = purchaseDate;
                    changedFields.Add("PurchaseDate");
                }
                if (purchasePrice.HasValue && existing.PurchasePrice != purchasePrice)
                {
                    existing.PurchasePrice = purchasePrice;
                    changedFields.Add("PurchasePrice");
                }
                if (currentValue.HasValue && existing.CurrentValue != currentValue)
                {
                    existing.CurrentValue = currentValue;
                    changedFields.Add("CurrentValue");
                }
                if (!string.IsNullOrEmpty(serialNum) && existing.SerialNumber != serialNum)
                {
                    existing.SerialNumber = serialNum;
                    changedFields.Add("SerialNumber");
                }
                if (!string.IsNullOrEmpty(digitalTag) && existing.DigitalAssetTag != digitalTag)
                {
                    existing.DigitalAssetTag = digitalTag;
                    changedFields.Add("DigitalAssetTag");
                }
                if (!string.IsNullOrEmpty(vendor) && existing.VendorName != vendor)
                {
                    existing.VendorName = vendor;
                    changedFields.Add("VendorName");
                }
                if (!string.IsNullOrEmpty(invoice) && existing.InvoiceNumber != invoice)
                {
                    existing.InvoiceNumber = invoice;
                    changedFields.Add("InvoiceNumber");
                }
                if (quantity.HasValue && existing.Quantity != quantity.Value)
                {
                    existing.Quantity = quantity.Value;
                    changedFields.Add("Quantity");
                }
                if (costPerUnit.HasValue && existing.CostPerUnit != costPerUnit)
                {
                    existing.CostPerUnit = costPerUnit;
                    changedFields.Add("CostPerUnit");
                }
                if (usefulLife.HasValue && existing.UsefulLifeYears != usefulLife)
                {
                    existing.UsefulLifeYears = usefulLife;
                    changedFields.Add("UsefulLifeYears");
                }
                if (warrantyExpiry.HasValue && existing.WarrantyExpiry != warrantyExpiry)
                {
                    existing.WarrantyExpiry = warrantyExpiry;
                    changedFields.Add("WarrantyExpiry");
                }
                if (disposalDate.HasValue && existing.DisposalDate != disposalDate)
                {
                    existing.DisposalDate = disposalDate;
                    changedFields.Add("DisposalDate");
                }
                if (disposalValue.HasValue && existing.DisposalValue != disposalValue)
                {
                    existing.DisposalValue = disposalValue;
                    changedFields.Add("DisposalValue");
                }
                if (!string.IsNullOrEmpty(remarks) && existing.Remarks != remarks)
                {
                    existing.Remarks = remarks;
                    changedFields.Add("Remarks");
                }

                var updated = changedFields.Count > 0;
                if (updated)
                    existing.DateModified = DateTime.UtcNow;

                pendingRows.Add(new PendingImportRow(
                    existing,
                    IsNew: false,
                    IsUpdated: updated,
                    OldLocationId: oldLocationId,
                    OldStatus: oldStatus,
                    ChangedFields: changedFields));
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
                pendingRows.Add(new PendingImportRow(newAsset, IsNew: true, IsUpdated: false, null, null, null));
            }
        }

        var failureCount = errors.Select(e => e.Row).Distinct().Count();
        var successCount = 0;

        if (validateOnly)
        {
            _context.ChangeTracker.Clear();
            return new AssetImportResultDTO
            {
                TotalRows = totalDataRows,
                SuccessCount = pendingRows.Count,
                FailureCount = failureCount,
                Errors = errors
            };
        }

        if (pendingRows.Count > 0)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                successCount = await strategy.ExecuteAsync(async () =>
                {
                    var createdCount = 0;
                    var upsertedCount = 0;

                    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        foreach (var pending in pendingRows)
                        {
                            if (pending.IsNew)
                            {
                                _context.Assets.Add(pending.Asset);
                                AddHistory(
                                    userId,
                                    pending.Asset.AssetId,
                                    "CREATE",
                                    $"Asset '{pending.Asset.Name}' with tag '{pending.Asset.AssetTag}' was created via batch import",
                                    oldLocationId: null,
                                    newLocationId: pending.Asset.LocationId,
                                    oldStatus: null,
                                    newStatus: pending.Asset.Status);
                                createdCount++;
                            }
                            else if (pending.IsUpdated)
                            {
                                var fields = pending.ChangedFields is { Count: > 0 }
                                    ? string.Join(", ", pending.ChangedFields)
                                    : "fields";
                                AddHistory(
                                    userId,
                                    pending.Asset.AssetId,
                                    "UPDATE",
                                    $"Asset '{pending.Asset.Name}' with tag '{pending.Asset.AssetTag}' was updated via batch import ({fields})",
                                    oldLocationId: pending.OldLocationId,
                                    newLocationId: pending.Asset.LocationId,
                                    oldStatus: pending.OldStatus,
                                    newStatus: pending.Asset.Status);
                                upsertedCount++;
                            }
                            else
                            {
                                upsertedCount++;
                            }
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return createdCount + upsertedCount;
                    }
                    catch
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Batch asset import failed during save");
                throw new AssetImportException(
                    "Failed to import assets due to a database conflict (for example a duplicate AssetTag). Re-check the file and try again.",
                    ex);
            }
        }

        return new AssetImportResultDTO
        {
            TotalRows = totalDataRows,
            SuccessCount = successCount,
            FailureCount = failureCount,
            Errors = errors
        };
    }

    private async Task<Dictionary<string, Asset>> LoadExistingAssetsByTagsAsync(
        IReadOnlyList<string> tags,
        bool track,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
        if (tags.Count == 0)
            return result;

        // SQL Server has a practical parameter limit; chunk large tag lists.
        const int chunkSize = 1_000;
        for (var i = 0; i < tags.Count; i += chunkSize)
        {
            var chunk = tags.Skip(i).Take(chunkSize).ToList();
            var query = track ? _context.Assets.AsQueryable() : _context.Assets.AsNoTracking();
            var assets = await query
                .Where(a => chunk.Contains(a.AssetTag))
                .ToListAsync(cancellationToken);

            foreach (var asset in assets)
                result[asset.AssetTag] = asset;
        }

        return result;
    }

    private void AddHistory(
        string? userId,
        string assetId,
        string action,
        string description,
        string? oldLocationId,
        string? newLocationId,
        string? oldStatus,
        string? newStatus)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        _context.AssetHistories.Add(new AssetHistory
        {
            AssetId = assetId,
            UserId = userId,
            Action = action,
            Description = description,
            OldLocationId = oldLocationId,
            NewLocationId = newLocationId,
            OldStatus = oldStatus,
            NewStatus = newStatus
        });
    }

    private static ImportErrorDTO RequiredError(int row, string field) =>
        new() { Row = row, Field = field, Message = $"{field} is required for new assets." };

    private static LocationLookup BuildLocationLookup(List<Location> locations)
    {
        var byName = locations
            .GroupBy(l => l.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var byNameCampus = new Dictionary<(string Name, string Campus), Location>(new NameCampusComparer());
        foreach (var loc in locations)
        {
            var key = (loc.Name.Trim(), loc.Campus.Trim());
            // Unique index is (Name, Campus); last write wins only if data already violates that.
            byNameCampus[key] = loc;
        }

        return new LocationLookup(byName, byNameCampus);
    }

    private static (string? LocationId, string? Error, string? ErrorField) ResolveLocation(
        LocationLookup lookup,
        string locName,
        string campus)
    {
        if (!string.IsNullOrEmpty(campus))
        {
            if (lookup.ByNameCampus.TryGetValue((locName, campus), out var exact))
                return (exact.LocationId, null, null);

            return (null, $"Location '{locName}' with Campus '{campus}' not found.", "Location");
        }

        if (!lookup.ByName.TryGetValue(locName, out var matches) || matches.Count == 0)
            return (null, $"Location '{locName}' not found.", "Location");

        if (matches.Count == 1)
            return (matches[0].LocationId, null, null);

        var campuses = string.Join(", ", matches.Select(m => m.Campus).Distinct(StringComparer.OrdinalIgnoreCase));
        return (null,
            $"Location '{locName}' matches multiple campuses ({campuses}). Specify the Campus column.",
            "Campus");
    }

    private static string GetCellString(IXLRangeRow row, Dictionary<string, int> headerMap, string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out var ci))
            return string.Empty;

        var cell = row.Cell(ci + 1);
        if (cell.IsEmpty())
            return string.Empty;

        // Prefer typed values so Excel dates/numbers don't become locale-dependent strings first.
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double number))
            return number.ToString(CultureInfo.InvariantCulture);

        return cell.GetString().Trim();
    }

    private static DateTime? GetDateCell(
        IXLRangeRow row,
        Dictionary<string, int> headerMap,
        string columnName,
        int excelRow,
        List<ImportErrorDTO> errors)
    {
        if (!headerMap.TryGetValue(columnName, out var ci))
            return null;

        var cell = row.Cell(ci + 1);
        if (cell.IsEmpty())
            return null;

        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().Date;

        if (cell.TryGetValue(out DateTime typedDate))
            return typedDate.Date;

        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double oa))
        {
            try
            {
                return DateTime.FromOADate(oa).Date;
            }
            catch
            {
                errors.Add(new ImportErrorDTO { Row = excelRow, Field = columnName, Message = $"Invalid date: '{oa}'." });
                return null;
            }
        }

        var raw = cell.GetString().Trim();
        if (string.IsNullOrEmpty(raw))
            return null;

        var parsed = TryParseDateStrict(raw);
        if (parsed is null)
            errors.Add(new ImportErrorDTO
            {
                Row = excelRow,
                Field = columnName,
                Message = $"Invalid date format: '{raw}'. Use yyyy-MM-dd or dd/MM/yyyy."
            });
        return parsed;
    }

    private static decimal? GetDecimalCell(
        IXLRangeRow row,
        Dictionary<string, int> headerMap,
        string columnName,
        int excelRow,
        List<ImportErrorDTO> errors)
    {
        var raw = GetCellString(row, headerMap, columnName);
        if (string.IsNullOrEmpty(raw))
            return null;

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d;

        errors.Add(new ImportErrorDTO { Row = excelRow, Field = columnName, Message = $"Invalid number: '{raw}'." });
        return null;
    }

    private static int? GetIntCell(
        IXLRangeRow row,
        Dictionary<string, int> headerMap,
        string columnName,
        int excelRow,
        List<ImportErrorDTO> errors)
    {
        var raw = GetCellString(row, headerMap, columnName);
        if (string.IsNullOrEmpty(raw))
            return null;

        // Excel may emit "1" or "1.0" for integers.
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            && d == decimal.Truncate(d)
            && d >= int.MinValue && d <= int.MaxValue)
            return (int)d;

        errors.Add(new ImportErrorDTO { Row = excelRow, Field = columnName, Message = $"Invalid integer: '{raw}'." });
        return null;
    }

    private static DateTime? TryParseDateStrict(string value)
    {
        if (DateTime.TryParseExact(
                value,
                StrictDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
            return dt.Date;

        return null;
    }

    private sealed record PendingImportRow(
        Asset Asset,
        bool IsNew,
        bool IsUpdated,
        string? OldLocationId,
        string? OldStatus,
        List<string>? ChangedFields);

    private sealed record LocationLookup(
        Dictionary<string, List<Location>> ByName,
        Dictionary<(string Name, string Campus), Location> ByNameCampus);

    private sealed class NameCampusComparer : IEqualityComparer<(string Name, string Campus)>
    {
        public bool Equals((string Name, string Campus) x, (string Name, string Campus) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name)
            && StringComparer.OrdinalIgnoreCase.Equals(x.Campus, y.Campus);

        public int GetHashCode((string Name, string Campus) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Campus));
    }
}

/// <summary>
/// Domain/validation failure for asset import (maps to HTTP 400 in the controller).
/// </summary>
public sealed class AssetImportException : Exception
{
    public AssetImportException(string message) : base(message) { }
    public AssetImportException(string message, Exception inner) : base(message, inner) { }
}
