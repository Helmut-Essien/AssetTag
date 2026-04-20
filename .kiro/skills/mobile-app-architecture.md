# Mobile App Architecture & Development Guide

## Project Overview

**AssetTag Mobile App** - A cross-platform .NET MAUI mobile application for offline-first asset management with barcode scanning, biometric authentication, and real-time synchronization.

**Target Platform**: Android (primary), iOS/MacCatalyst (commented out for CI/CD)  
**Framework**: .NET 9.0 MAUI  
**Architecture**: MVVM with offline-first data synchronization  
**Database**: SQLite with Entity Framework Core

---

## Core Architecture Patterns

### 1. MVVM Pattern with CommunityToolkit.Mvvm

**All ViewModels MUST**:
- Inherit from `BaseViewModel` (which inherits from `ObservableObject`)
- Use `[ObservableProperty]` attributes for bindable properties (generates backing fields automatically)
- Use `[RelayCommand]` attributes for commands (generates ICommand implementations)
- Implement token validation before API calls using `ValidateTokenAsync()`

**Example ViewModel Pattern**:
```csharp
public partial class MyViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IAssetService _assetService;
    
    [ObservableProperty]
    private string searchText = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAssets))]
    private ObservableCollection<Asset> assets = new();
    
    public bool HasAssets => Assets.Any();
    
    public MyViewModel(IAuthService authService, IAssetService assetService)
    {
        _authService = authService;
        _assetService = assetService;
    }
    
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (IsBusy) return;
        
        IsBusy = true;
        try
        {
            // ALWAYS validate token before API calls
            if (!await ValidateTokenAsync(_authService))
                return;
            
            var result = await _assetService.GetAssetsAsync();
            Assets = new ObservableCollection<Asset>(result);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### 2. Dependency Injection Lifecycle

**Service Registration in MauiProgram.cs**:

```csharp
// Singleton Services (created once, live for app lifetime)
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddSingleton<BackgroundSyncService>();
builder.Services.AddSingleton<IVersionCheckService, VersionCheckService>();

// Scoped Services (new instance per scope, used with DbContext)
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<ILocationService, LocationService>();

// Transient Services (new instance every time)
builder.Services.AddTransient<TokenRefreshHandler>();

// PERFORMANCE: Singleton ViewModels for instant navigation
builder.Services.AddSingleton<MainPageViewModel>();
builder.Services.AddSingleton<InventoryViewModel>();
builder.Services.AddSingleton<LocationsViewModel>();

// Transient ViewModels for add/edit pages
builder.Services.AddTransient<AddLocationViewModel>();
builder.Services.AddTransient<EditLocationViewModel>();
```

**CRITICAL RULES**:
- Services using `LocalDbContext` MUST be Scoped (not Singleton)
- ViewModels for tab pages SHOULD be Singleton for performance
- ViewModels for modal/add/edit pages SHOULD be Transient
- Pages follow same pattern as ViewModels

### 3. Offline-First Data Architecture

**Database**: SQLite (`AssetTagOffline.db3` in `FileSystem.AppDataDirectory`)

**Key Entities**:
- `Asset` - Physical/digital assets
- `Category` - Asset categories with depreciation rates
- `Location` - Physical locations with GPS coordinates
- `Department` - Organizational departments
- `SyncQueueItem` - Pending changes to sync to server
- `DeviceInfo` - Device ID and last sync timestamp

**Sync Strategy**:
```
1. User makes changes → Saved to local SQLite
2. Changes tracked in SyncQueue table
3. Background sync pushes changes to server
4. Pull sync fetches server changes
5. Conflict resolution: Server wins (last-write-wins)
```

**CRITICAL: Change Tracking Control**:
```csharp
// During PULL sync, DISABLE change tracking to prevent creating SyncQueue entries
try
{
    dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
    
    // ... apply server changes ...
    
    await dbContext.SaveChangesAsync();
}
finally
{
    // ALWAYS re-enable in finally block
    dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
}
```

### 4. Authentication & Token Management

**Token Storage**: `SecureStorage` (platform-specific secure storage)

**Token Lifecycle**:
1. Login → Store access token + refresh token
2. API calls → Attach access token to Authorization header
3. Token expires → `TokenRefreshHandler` automatically refreshes
4. Refresh fails → Redirect to login

**Biometric Authentication Flow**:
```csharp
// Enable biometric
await _authService.EnableBiometricAuthenticationAsync(email, password);

// Login with biometric
var (success, token, message) = await _authService.BiometricLoginAsync();
// This will:
// 1. Prompt for fingerprint/Face ID
// 2. Try to use existing tokens
// 3. Refresh tokens if expired
// 4. Re-authenticate with stored credentials if refresh fails
```

**Token Validation Pattern** (MUST use in all ViewModels):
```csharp
protected async Task<bool> ValidateTokenAsync(IAuthService authService)
{
    var (accessToken, refreshToken) = await authService.GetStoredTokensAsync();
    
    if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
    {
        await NavigateToLoginAsync();
        return false;
    }
    
    if (await authService.IsTokenExpiredAsync())
    {
        var (success, newTokens, message) = await authService.RefreshTokenAsync();
        if (!success)
        {
            authService.ClearTokens();
            await NavigateToLoginAsync();
            return false;
        }
    }
    
    return true;
}
```

---

## Package Usage Guide

### UI Components

**1. UraniumUI.Material + UraniumUI.Icons.MaterialIcons**
```xml
<!-- Material Design TextField -->
<material:TextField Title="Asset Name" 
                    Text="{Binding AssetName}"
                    Icon="{mi:Material Icon=Inventory}"/>

<!-- Material Design Button -->
<material:Button Text="Save" 
                 Command="{Binding SaveCommand}"
                 StyleClass="FilledButton"/>
```

**2. CommunityToolkit.Maui**
```csharp
// Behaviors
<Entry Text="{Binding Email}">
    <Entry.Behaviors>
        <toolkit:EmailValidationBehavior />
    </Entry.Behaviors>
</Entry>

// Converters
<Label IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}"/>
```

**3. Syncfusion.Maui.Toolkit**
```xml
<!-- Charts for dashboard -->
<sfchart:SfCartesianChart>
    <sfchart:SfCartesianChart.Series>
        <sfchart:ColumnSeries ItemsSource="{Binding ChartData}"/>
    </sfchart:SfCartesianChart.Series>
</sfchart:SfCartesianChart>
```

### Barcode Scanning (ZXing.Net.Maui.Controls)

**Implementation Pattern**:
```xml
<zxing:CameraBarcodeReaderView x:Name="CameraView"
                                IsDetecting="True"
                                IsTorchOn="False"
                                BarcodesDetected="OnBarcodesDetected" />
```

```csharp
private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
{
    var barcode = e.Results.FirstOrDefault();
    if (barcode != null)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            CameraView.IsDetecting = false;
            await ProcessBarcodeAsync(barcode.Value);
        });
    }
}
```

**CRITICAL**: Always stop detection before processing to prevent multiple scans:
```csharp
CameraView.IsDetecting = false;
```

### HTTP Client with Polly Resilience

**Configuration**:
```csharp
builder.Services.AddHttpClient("ApiClient", (sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<ApiSettings>>().Value;
    client.BaseAddress = new Uri(settings.PrimaryApiUrl);
    client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeout);
})
.AddHttpMessageHandler<TokenRefreshHandler>(); // Automatic token refresh
```

**TokenRefreshHandler** automatically:
- Checks if token is expired before each request
- Refreshes token if needed
- Retries request with new token
- Redirects to login if refresh fails

### Entity Framework Core with SQLite

**DbContext Configuration**:
```csharp
string dbPath = Path.Combine(FileSystem.AppDataDirectory, "AssetTagOffline.db3");

builder.Services.AddDbContext<LocalDbContext>((serviceProvider, options) =>
{
    options.UseSqlite($"Data Source={dbPath};Cache=Shared",
        sqliteOptions => sqliteOptions.CommandTimeout(30));
    
#if DEBUG
    options.EnableSensitiveDataLogging();
    options.EnableDetailedErrors();
#endif
});
```

**Migration Strategy**:
```csharp
// Non-blocking background migration
public class MigrationBackgroundService
{
    public MigrationBackgroundService(IServiceProvider serviceProvider)
    {
        Task.Run(async () =>
        {
            await Task.Delay(100); // Let UI initialize first
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
            await dbContext.Database.MigrateAsync();
        });
    }
}
```

---

## Critical Implementation Patterns

### 1. Sync Service Patterns

**Push Changes** (Local → Server):
```csharp
public async Task<(bool Success, string Message)> PushChangesAsync()
{
    // 1. Check connectivity
    if (!await _authService.IsConnectedToInternet())
        return (false, "No internet connection");
    
    // 2. Get pending changes from SyncQueue
    var pendingItems = await dbContext.SyncQueue
        .OrderBy(s => s.CreatedAt)
        .ToListAsync();
    
    // 3. Send to server
    var response = await httpClient.PostAsJsonAsync("api/sync/push", request);
    
    // 4. Remove only successful items from queue
    var successfulItems = pendingItems
        .Where(item => result.SuccessfulOperationIds.Contains(item.Id))
        .ToList();
    dbContext.SyncQueue.RemoveRange(successfulItems);
    
    // 5. Increment retry count for failed items
    var failedItems = pendingItems
        .Where(item => !result.SuccessfulOperationIds.Contains(item.Id))
        .ToList();
    foreach (var failedItem in failedItems)
        failedItem.RetryCount++;
    
    await dbContext.SaveChangesAsync();
}
```

**Pull Changes** (Server → Local):
```csharp
public async Task<(bool Success, string Message)> PullChangesAsync()
{
    try
    {
        // CRITICAL: Disable change tracking during pull
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        
        // Sync in dependency order:
        // 1. Categories (no dependencies)
        // 2. Locations (no dependencies)
        // 3. Departments (no dependencies)
        // 4. Assets (depends on all above)
        
        foreach (var categoryDto in result.Categories)
        {
            var existing = await dbContext.Categories.FindAsync(categoryDto.CategoryId);
            if (existing != null)
            {
                // Update existing
                existing.Name = categoryDto.Name;
                // ... update other fields
            }
            else
            {
                // Insert new
                dbContext.Categories.Add(new Category { /* ... */ });
            }
        }
        
        await dbContext.SaveChangesAsync();
        
        // Process assets in batches to avoid memory spikes
        const int BATCH_SIZE = 200;
        for (int offset = 0; offset < result.Assets.Count; offset += BATCH_SIZE)
        {
            var batch = result.Assets.Skip(offset).Take(BATCH_SIZE);
            // ... process batch
            await dbContext.SaveChangesAsync();
        }
        
        // Update LastSync timestamp
        deviceInfo.LastSync = result.ServerTimestamp;
        dbContext.Entry(deviceInfo).Property(d => d.LastSync).IsModified = true;
        await dbContext.SaveChangesAsync();
    }
    finally
    {
        // ALWAYS re-enable change tracking
        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
    }
}
```

### 2. Navigation Patterns

**Shell Navigation**:
```csharp
// Navigate to page
await Shell.Current.GoToAsync(nameof(InventoryPage));

// Navigate with parameters
await Shell.Current.GoToAsync($"{nameof(EditLocationPage)}?LocationId={locationId}");

// Navigate to root (login)
await Shell.Current.GoToAsync($"/{nameof(LoginPage)}");

// Go back
await Shell.Current.GoToAsync("..");
```

**Query Parameters**:
```csharp
[QueryProperty(nameof(LocationId), "LocationId")]
public partial class EditLocationViewModel : BaseViewModel
{
    private string _locationId = string.Empty;
    
    public string LocationId
    {
        get => _locationId;
        set
        {
            _locationId = value;
            LoadLocationAsync(value);
        }
    }
}
```

### 3. Loading States & Skeleton Loaders

**Custom Skeleton Loader**:
```xml
<controls:SkeletonLoader IsVisible="{Binding IsBusy}"
                          SkeletonType="List"
                          ItemCount="5"/>

<CollectionView ItemsSource="{Binding Assets}"
                IsVisible="{Binding IsNotBusy}">
    <!-- ... -->
</CollectionView>
```

**SkeletonType Options**:
- `List` - List item placeholders
- `Card` - Card placeholders
- `Text` - Text line placeholders

### 4. Error Handling Patterns

**ViewModel Error Handling**:
```csharp
[RelayCommand]
private async Task SaveAsync()
{
    if (IsBusy) return;
    
    IsBusy = true;
    try
    {
        // Validate token first
        if (!await ValidateTokenAsync(_authService))
            return;
        
        // Perform operation
        var result = await _assetService.CreateAssetAsync(asset);
        
        if (result.Success)
        {
            await Shell.Current.DisplayAlert("Success", "Asset created", "OK");
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlert("Error", result.Message, "OK");
        }
    }
    catch (HttpRequestException ex)
    {
        await Shell.Current.DisplayAlert("Network Error", 
            "Please check your connection", "OK");
    }
    catch (Exception ex)
    {
        await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
    }
    finally
    {
        IsBusy = false;
    }
}
```

### 5. Background Services

**BackgroundSyncService Pattern**:
```csharp
public class BackgroundSyncService
{
    private readonly PeriodicTimer _timer;
    private readonly ISyncService _syncService;
    
    public BackgroundSyncService(ISyncService syncService)
    {
        _syncService = syncService;
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        
        // Start background sync loop
        Task.Run(SyncLoopAsync);
    }
    
    private async Task SyncLoopAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            try
            {
                await _syncService.EnqueueFullSyncAsync();
            }
            catch (Exception ex)
            {
                // Log but don't crash
                System.Diagnostics.Debug.WriteLine($"Background sync error: {ex.Message}");
            }
        }
    }
}
```

---

## Performance Optimization Patterns

### 1. Compiled Bindings

**ALWAYS use x:DataType for compiled bindings**:
```xml
<ContentPage xmlns:vm="clr-namespace:MobileApp.ViewModels"
             x:DataType="vm:InventoryViewModel">
    
    <Label Text="{Binding AssetName}"/> <!-- 2-3x faster than runtime binding -->
</ContentPage>
```

### 2. Singleton ViewModels for Tab Pages

**Why**: Eliminates recreation delays, provides instant navigation

```csharp
// Register as Singleton
builder.Services.AddSingleton<InventoryViewModel>();
builder.Services.AddSingleton<InventoryPage>();

// Initialize in OnAppearing instead of constructor
public partial class InventoryPage : ContentPage
{
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((InventoryViewModel)BindingContext).InitializeAsync();
    }
}
```

### 3. Batch Database Operations

```csharp
// BAD: Individual saves
foreach (var asset in assets)
{
    dbContext.Assets.Add(asset);
    await dbContext.SaveChangesAsync(); // Slow!
}

// GOOD: Batch save
dbContext.Assets.AddRange(assets);
await dbContext.SaveChangesAsync(); // Fast!

// BETTER: Batch with size limit
const int BATCH_SIZE = 200;
for (int i = 0; i < assets.Count; i += BATCH_SIZE)
{
    var batch = assets.Skip(i).Take(BATCH_SIZE);
    dbContext.Assets.AddRange(batch);
    await dbContext.SaveChangesAsync();
}
```

### 4. Async/Await Best Practices

```csharp
// ALWAYS use ConfigureAwait(false) in services (not UI code)
public async Task<List<Asset>> GetAssetsAsync()
{
    var assets = await dbContext.Assets
        .ToListAsync()
        .ConfigureAwait(false); // Improves performance
    return assets;
}

// DON'T use ConfigureAwait in ViewModels (need UI context)
[RelayCommand]
private async Task LoadDataAsync()
{
    var assets = await _assetService.GetAssetsAsync();
    Assets = new ObservableCollection<Asset>(assets); // Needs UI thread
}
```

---

## Testing & Debugging

### Debug Logging

```csharp
#if DEBUG
System.Diagnostics.Debug.WriteLine($"Token expires: {jwt.ValidTo}");
#endif

// Or use ILogger
_logger.LogInformation("Sync completed: {Count} items", count);
_logger.LogWarning("Token expired, refreshing...");
_logger.LogError(ex, "Sync failed");
```

### Database Inspection

```bash
# Connect to SQLite database
adb pull /data/data/helmutcodesolutions.assettag.mobileapp/files/AssetTagOffline.db3
sqlite3 AssetTagOffline.db3

# View tables
.tables

# View sync queue
SELECT * FROM SyncQueue;

# View device info
SELECT * FROM DeviceInfo;
```

---

## Common Pitfalls & Solutions

### 1. DbContext Lifetime Issues

**Problem**: Singleton service using DbContext causes "disposed context" errors

**Solution**: Use Scoped services or create new scope:
```csharp
// BAD: Singleton with DbContext
builder.Services.AddSingleton<IAssetService, AssetService>();

// GOOD: Scoped service
builder.Services.AddScoped<IAssetService, AssetService>();

// OR: Create scope in Singleton
using var scope = _serviceProvider.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
```

### 2. Sync Queue Pollution

**Problem**: Pull sync creates SyncQueue entries, causing infinite sync loop

**Solution**: Disable change tracking during pull:
```csharp
try
{
    dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
    // ... apply server changes ...
}
finally
{
    dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
}
```

### 3. Token Refresh Race Conditions

**Problem**: Multiple concurrent requests trigger multiple token refreshes

**Solution**: Use SemaphoreSlim to serialize refreshes:
```csharp
private static readonly SemaphoreSlim _refreshLock = new(1, 1);

public async Task<TokenResponseDTO> RefreshTokenAsync()
{
    await _refreshLock.WaitAsync();
    try
    {
        // Check if token was recently refreshed
        if (IsTokenFresh()) return currentToken;
        
        // Refresh token
        var newToken = await _httpClient.PostAsync(...);
        return newToken;
    }
    finally
    {
        _refreshLock.Release();
    }
}
```

### 4. Barcode Scanner Multiple Detections

**Problem**: Barcode detected multiple times before processing completes

**Solution**: Stop detection immediately:
```csharp
private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
{
    CameraView.IsDetecting = false; // Stop immediately
    
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        await ProcessBarcodeAsync(e.Results.FirstOrDefault()?.Value);
    });
}
```

---

## Configuration Files

### appsettings.json

```json
{
  "ApiSettings": {
    "PrimaryApiUrl": "https://mugassetapi.runasp.net/",
    "FallbackApiUrl": "https://localhost:7135/",
    "RequestTimeout": 30
  }
}
```

**Loading Configuration**:
```csharp
var assembly = Assembly.GetExecutingAssembly();
using var stream = assembly.GetManifestResourceStream("MobileApp.appsettings.json");

var config = new ConfigurationBuilder()
    .AddJsonStream(stream)
    .Build();

builder.Services.Configure<ApiSettings>(config.GetSection("ApiSettings"));
```

---

## Code Style & Conventions

### Naming Conventions

- **ViewModels**: `{Feature}ViewModel.cs` (e.g., `InventoryViewModel.cs`)
- **Views**: `{Feature}Page.xaml` (e.g., `InventoryPage.xaml`)
- **Services**: `I{Service}.cs` interface, `{Service}.cs` implementation
- **Commands**: `{Action}Command` (e.g., `SaveCommand`, `DeleteCommand`)
- **Observable Properties**: camelCase with `[ObservableProperty]` attribute

### File Organization

```
MobileApp/
├── Configuration/       # App settings classes
├── Controls/           # Custom controls
├── Converters/         # Value converters
├── Services/           # Business logic services
├── ViewModels/         # MVVM ViewModels
├── Views/              # XAML pages
├── App.xaml            # App-level resources
├── AppShell.xaml       # Shell navigation
└── MauiProgram.cs      # DI configuration
```

---

## When to Use Each Pattern

| Scenario | Pattern | Example |
|----------|---------|---------|
| Tab page ViewModel | Singleton | `InventoryViewModel` |
| Modal/Add/Edit ViewModel | Transient | `AddAssetViewModel` |
| Service with DbContext | Scoped | `AssetService` |
| Stateless service | Singleton | `AuthService` |
| Background service | Singleton | `BackgroundSyncService` |
| API call | HttpClient with TokenRefreshHandler | All API calls |
| Navigation | Shell.Current.GoToAsync | All navigation |
| Loading state | IsBusy + SkeletonLoader | All data loading |
| Error handling | try/catch + DisplayAlert | All user operations |

---

## Quick Reference Commands

```csharp
// Navigation
await Shell.Current.GoToAsync(nameof(PageName));
await Shell.Current.GoToAsync("..");

// Alerts
await Shell.Current.DisplayAlert("Title", "Message", "OK");
var result = await Shell.Current.DisplayAlert("Title", "Message", "Yes", "No");

// Token validation
if (!await ValidateTokenAsync(_authService)) return;

// Sync operations
await _syncService.EnqueuePushAsync();
await _syncService.EnqueueFullSyncAsync();

// Secure storage
await SecureStorage.SetAsync("key", "value");
var value = await SecureStorage.GetAsync("key");
SecureStorage.Remove("key");

// Main thread
MainThread.BeginInvokeOnMainThread(() => { /* UI code */ });
await MainThread.InvokeOnMainThreadAsync(async () => { /* async UI code */ });
```

---

## Version Information

- **.NET**: 9.0
- **MAUI**: Latest (via $(MauiVersion))
- **Target OS**: Android 21+, iOS 15+
- **Database**: SQLite 3
- **Min Android SDK**: 21 (Android 5.0 Lollipop)

---

*This guide should be updated whenever architectural patterns or critical implementations change.*
