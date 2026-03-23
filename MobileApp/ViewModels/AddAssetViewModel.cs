using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Services;
using Shared.Models;
using Shared.Constants;
using SharedLocation = Shared.Models.Location;

namespace MobileApp.ViewModels;

/// <summary>
/// ViewModel for adding a new asset with barcode/QR scanning support
/// </summary>
public partial class AddAssetViewModel : BaseViewModel
{
    private readonly IAssetService _assetService;
    private readonly ILocationService _locationService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string? digitalAssetTag;

    [ObservableProperty]
    private string assetTag = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? serialNumber;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private DateTime? purchaseDate;

    [ObservableProperty]
    private decimal? purchasePrice;

    [ObservableProperty]
    private int quantity = 1;

    [ObservableProperty]
    private decimal? costPerUnit;

    [ObservableProperty]
    private string? vendorName;

    [ObservableProperty]
    private string? invoiceNumber;

    [ObservableProperty]
    private DateTime? warrantyExpiry;

    [ObservableProperty]
    private DateTime? disposalDate;

    [ObservableProperty]
    private decimal? disposalValue;

    [ObservableProperty]
    private string? remarks;

    [ObservableProperty]
    private List<Category> categories = new();

    [ObservableProperty]
    private Category? selectedCategory;

    [ObservableProperty]
    private List<SharedLocation> locations = new();

    [ObservableProperty]
    private SharedLocation? selectedLocation;

    [ObservableProperty]
    private List<Department> departments = new();

    [ObservableProperty]
    private Department? selectedDepartment;

    [ObservableProperty]
    private List<string> statusOptions = new();

    [ObservableProperty]
    private string selectedStatus = AssetConstants.Status.Available;

    [ObservableProperty]
    private List<string> conditionOptions = new();

    [ObservableProperty]
    private string selectedCondition = AssetConstants.Condition.Good;

    [ObservableProperty]
    private string busyMessage = "Loading...";

    public AddAssetViewModel(
        IAssetService assetService,
        ILocationService locationService,
        IAuthService authService)
    {
        _assetService = assetService;
        _locationService = locationService;
        _authService = authService;
        Title = "Add Asset";

        // Initialize status and condition options
        StatusOptions = new List<string>(AssetConstants.Status.All);
        ConditionOptions = new List<string>(AssetConstants.Condition.All);
    }

    /// <summary>
    /// Initialize the form with data from local database
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            BusyMessage = "Loading form data...";

            // Load categories, locations, and departments from local database
            await LoadFormDataAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load form data: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Load categories, locations, and departments from local database
    /// </summary>
    private async Task LoadFormDataAsync()
    {
        try
        {
            // Load from local database (already synced)
            var categoriesTask = LoadCategoriesAsync();
            var locationsTask = _locationService.GetAllLocationsAsync();
            var departmentsTask = LoadDepartmentsAsync();

            await Task.WhenAll(categoriesTask, locationsTask, departmentsTask);

            Categories = await categoriesTask;
            Locations = await locationsTask;
            Departments = await departmentsTask;

            // Auto-select first items if available
            if (Categories.Count > 0) SelectedCategory = Categories[0];
            if (Locations.Count > 0) SelectedLocation = Locations[0];
            if (Departments.Count > 0) SelectedDepartment = Departments[0];
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load form data: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Load categories from local database
    /// </summary>
    private async Task<List<Category>> LoadCategoriesAsync()
    {
        try
        {
            using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .CreateScope(Application.Current!.Handler!.MauiContext!.Services);
            var dbContext = scope.ServiceProvider.GetRequiredService<MobileData.Data.LocalDbContext>();

            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(dbContext.Categories.OrderBy(c => c.Name));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load categories: {ex.Message}", "OK");
            return new List<Category>();
        }
    }

    /// <summary>
    /// Load departments from local database
    /// </summary>
    private async Task<List<Department>> LoadDepartmentsAsync()
    {
        try
        {
            using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .CreateScope(Application.Current!.Handler!.MauiContext!.Services);
            var dbContext = scope.ServiceProvider.GetRequiredService<MobileData.Data.LocalDbContext>();

            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(dbContext.Departments.OrderBy(d => d.Name));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load departments: {ex.Message}", "OK");
            return new List<Department>();
        }
    }

    /// <summary>
    /// Scan barcode or QR code for digital asset tag using optimized ZXing.Net.Maui implementation
    /// </summary>
    [RelayCommand]
    private async Task ScanBarcodeAsync()
    {
        try
        {
            // Check camera permission
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlert(
                        "Permission Denied",
                        "Camera permission is required to scan barcodes. Please enable it in settings.",
                        "OK");
                    return;
                }
            }

            // Create and navigate to scanner page
            var scannerPage = new Views.BarcodeScannerPage();
            await Shell.Current.Navigation.PushModalAsync(scannerPage);

            // Wait for scan result
            var scannedValue = await scannerPage.GetScanResultAsync();

            if (!string.IsNullOrWhiteSpace(scannedValue))
            {
                DigitalAssetTag = scannedValue;
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to scan barcode: {ex.Message}", "OK");
        }
    }


    /// <summary>
    /// Save the new asset
    /// </summary>
    [RelayCommand]
    private async Task SaveAssetAsync()
    {
        if (IsBusy) return;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(AssetTag))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Asset Tag is required", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Asset Name is required", "OK");
            return;
        }

        if (SelectedCategory == null)
        {
            await Shell.Current.DisplayAlert("Validation Error", "Please select a category", "OK");
            return;
        }

        if (SelectedLocation == null)
        {
            await Shell.Current.DisplayAlert("Validation Error", "Please select a location", "OK");
            return;
        }

        if (SelectedDepartment == null)
        {
            await Shell.Current.DisplayAlert("Validation Error", "Please select a department", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Saving asset...";

            var newAsset = new Asset
            {
                AssetTag = AssetTag.Trim(),
                DigitalAssetTag = string.IsNullOrWhiteSpace(DigitalAssetTag) ? null : DigitalAssetTag.Trim(),
                Name = Name.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                SerialNumber = string.IsNullOrWhiteSpace(SerialNumber) ? null : SerialNumber.Trim(),
                CategoryId = SelectedCategory.CategoryId,
                LocationId = SelectedLocation.LocationId,
                DepartmentId = SelectedDepartment.DepartmentId,
                Status = SelectedStatus,
                Condition = SelectedCondition,
                PurchaseDate = PurchaseDate,
                PurchasePrice = PurchasePrice,
                Quantity = Quantity,
                CostPerUnit = CostPerUnit,
                VendorName = string.IsNullOrWhiteSpace(VendorName) ? null : VendorName.Trim(),
                InvoiceNumber = string.IsNullOrWhiteSpace(InvoiceNumber) ? null : InvoiceNumber.Trim(),
                WarrantyExpiry = WarrantyExpiry,
                DisposalDate = DisposalDate,
                DisposalValue = DisposalValue,
                Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim(),
                CreatedAt = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            };

            var (success, message) = await _assetService.CreateAssetAsync(newAsset);

            if (success)
            {
                await Shell.Current.DisplayAlert("Success", message, "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", message, "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to save asset: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Cancel and go back
    /// </summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Confirm",
            "Discard changes and go back?",
            "Yes",
            "No");

        if (confirm)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}