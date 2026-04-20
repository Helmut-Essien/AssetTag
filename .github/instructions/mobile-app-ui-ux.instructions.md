---
applyTo: "MobileApp/**/*.xaml"
name: mobile-app-ui-ux
description: "Use when working on AssetTag MobileApp UI/UX, page layout, theming, or mobile screen flow. Helps preserve current MAUI design patterns and accessibility while updating views or styles."
---

# AssetTag Mobile App UI/UX Workflow

Follow this structured approach when working on mobile UI/UX:

## Step 1: Analyze User Requirements
Before making any changes, extract key information:
- **Feature goal**: What specific user problem are we solving?
- **User context**: When and how will users interact with this feature?
- **Visual style preferences**: Any specific style keywords mentioned (clean, professional, modern, etc.)
- **Platform considerations**: iOS/Android specific requirements
- **Performance constraints**: Any known performance concerns

## Step 2: Generate Design System (REQUIRED)
Always start by reviewing and applying the AssetTag Mobile Design System:
1. **Review existing patterns**: Check similar screens for established patterns
2. **Align with brand**: Use colors from `Resources/Styles/Styles.xaml`
3. **Apply spacing system**: Use 4pt-based multiples (4, 8, 12, 16, 20, 24, 32pt)
4. **Follow typography scale**: Use defined font sizes and families
5. **Consider platform adaptations**: Check iOS/Android specific guidelines

## Step 3: Supplement with Detailed Reviews (as needed)
After applying the core design system, review specific areas:
- **Accessibility**: Check touch targets (44pt minimum), contrast ratios, text scaling
- **Performance**: Verify use of compiled bindings, virtualization, image optimization
- **Platform specifics**: Review iOS/Android adaptations and platform conventions
- **Interaction patterns**: Ensure proper feedback, loading states, error handling

## Step 4: Apply Platform Guidelines (MAUI-specific)
Apply MAUI/Mobile-specific best practices:
- **Navigation**: Respect AppShell tab caching and page lifecycle
- **Performance**: Use CollectionView virtualization, avoid excessive nesting
- **Theming**: Use AppThemeBinding for dark/light mode support
- **Input handling**: Use Syncfusion input layouts with proper validation
- **Image handling**: Use appropriate sizes and FontImageSource for icons

## Core Principles to Always Follow

### Visual Design
- Follow the existing Material-inspired visual language and card-based dashboard style
- Preserve theme-aware color usage and app resource tokens from `Resources/Styles/Styles.xaml`
- Use `Border`, `VerticalStackLayout`, `Grid`, and `RefreshView` for simple page structure
- Keep page navigation stable by respecting `AppShell` tab caching patterns
- Prefer accessible touch targets (44pt minimum), readable fonts, and consistent spacing for mobile devices
- Avoid page recreation or layout complexity that could introduce blank-screen behavior on tab changes
- Favor small incremental changes and preserve current biometric/login form flow when modifying authentication screens

### Component Usage
- **Headers**: Use AppBlue (#005A9C) background with white text, 56pt height
- **Buttons**: Primary: #005A9C with white text; Secondary: transparent with #005A9C stroke
- **Cards**: White background with shadow, 12pt corner radius, 80pt height
- **Search Bars**: White background with shadow, 24pt corner radius
- **Input Fields**: Use SfTextInputLayout with Outlined style, 20pt corner radius
- **Icons**: Use Material Icons library with consistent sizing (24pt standard)
- **Loading States**: Use skeleton loaders or activity indicators with proper messaging
- **Empty States**: Show when appropriate with helpful guidance text
- **Error Messages**: Use consistent styling with visible background and text colors

### Professional UI Rules
**Do:**
- Use theme colors directly from Resources/Styles/Styles.xaml
- Provide visual feedback for all interactive elements (color, shadow, elevation changes)
- Use smooth transitions (150-300ms) for state changes
- Ensure 4.5:1 contrast ratio for normal text, 3:1 for large text
- Use consistent icon sizes from Material Icons library
- Apply AppThemeBinding for dark/light mode support
- Use x:DataType for compiled bindings (2-3x performance improvement)
- Follow 4pt spacing system for all layout and padding
- Provide cursor feedback equivalent for touch interactions

**Don't:**
- Hard-code colors outside of Resources/Styles/Styles.xaml
- Use emojis as icons (always use SVG/Material Icons)
- Create layout shifts on hover/press states
- Use invisible borders or low-contrast text
- Mix different container widths or max-width values
- Forget to test both light and dark modes
- Overlook accessibility requirements (screen reader labels, touch targets)

### Pre-Delivery Checklist
Before considering UI work complete, verify:
- [ ] Uses theme colors from Resources/Styles/Styles.xaml (no hardcoded colors)
- [ ] All icons from Material Icons library with consistent sizing
- [ ] Interactive elements provide clear visual feedback
- [ ] Transitions are smooth (150-300ms duration)
- [ ] Text meets contrast requirements (4.5:1 normal, 3:1 large)
- [ ] Touch targets are minimum 44pt × 44pt
- [ ] Supports dark mode via AppThemeBinding
- [ ] Uses compiled bindings (x:DataType) where applicable
- [ ] Follows 4pt spacing system (all values multiples of 4)
- [ ] Has appropriate loading state (skeleton or spinner)
- [ ] Has empty state when applicable (no data scenarios)
- [ ] Has error state when applicable (validation, network errors)
- [ ] Respects AppShell tab caching and navigation patterns
- [ ] Optimized images (appropriate sizes, FontImageSource for icons)