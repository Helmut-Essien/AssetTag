---
name: mobile-app-ui-ux
description: "Use when working on the AssetTag MobileApp UI/UX. Covers MAUI page styling, layout patterns, shell navigation, theming, app feedback, login experience, and current screen design." 
---

# AssetTag Mobile App UI/UX Skill

This skill captures the current UI/UX implementation of the `MobileApp` project so Copilot can help with design, layout, interaction, and style changes more accurately.

## What this skill covers

- MAUI UI structure for the app shell and tab navigation
- `MainPage` dashboard design, summary cards, sync status, and skeleton loading states
- `LoginPage` gradient branding, biometric login, and form layout
- App theming and style defaults in `Resources/Styles/Styles.xaml`
- Use of `Border`, `VerticalStackLayout`, `Grid`, `RefreshView`, and `ScrollView` for mobile layout
- Material icon usage and icon button interactions
- Sync and scan feedback patterns used in the app
- Page caching and smooth navigation patterns in `AppShell.xaml`/`AppShell.xaml.cs`
- Consistent input styling via Syncfusion controls and custom visual state handling

## Key UI/UX characteristics

- Modern card-based dashboard with soft shadows and rounded corners
- Clean header and tabbed workflow optimized for field operations
- Gradient login screen with decorative background elements
- Compact, accessible form controls with consistent spacing and touch targets
- Dynamic feedback for loading states via skeleton loaders
- Use of custom resource tokens and theme-aware colors
- Navigation flows intentionally decoupled from page recreation to prevent blank screens

## When to use this skill

Use this skill when the task requires:

- improving mobile UI layout, spacing, responsiveness, or theming
- updating login and authentication screens
- refining asset dashboard cards, sync status, or quick action flows
- changing tab navigation, shell visibility or page transition behavior
- applying consistent visual styling across pages
- solving UI/UX issues such as black screens, slow tab switching, or poor loading feedback

## Important paths

- `MobileApp/MainPage.xaml`
- `MobileApp/Views/LoginPage.xaml`
- `MobileApp/AppShell.xaml`
- `MobileApp/AppShell.xaml.cs`
- `MobileApp/Resources/Styles/Styles.xaml`
- `MobileApp/Controls/SkeletonLoader.xaml`
- `MobileApp/Views/InventoryPage.xaml`
- `MobileApp/Views/LocationsPage.xaml`
- `MobileApp/Views/SettingsPage.xaml`
