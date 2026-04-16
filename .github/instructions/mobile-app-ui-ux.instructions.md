---
applyTo: "MobileApp/**/*.xaml"
name: mobile-app-ui-ux
description: "Use when working on AssetTag MobileApp UI/UX, page layout, theming, or mobile screen flow. Helps preserve current MAUI design patterns and accessibility while updating views or styles."
---

When editing the AssetTag mobile app UI:

- follow the existing Material-inspired visual language and card-based dashboard style
- preserve theme-aware color usage and app resource tokens from `Resources/Styles/Styles.xaml`
- keep page navigation stable by respecting `AppShell` tab caching patterns
- prefer accessible touch targets, readable fonts, and consistent spacing for mobile devices
- use `Border`, `VerticalStackLayout`, `Grid`, and `RefreshView` for simple page structure
- avoid page recreation or layout complexity that could introduce blank-screen behavior on tab changes
- favor small incremental changes and preserve current biometric/login form flow when modifying authentication screens
