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

- **Offline session:** Never `ClearTokens()` or send the user to login when token refresh fails due to no network or timeout. Local SQLite must keep working. Use `TokenRefreshResult.IsTransientFailure`.
- **Login navigation:** Session expiry uses `ShowLoginAsync()` (hides the tab bar). Never `GoToAsync("/LoginPage")`. `ShowLoginAsync` must pop modals and pop **every tab** to root (Home, Inventory, Locations each have their own stack) so the next login cannot resume Settings/Add Asset.
- **Tab overlays:** Close singleton-VM overlays (Inventory filter picker) in `OnDisappearing`. They survive tab switches otherwise.
- **Form lifecycle:** Add/Edit Asset `OnAppearing` must not re-run `InitializeAsync()` after a barcode scan or Add Location. Initialize once; then refresh lookups only. Skip lookup refresh while a scan is still applying (`_isHandlingScan`) so it cannot overwrite `LoadAssetAsync`.
- **Modal result TCS:** Never replace a `TaskCompletionSource` in `OnAppearing` if a caller is already awaiting it (barcode scanner). Complete it with null in `OnDisappearing` if still open (password prompt).
- **HTTP 401 retry:** Buffer the body and clone `HttpRequestMessage` before resend. .NET will not send the same message twice.

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
