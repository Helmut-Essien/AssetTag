---
name: mobile-app
description: "Use when working on the AssetTag MobileApp project in this workspace. Covers MAUI page flow, dependency injection, view models, navigation, barcode scanning, authentication, and local offline sync."
---

# AssetTag Mobile App Skill

This skill is for the `MobileApp` project in the workspace. It captures the mobile app architecture, main patterns, and developer intent so Copilot can answer questions and assist with changes more accurately.

## What this skill covers

- .NET MAUI app shell and navigation flow
- `MauiProgram.cs` dependency injection setup
- `AppShell` page caching and tab-based navigation
- ViewModel-based MVVM patterns (`MainPageViewModel`, `LoginViewModel`, `InventoryViewModel`, etc.)
- Page and view lifecycle (`SplashScreen`, `LoginPage`, `AddAssetPage`, `LocationsPage`, `SettingsPage`)
- Barcode scanning and asset lookups using ZXing
- Service abstractions for auth, asset management, location management, sync, and navigation
- Shared models in `MobileData` and `Shared`

## Key app characteristics

- Uses `CommunityToolkit.Mvvm`, `Syncfusion`, and `ZXing.Net.Maui`
- Registers pages and view models with DI; singleton pages/view models for instant tab navigation
- Uses `SQLite` via EF Core `LocalDbContext` for offline data storage
- Uses `HttpClient` and token handling for remote API access
- Includes background migration and sync queue behavior from local changes

## Hard rules

- **Offline session:** Never `ClearTokens()` or send the user to login when token refresh fails due to no network, timeout, or a non-auth HTTP status (5xx, 429). Only 401/403 from refresh mean the session is invalid. Local SQLite must keep working. Use `TokenRefreshResult.IsTransientFailure`.
- **Login navigation:** Session expiry uses `ShowLoginAsync()` (hides the tab bar). Never `GoToAsync("/LoginPage")`. `ShowLoginAsync` must pop modals and pop **every tab** to root (Home, Inventory, Locations each have their own stack) so the next login cannot resume Settings/Add Asset.
- **Logout isolation:** Explicit logout must disable biometric keys and clear local SQLite (`ClearAllLocalDataAsync`) so the next user cannot resume the previous session or data. If the wipe fails, abort logout and keep the session. Session expiry must **not** wipe local data.
- **Splash startup:** If tokens exist and migrations/startup throw, retry on splash. Do not `ShowLoginAsync()` for a valid stored session. Retry must start a new migration attempt when the previous task faulted — do not await the same failed task.
- **Tab overlays:** Close singleton-VM overlays (Inventory filter picker) in `OnDisappearing`. They survive tab switches otherwise.
- **Form overlays / back:** Add Asset location picker closes on `OnDisappearing` and Android back. Hardware back on Add/Edit Asset and Add/Edit Location must run the same discard confirm as the in-app back button (`CancelCommand`), not pop the page immediately.
- **Form lifecycle:** Add/Edit Asset `OnAppearing` must not re-run `InitializeAsync()` after a barcode scan or Add Location. Initialize once; then refresh lookups only. Skip lookup refresh while a scan is still applying (`_isHandlingScan`) so it cannot overwrite `LoadAssetAsync`.
- **Modal result TCS:** Never replace a `TaskCompletionSource` in `OnAppearing` if a caller is already awaiting it (barcode scanner). Complete it with null in `OnDisappearing` if still open (password prompt).
- **HTTP 401 retry:** Buffer the body and clone `HttpRequestMessage` before resend. .NET will not send the same message twice.
- **API fallback:** When DEBUG ping succeeds on `FallbackApiUrl`, `ApiEndpointSelector` must be updated so AuthClient and ApiClient actually call that host — not only the ping. Health pings must **not** go through `ApiEndpointHandler` (it rewrites primary URIs to fallback and then looks like primary is up).

## When to use this skill

Use this skill when the task involves:

- Fixing or extending the mobile app UI
- Adding or updating MAUI pages, viewmodels, or shell navigation
- Working with the local SQLite database and EF Core models
- Handling authentication, token refresh, or login flow
- Maintaining asset scan and sync behavior
- Improving performance or page caching in the mobile app

## Important paths

- `MobileApp/MauiProgram.cs`
- `MobileApp/AppShell.xaml` and `MobileApp/AppShell.xaml.cs`
- `MobileApp/Views/`
- `MobileApp/ViewModels/`
- `MobileApp/Services/`
- `MobileData/` for local persistence models
- `Shared/` for DTOs and shared domain models
