---
name: mobile-app-ui-ux
description: "Use when working on AssetTag MobileApp UI/UX. Covers MAUI page styling, layout patterns, shell navigation, theming, app feedback, and current screen design across the mobile app." 
---

# AssetTag Mobile App UI/UX Skill

This skill captures the current UI/UX implementation of the `MobileApp` project so Copilot can help with design, layout, interaction, and style changes more accurately.

## What this skill covers

- MAUI UI structure for the app shell and tab navigation
- `MainPage` dashboard design with card-based summaries, sync controls, and skeleton loading states
- `LoginPage` gradient onboarding screen with biometric and form UX
- `InventoryPage` and `LocationsPage` search/filter patterns, chip-style filters, and card list item layouts
- `SettingsPage` settings groups, toggle patterns, and data management actions
- App theming and reusable style defaults in `Resources/Styles/Styles.xaml`
- Use of `Border`, `Frame`, `VerticalStackLayout`, `Grid`, `RefreshView`, `ScrollView`, and `CollectionView`
- Material iconography, icon buttons, and accessible touch targets
- Existing custom visual state handling and color resources for dark/light themes
- The current brand palette: primary blue `#005A9C`, white cards on `#F8F9FA`, success green `#4CAF50`, warning orange `#FF9800`, and neutral text `#333333`/`#666666`

## Key UI/UX characteristics

- White card surfaces with subtle shadow and 12-16dp corner radius for hierarchy
- Light gray page background (`#F8F9FA`) with blue primary headers and accent controls
- Login screen gradient from `#512BD4` to `#2B0B98`, with soft white decorative circles
- Consistent horizontal padding around `16`, `20`, `24`, and `30` points across pages
- Card content uses 16-18sp headline text and 13-14sp supporting text for readability
- Clear action affordances using bordered icon cards, tap areas, and chevrons
- High contrast text on colored headers/buttons and accessible contrast for body text
- Skeleton loading states for dashboard, list, and settings screens
- Stable navigation by caching tab pages in `AppShell` and minimizing page recreation

## When to use this skill

Use this skill when the task requires:

- improving mobile UI layout, spacing, responsiveness, or theming
- updating login, authentication, or security screens
- refining dashboard cards, quick action tiles, or sync feedback flows
- adjusting list item cards, search filters, or empty/loading states
- maintaining consistent color contrast and accessible touch targets
- preserving app navigation and tab caching patterns while changing UI

## Important paths

- `MobileApp/MainPage.xaml`
- `MobileApp/Views/LoginPage.xaml`
- `MobileApp/Views/InventoryPage.xaml`
- `MobileApp/Views/LocationsPage.xaml`
- `MobileApp/Views/SettingsPage.xaml`
- `MobileApp/Resources/Styles/Styles.xaml`
- `MobileApp/AppShell.xaml`
- `MobileApp/AppShell.xaml.cs`
- `MobileApp/Controls/SkeletonLoader.xaml`
