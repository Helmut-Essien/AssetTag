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
    private bool isLocationPickerOpen;

    [ObservableProperty]
    private string locationSearchText = string.Empty;

    [ObservableProperty]
    private List<SharedLocation> filteredLocations = new();

    [ObservableProperty]
    private SharedLocation? selectedFilteredLocation;

    [ObservableProperty]
    private List<Department> departments = new();

    [ObservableProperty]
    private Department? selectedDepartment;

    [ObservableProperty]
    private List<string> statusOptions = new();

    [ObservableProperty]
    private string? selectedStatus;

    [ObservableProperty]
    private List<string> conditionOptions = new();

    [ObservableProperty]
    private string? selectedCondition;

    [ObservableProperty]
    private string busyMessage = "Loading...";

    [ObservableProperty]
    private bool isEditMode = false;

    [ObservableProperty]
    private string pageTitle = "Add Asset";

    [ObservableProperty]
    private string saveButtonText = "Save";

    public string SelectedLocationDisplay =>
        SelectedLocation == null ? "No location selected" : GetLocationDisplayText(SelectedLocation);

    public string SelectedLocationContext =>
        SelectedLocation == null
            ? "Tap 'Choose location' to pick where this asset belongs."
            : GetLocationContextText(SelectedLocation);

    public bool HasLocations => Locations.Count > 0;
    public bool HasFilteredLocations => FilteredLocations.Count > 0;

    // Store the asset ID when editing an existing asset
    private string? _editingAssetId;

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

            // Set mode to Add
            IsEditMode = false;
            PageTitle = "Add Asset";
            SaveButtonText = "Save";
            SelectedCategory = null;
            SelectedLocation = null;
            SelectedDepartment = null;
            SelectedStatus = null;
            SelectedCondition = null;

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
    /// Load an existing asset for editing
    /// </summary>
    public async Task LoadAssetAsync(string assetId)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            BusyMessage = "Loading asset...";

            // Store the asset ID for editing
            _editingAssetId = assetId;

            // Set mode to Edit
            IsEditMode = true;
            PageTitle = "Update Asset";
            SaveButtonText = "Update";

            // Load form data first
            await LoadFormDataAsync();

            // Load the asset
            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
            {
                await Shell.Current.DisplayAlert("Error", "Asset not found", "OK");
                _editingAssetId = null;
                return;
            }

            // Populate form fields - required fields
            AssetTag = asset.AssetTag;
            Name = asset.Name;
            SelectedStatus = asset.Status;
            SelectedCondition = asset.Condition;
            Quantity = asset.Quantity;

            // Populate nullable fields
            DigitalAssetTag = asset.DigitalAssetTag;
            Description = asset.Description;
            SerialNumber = asset.SerialNumber;
            PurchaseDate = asset.PurchaseDate;
            PurchasePrice = asset.PurchasePrice;
            CostPerUnit = asset.CostPerUnit;
            VendorName = asset.VendorName;
            InvoiceNumber = asset.InvoiceNumber;
            WarrantyExpiry = asset.WarrantyExpiry;
            DisposalDate = asset.DisposalDate;
            DisposalValue = asset.DisposalValue;
            Remarks = asset.Remarks;

            // Select the matching category, location, and department
            SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == asset.CategoryId);
            SelectedLocation = Locations.FirstOrDefault(l => l.LocationId == asset.LocationId);
            SelectedDepartment = Departments.FirstOrDefault(d => d.DepartmentId == asset.DepartmentId);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load asset: {ex.Message}", "OK");
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

            ApplyLocationFilter();

            OnPropertyChanged(nameof(HasLocations));
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
    /// Checks if the scanned tag already exists and offers to load that asset
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
                // Check if an asset with this digital tag or asset tag already exists
                using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .CreateScope(Application.Current!.Handler!.MauiContext!.Services);
                var dbContext = scope.ServiceProvider.GetRequiredService<MobileData.Data.LocalDbContext>();
                
                var existingAsset = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .FirstOrDefaultAsync(
                        dbContext.Assets,
                        a => a.DigitalAssetTag == scannedValue || a.AssetTag == scannedValue);

                if (existingAsset != null)
                {
                    // Asset already exists - ask user what to do
                    var loadExisting = await Shell.Current.DisplayAlert(
                        "Asset Already Exists",
                        $"An asset with tag '{scannedValue}' already exists:\n\n" +
                        $"Name: {existingAsset.Name}\n" +
                        $"Asset Tag: {existingAsset.AssetTag}\n\n" +
                        "Would you like to load and edit this existing asset instead?",
                        "Yes, Load It",
                        "No, Use Tag Anyway");

                    if (loadExisting)
                    {
                        // Load the existing asset for editing
                        await LoadAssetAsync(existingAsset.AssetId);
                    }
                    else
                    {
                        // User wants to use the tag anyway (maybe for a different field)
                        DigitalAssetTag = scannedValue;
                    }
                }
                else
                {
                    // No existing asset - just populate the field
                    DigitalAssetTag = scannedValue;
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to scan barcode: {ex.Message}", "OK");
        }
    }


    /// <summary>
    /// Save the asset (creates new or updates existing based on edit mode)
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

        if (string.IsNullOrWhiteSpace(SelectedStatus))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Please select a status", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCondition))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Please select a condition", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Saving asset...";

            var asset = new Asset
            {
                AssetTag = AssetTag.Trim(),
                DigitalAssetTag = string.IsNullOrWhiteSpace(DigitalAssetTag) ? null : DigitalAssetTag.Trim(),
                Name = Name.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                SerialNumber = string.IsNullOrWhiteSpace(SerialNumber) ? null : SerialNumber.Trim(),
                CategoryId = SelectedCategory.CategoryId,
                LocationId = SelectedLocation.LocationId,
                DepartmentId = SelectedDepartment.DepartmentId,
                Status = SelectedStatus!,
                Condition = SelectedCondition!,
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

            bool success;
            string message;

            if (IsEditMode && !string.IsNullOrEmpty(_editingAssetId))
            {
                // Update existing asset
                asset.AssetId = _editingAssetId;
                (success, message) = await _assetService.UpdateAssetAsync(asset);
            }
            else
            {
                // Create new asset
                (success, message) = await _assetService.CreateAssetAsync(asset);
            }

            if (success)
            {
                var actionText = IsEditMode ? "updated" : "created";
                await Shell.Current.DisplayAlert("Success", $"Asset {actionText} successfully", "OK");
                
                // Clear the editing asset ID
                _editingAssetId = null;
                
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

    [RelayCommand]
    private async Task OpenLocationPickerAsync()
    {
        if (!HasLocations)
        {
            var addLocation = await Shell.Current.DisplayAlert(
                "No Locations Available",
                "You don't have any saved locations yet. Add a location first, then assign assets to it.",
                "Add Location",
                "Cancel");

            if (addLocation)
            {
                await Shell.Current.GoToAsync(nameof(Views.AddLocationPage));
            }

            return;
        }

        LocationSearchText = string.Empty;
        ApplyLocationFilter();
        IsLocationPickerOpen = true;
    }

    [RelayCommand]
    private void CloseLocationPicker()
    {
        IsLocationPickerOpen = false;
    }

    [RelayCommand]
    private void SelectLocation(SharedLocation? location)
    {
        if (location == null) return;

        SelectedLocation = location;
        IsLocationPickerOpen = false;
    }

    [RelayCommand]
    private async Task AddLocationAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.AddLocationPage));
    }

    partial void OnLocationSearchTextChanged(string value)
    {
        ApplyLocationFilter();
    }

    partial void OnSelectedLocationChanged(SharedLocation? value)
    {
        OnPropertyChanged(nameof(SelectedLocationDisplay));
        OnPropertyChanged(nameof(SelectedLocationContext));
    }

    partial void OnSelectedFilteredLocationChanged(SharedLocation? value)
    {
        if (value == null) return;

        SelectLocation(value);
        SelectedFilteredLocation = null;
    }

    partial void OnLocationsChanged(List<SharedLocation> value)
    {
        ApplyLocationFilter();
        OnPropertyChanged(nameof(HasLocations));
    }

    private void ApplyLocationFilter()
    {
        var query = LocationSearchText?.Trim();
        IEnumerable<SharedLocation> source = Locations;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.ToLowerInvariant();
            source = source.Where(location =>
                location.Name.ToLowerInvariant().Contains(search) ||
                (!string.IsNullOrWhiteSpace(location.Campus) && location.Campus.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrWhiteSpace(location.Building) && location.Building.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrWhiteSpace(location.Room) && location.Room.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrWhiteSpace(location.Description) && location.Description.ToLowerInvariant().Contains(search)));
        }

        FilteredLocations = source
            .OrderBy(location => location.Name)
            .ToList();
        OnPropertyChanged(nameof(HasFilteredLocations));
    }

    private static string GetLocationDisplayText(SharedLocation location)
    {
        var parts = new List<string> { location.Name };

        if (!string.IsNullOrWhiteSpace(location.Building))
            parts.Add(location.Building);

        if (!string.IsNullOrWhiteSpace(location.Room))
            parts.Add($"Room {location.Room}");

        return string.Join(" | ", parts);
    }

    private static string GetLocationContextText(SharedLocation location)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(location.Campus))
            parts.Add(location.Campus);

        if (!string.IsNullOrWhiteSpace(location.Building))
            parts.Add(location.Building);

        if (!string.IsNullOrWhiteSpace(location.Room))
            parts.Add($"Room {location.Room}");

        if (!string.IsNullOrWhiteSpace(location.Description))
            parts.Add(location.Description);

        return parts.Count == 0 ? "No additional location details available." : string.Join(" | ", parts);
    }
}