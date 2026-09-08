using Shared.DTOs;

namespace AssetTag.Services;

public interface IAssetImportService
{
    /// <summary>
    /// Parses an .xlsx asset import workbook and upserts valid rows (or validates only).
    /// </summary>
    /// <param name="stream">Workbook stream (not disposed by the service).</param>
    /// <param name="userId">Authenticated user id for asset history; may be null.</param>
    /// <param name="validateOnly">When true, run validation and skip persistence.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AssetImportResultDTO> ImportAsync(
        Stream stream,
        string? userId,
        bool validateOnly = false,
        CancellationToken cancellationToken = default);
}
