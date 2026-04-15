---
applyTo:
  - "MobileApp/**/*.xaml"
  - "MobileApp/**/*.cs"
name: mobile-app-ui-ux
description: "Use when working on AssetTag MobileApp UI/UX, page layout, theming, or mobile screen flow. Helps preserve current MAUI design patterns, spacing, color contrast, and accessibility while updating views or styles."
---

When editing the AssetTag mobile app UI:

- follow the existing Material-inspired visual language, card-based dashboard, and branded blue accent palette
- preserve theme-aware color usage and resource tokens from `Resources/Styles/Styles.xaml`
- keep page navigation stable by respecting `AppShell` tab caching patterns
- prefer accessible touch targets, readable fonts, and consistent spacing for mobile devices
- use `Border`, `Frame`, `VerticalStackLayout`, `Grid`, `RefreshView`, and `CollectionView` for simple page structure
- preserve the light gray page background (`#F8F9FA`) and white card surfaces with subtle shadows
- maintain the login page gradient style while keeping high contrast text and button states
- keep spacing consistent across pages: use values like 16, 20, 24, 30 for padding and 12-16 for card spacing
- ensure text contrast is strong on colored headers/buttons and use neutral text colors (`#333333`, `#666666`) for body content
- avoid page recreation or layout complexity that could introduce blank-screen behavior on tab changes
- favor small incremental changes and preserve current biometric/login form flow when modifying authentication screens
