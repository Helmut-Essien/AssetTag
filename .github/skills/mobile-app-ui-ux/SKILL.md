# Mobile App UI/UX Design System & Guidelines

## Design Philosophy

**AssetTag Mobile App** follows a modern, clean, and professional design language with emphasis on:
- **Clarity**: Clear visual hierarchy and readable typography
- **Efficiency**: Quick access to common actions with minimal taps
- **Consistency**: Unified design patterns across all screens
- **Accessibility**: 44pt minimum touch targets, high contrast, readable fonts
- **Performance**: Smooth animations, skeleton loaders, instant feedback

---

## UX Workflow

When updating AssetTag mobile UI, apply a lightweight design-system approach:
- identify the feature goal, user context, and preferred visual style before editing screens
- match new layouts to existing brand palette, semantic colors, typography scale, and 4pt spacing
- reuse app resource tokens and avoid hard-coded colors outside `Resources/Styles/Styles.xaml`
- prefer simple component patterns (cards, buttons, search bars, filters) over complex custom controls
- validate changes against Shell navigation, tab caching behavior, and page lifecycle expectations

---

## Color System

### Primary Colors

```xml
<!-- Brand Colors -->
<Color x:Key="Primary">#512BD4</Color>          <!-- Purple - Primary brand -->
<Color x:Key="PrimaryDark">#ac99ea</Color>      <!-- Light purple - Dark mode -->
<Color x:Key="Tertiary">#2B0B98</Color>         <!-- Deep purple - Accents -->
<Color x:Key="Secondary">#DFD8F7</Color>        <!-- Light purple - Backgrounds -->

<!-- Functional Colors -->
<Color x:Key="BrandGreen">#7CB342</Color>       <!-- Success states -->
<Color x:Key="Magenta">#D600AA</Color>          <!-- Highlights -->
```

### Neutral Colors

```xml
<!-- Grayscale Palette -->
<Color x:Key="Gray100">#E1E1E1</Color>          <!-- Lightest gray -->
<Color x:Key="Gray200">#C8C8C8</Color>          <!-- Light gray -->
<Color x:Key="Gray300">#ACACAC</Color>          <!-- Medium-light gray -->
<Color x:Key="Gray400">#919191</Color>          <!-- Medium gray -->
<Color x:Key="Gray500">#6E6E6E</Color>          <!-- Medium-dark gray -->
<Color x:Key="Gray600">#404040</Color>          <!-- Dark gray -->
<Color x:Key="Gray900">#212121</Color>          <!-- Almost black -->
<Color x:Key="Gray950">#141414</Color>          <!-- Darkest gray -->

<!-- Base Colors -->
<Color x:Key="White">White</Color>
<Color x:Key="Black">Black</Color>
<Color x:Key="OffBlack">#1f1f1f</Color>         <!-- Dark mode background -->
<Color x:Key="DarkBackground">#1a1a1a</Color>   <!-- Darker backgrounds -->
```

### Semantic Color Usage

| Use Case | Light Mode | Dark Mode | Purpose |
|----------|-----------|-----------|---------|
| Primary Actions | `#512BD4` | `#ac99ea` | Buttons, CTAs, active states |
| Backgrounds | `White` | `#1f1f1f` | Page backgrounds |
| Text Primary | `#212121` | `White` | Body text, headings |
| Text Secondary | `#666666` | `#C8C8C8` | Subtitles, metadata |
| Borders | `#C8C8C8` | `#6E6E6E` | Dividers, card borders |
| Success | `#7CB342` | `#7CB342` | Success messages, sync status |
| Error | `#EF5350` | `#EF5350` | Error states, validation |
| Warning | `#FFA726` | `#FFA726` | Warnings, pending states |

### App-Specific Colors

```xml
<!-- AssetTag Specific -->
<Color x:Key="AppBlue">#005A9C</Color>          <!-- Headers, navigation -->
<Color x:Key="SyncGreen">#4CAF50</Color>        <!-- Synced status -->
<Color x:Key="SyncOrange">#FF9800</Color>       <!-- Pending sync -->
<Color x:Key="SyncRed">#F44336</Color>          <!-- Sync error -->
```

---

## Typography

### Font Families

```xml
<!-- Primary Fonts -->
<FontFamily>OpenSansRegular</FontFamily>        <!-- Body text -->
<FontFamily>OpenSansSemibold</FontFamily>       <!-- Headings, emphasis -->
```

### Type Scale

| Style | Size | Weight | Usage |
|-------|------|--------|-------|
| Headline | 32pt | Bold | Page titles (rare) |
| SubHeadline | 24pt | Bold | Section headers |
| Title | 20pt | Semibold | Card titles, nav bar |
| Body Large | 16pt | Semibold | Emphasized text |
| Body | 14pt | Regular | Default body text |
| Body Small | 13pt | Regular | Secondary text |
| Caption | 12pt | Regular | Metadata, timestamps |
| Tiny | 11pt | Regular | Version info, fine print |

### Typography Patterns

```xml
<!-- Page Title -->
<Label Text="Inventory"
       FontFamily="OpenSansSemibold"
       FontSize="20"
       TextColor="White"/>

<!-- Card Title -->
<Label Text="{Binding AssetName}"
       FontFamily="OpenSansSemibold"
       FontSize="16"
       TextColor="#333333"/>

<!-- Body Text -->
<Label Text="{Binding Description}"
       FontFamily="OpenSansRegular"
       FontSize="14"
       TextColor="#666666"/>

<!-- Caption/Metadata -->
<Label Text="{Binding LastModified}"
       FontFamily="OpenSansRegular"
       FontSize="12"
       TextColor="#999999"/>
```

---

## Spacing System

### Base Unit: 4pt

All spacing should use multiples of 4pt for consistency:

```
4pt   - Tight spacing (icon padding)
8pt   - Small spacing (between related items)
12pt  - Medium spacing (card padding)
16pt  - Standard spacing (page margins)
20pt  - Large spacing (section separation)
24pt  - Extra large spacing (major sections)
32pt  - Huge spacing (empty states)
```

### Common Spacing Patterns

```xml
<!-- Page Margins -->
<ContentPage Padding="16">

<!-- Card Padding -->
<Frame Padding="12,16">

<!-- Section Spacing -->
<VerticalStackLayout Spacing="12">

<!-- List Item Spacing -->
<CollectionView ItemsSource="{Binding Items}">
    <CollectionView.ItemTemplate>
        <DataTemplate>
            <Frame Margin="0,8">  <!-- 8pt vertical spacing -->
```

---

## Component Library

### 1. Headers & Navigation

#### Top Header Bar Pattern

```xml
<Frame Grid.Row="0" 
       BackgroundColor="#005A9C" 
       CornerRadius="0" 
       Padding="0"
       HasShadow="True">
    <Grid ColumnDefinitions="56,*,56" Padding="0">
        <!-- Back Button -->
        <Button Grid.Column="0"
                ImageSource="{mi:Material Icon=ArrowBack, IconColor=White}"
                BackgroundColor="Transparent"/>
        
        <!-- Title -->
        <Label Grid.Column="1"
               Text="Page Title"
               FontFamily="OpenSansSemibold"
               FontSize="20"
               TextColor="White"
               HorizontalOptions="Center"
               VerticalOptions="Center"/>
        
        <!-- Action Button -->
        <Button Grid.Column="2"
                ImageSource="{mi:Material Icon=Sort, IconColor=White}"
                BackgroundColor="Transparent"/>
    </Grid>
</Frame>
```

**Rules**:
- Height: 56pt (standard touch target)
- Background: `#005A9C` (app blue)
- Text: White, 20pt, Semibold
- Icons: 24pt, White
- Shadow: Subtle drop shadow for depth

### 2. Search Bars

#### Standard Search Pattern

```xml
<Frame BackgroundColor="White"
       CornerRadius="24"
       Padding="12,8"
       HasShadow="True">
    <Grid ColumnDefinitions="24,*,24,24">
        <!-- Search Icon -->
        <Image Grid.Column="0"
               WidthRequest="20"
               HeightRequest="20">
            <Image.Source>
                <FontImageSource FontFamily="MaterialIcons-Regular"
                               Glyph="{mi:Material Icon=Search}"
                               Color="#666666"
                               Size="20"/>
            </Image.Source>
        </Image>
        
        <!-- Search Entry -->
        <Entry Grid.Column="1"
               Placeholder="Search..."
               PlaceholderColor="#999999"
               Text="{Binding SearchText}"
               TextColor="#333333"
               FontSize="14"
               Margin="8,0"/>
        
        <!-- Scan Button -->
        <Button Grid.Column="2"
                ImageSource="{mi:Material Icon=QrCodeScanner}"
                BackgroundColor="Transparent"/>
        
        <!-- Clear Button -->
        <Button Grid.Column="3"
                ImageSource="{mi:Material Icon=Close}"
                BackgroundColor="Transparent"
                IsVisible="{Binding HasSearchText}"/>
    </Grid>
</Frame>
```

**Rules**:
- Corner radius: 24pt (pill shape)
- Background: White with shadow
- Placeholder: Gray600 (#666666)
- Text: Gray900 (#333333)
- Icons: 20pt, Gray600

### 3. Filter Chips

#### Chip Pattern

```xml
<Frame Padding="12,8"
       CornerRadius="16"
       HasShadow="False"
       BackgroundColor="{Binding IsActive, Converter={StaticResource BoolToColorConverter}, 
                         ConverterParameter='#005A9C|#E0E0E0'}">
    <Frame.GestureRecognizers>
        <TapGestureRecognizer Command="{Binding ToggleCommand}"/>
    </Frame.GestureRecognizers>
    <Label Text="Filter Name"
           FontFamily="OpenSansSemibold"
           FontSize="14"
           TextColor="{Binding IsActive, Converter={StaticResource BoolToColorConverter}, 
                       ConverterParameter='White|#666666'}"/>
</Frame>
```

**States**:
- **Active**: Background `#005A9C`, Text White
- **Inactive**: Background `#E0E0E0`, Text `#666666`
- Corner radius: 16pt
- Padding: 12pt horizontal, 8pt vertical

### 4. Cards

#### Asset Card Pattern

```xml
<Frame Margin="0,8"
       Padding="0"
       CornerRadius="12"
       BackgroundColor="White"
       HasShadow="True">
    <Grid ColumnDefinitions="4,56,*,24" 
          HeightRequest="80">
        
        <!-- Status Indicator (Left Edge) -->
        <BoxView Grid.Column="0"
                 BackgroundColor="{Binding StatusColor}"
                 CornerRadius="12,0,0,12"/>
        
        <!-- Icon -->
        <Frame Grid.Column="1"
               WidthRequest="40"
               HeightRequest="40"
               CornerRadius="20"
               BackgroundColor="#E3F2FD"
               Padding="0"
               HasShadow="False">
            <Image WidthRequest="24"
                   HeightRequest="24"
                   Source="{Binding Icon}"/>
        </Frame>
        
        <!-- Content -->
        <VerticalStackLayout Grid.Column="2"
                             Padding="12,16"
                             Spacing="4">
            <Label Text="{Binding Title}"
                   FontFamily="OpenSansSemibold"
                   FontSize="16"
                   TextColor="#333333"/>
            <Label Text="{Binding Subtitle}"
                   FontSize="14"
                   TextColor="#666666"/>
        </VerticalStackLayout>
        
        <!-- Chevron -->
        <Image Grid.Column="3"
               Source="{mi:Material Icon=ChevronRight}"
               WidthRequest="24"
               HeightRequest="24"/>
    </Grid>
</Frame>
```

**Rules**:
- Corner radius: 12pt
- Height: 80pt (standard card height)
- Shadow: Subtle elevation
- Status indicator: 4pt wide colored strip
- Icon: 40pt circle with light background
- Chevron: 24pt, Gray400

### 5. Buttons

#### Primary Button

```xml
<Button Text="Sign In"
        Command="{Binding LoginCommand}"
        CornerRadius="20"
        FontAttributes="Bold"
        FontSize="16"
        HeightRequest="48"
        BackgroundColor="#005A9C"
        TextColor="White"/>
```

#### Secondary Button (Outlined)

```xml
<Button Text="Cancel"
        Command="{Binding CancelCommand}"
        CornerRadius="20"
        FontSize="14"
        HeightRequest="44"
        BackgroundColor="Transparent"
        Stroke="#005A9C"
        StrokeThickness="2"
        TextColor="#005A9C"/>
```

#### Floating Action Button (FAB)

```xml
<Button ImageSource="{mi:Material Icon=Add, IconColor=White}"
        BackgroundColor="#005A9C"
        CornerRadius="28"
        WidthRequest="56"
        HeightRequest="56"
        HorizontalOptions="End"
        VerticalOptions="End"
        Margin="16"
        Command="{Binding AddCommand}"
        Shadow="{StaticResource FABShadow}"/>
```

**Button Sizes**:
- Large: 48pt height (primary actions)
- Medium: 44pt height (standard actions)
- Small: 36pt height (compact actions)
- FAB: 56pt × 56pt (floating action)

**Corner Radius**:
- Standard buttons: 20pt (pill shape)
- FAB: 28pt (circular)

### 6. Input Fields (Syncfusion)

#### Text Input Pattern

```xml
<inputLayout:SfTextInputLayout
    ContainerType="Outlined"
    Hint="Email Address"
    OutlineCornerRadius="20">
    <Entry FontSize="12"
           Keyboard="Email"
           Text="{Binding Email}"
           TextColor="{StaticResource Gray900}"/>
</inputLayout:SfTextInputLayout>
```

#### Password Input Pattern

```xml
<inputLayout:SfTextInputLayout
    ContainerType="Outlined"
    EnablePasswordVisibilityToggle="True"
    Hint="Password"
    OutlineCornerRadius="20">
    <Entry FontSize="12"
           IsPassword="True"
           Text="{Binding Password}"
           TextColor="{StaticResource Gray900}"/>
</inputLayout:SfTextInputLayout>
```

**Rules**:
- Container: Outlined style
- Corner radius: 20pt
- Font size: 12pt (compact)
- Hint color: Gray500
- Text color: Gray900

### 7. Loading States

#### Skeleton Loader

```xml
<controls:SkeletonLoader IsVisible="{Binding IsBusy}"
                          SkeletonType="List"
                          ItemCount="5"/>
```

**Skeleton Types**:
- `List` - List item placeholders
- `Card` - Card placeholders
- `Text` - Text line placeholders

#### Activity Indicator Overlay

```xml
<Grid BackgroundColor="#80000000"
      IsVisible="{Binding IsBusy}">
    <VerticalStackLayout HorizontalOptions="Center"
                         VerticalOptions="Center"
                         Spacing="16">
        <ActivityIndicator HeightRequest="48"
                          WidthRequest="48"
                          IsRunning="True"
                          Color="White"/>
        <Label Text="Loading..."
               FontAttributes="Bold"
               FontSize="16"
               TextColor="White"/>
    </VerticalStackLayout>
</Grid>
```

### 8. Empty States

```xml
<VerticalStackLayout IsVisible="{Binding ShowEmptyState}"
                     HorizontalOptions="Center"
                     VerticalOptions="Center"
                     Spacing="16"
                     Padding="32">
    <!-- Icon -->
    <Image WidthRequest="64" HeightRequest="64">
        <Image.Source>
            <FontImageSource FontFamily="MaterialIcons-Regular"
                           Glyph="{mi:Material Icon=SearchOff}"
                           Color="#CCCCCC"
                           Size="64"/>
        </Image.Source>
    </Image>
    
    <!-- Title -->
    <Label Text="No Assets Found"
           FontFamily="OpenSansSemibold"
           FontSize="18"
           TextColor="#666666"
           HorizontalOptions="Center"/>
    
    <!-- Description -->
    <Label Text="Try adjusting your filters"
           FontSize="14"
           TextColor="#999999"
           HorizontalOptions="Center"
           HorizontalTextAlignment="Center"/>
</VerticalStackLayout>
```

**Rules**:
- Icon: 64pt, Gray300 (#CCCCCC)
- Title: 18pt, Semibold, Gray600
- Description: 14pt, Regular, Gray500
- Spacing: 16pt between elements
- Padding: 32pt around container

### 9. Error Messages

```xml
<Border Padding="10,8"
        BackgroundColor="#FFEBEE"
        IsVisible="{Binding HasError}"
        Stroke="#EF5350"
        StrokeThickness="1">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="8"/>
    </Border.StrokeShape>
    <Label FontSize="12"
           LineBreakMode="WordWrap"
           Text="{Binding ErrorMessage}"
           TextColor="#C62828"/>
</Border>
```

**Error States**:
- Background: `#FFEBEE` (light red)
- Border: `#EF5350` (red)
- Text: `#C62828` (dark red)
- Corner radius: 8pt
- Padding: 10pt horizontal, 8pt vertical

---

## Icon System

### Material Icons

**Library**: `AathifMahir.Maui.MauiIcons.Material`

**Usage Pattern**:
```xml
xmlns:mi="http://www.aathifmahir.com/dotnet/2022/maui/icons"

<!-- In Image -->
<Image Source="{mi:Material Icon=Search, IconColor=#666666}"/>

<!-- In Button -->
<Button ImageSource="{mi:Material Icon=Add, IconColor=White}"/>

<!-- In FontImageSource -->
<Image.Source>
    <FontImageSource FontFamily="MaterialIcons-Regular"
                   Glyph="{mi:Material Icon=Search}"
                   Color="#666666"
                   Size="24"/>
</Image.Source>
```

### Common Icons

| Icon | Glyph | Usage |
|------|-------|-------|
| Search | `Search` | Search functionality |
| Add | `Add` | Create new items |
| Close | `Close` | Close/dismiss actions |
| ArrowBack | `ArrowBack` | Navigation back |
| ChevronRight | `ChevronRight` | List item navigation |
| QrCodeScanner | `QrCodeScanner` | Barcode scanning |
| FilterList | `FilterList` | Filter options |
| Sort | `Sort` | Sorting options |
| Inventory | `Inventory` | Asset/inventory |
| LocationOn | `LocationOn` | Location/place |
| SyncProblem | `SyncProblem` | Sync issues |
| CheckCircle | `CheckCircle` | Success states |
| Error | `Error` | Error states |

### Icon Sizes

- **Tiny**: 12pt (chip icons)
- **Small**: 16pt (inline icons)
- **Medium**: 20pt (search bar icons)
- **Standard**: 24pt (buttons, navigation)
- **Large**: 40pt (card icons)
- **Huge**: 64pt (empty states)

---

## Shadows & Elevation

### Shadow Definitions

```xml
<!-- FAB Shadow -->
<Shadow x:Key="FABShadow"
        Brush="Black"
        Offset="0,4"
        Radius="8"
        Opacity="0.3"/>

<!-- Card Shadow -->
<Frame HasShadow="True">  <!-- Built-in shadow -->
```

### Elevation Levels

| Level | Usage | Shadow |
|-------|-------|--------|
| 0 | Flat surfaces | None |
| 1 | Cards, chips | `HasShadow="True"` |
| 2 | Headers, search bars | `HasShadow="True"` |
| 3 | FAB, modals | Custom shadow (0,4,8,0.3) |

---

## Animation & Transitions

### Page Transitions

```xml
<!-- Disable animations for instant navigation -->
<ShellContent Shell.PresentationMode="NotAnimated"/>
```

### Loading Animations

```xml
<!-- Activity Indicator -->
<ActivityIndicator IsRunning="True"
                  Color="#005A9C"
                  WidthRequest="48"
                  HeightRequest="48"/>
```

### Skeleton Animations

Skeleton loaders automatically animate with a shimmer effect.

---

## Layout Patterns

### 1. Standard Page Layout

```xml
<Grid RowDefinitions="56,Auto,*">
    <!-- Header -->
    <Frame Grid.Row="0" BackgroundColor="#005A9C">
        <!-- Header content -->
    </Frame>
    
    <!-- Filters/Search -->
    <VerticalStackLayout Grid.Row="1" Padding="16,12">
        <!-- Search and filters -->
    </VerticalStackLayout>
    
    <!-- Content -->
    <Grid Grid.Row="2">
        <!-- Main content -->
    </Grid>
</Grid>
```

### 2. Login/Auth Layout

```xml
<Grid Background="{StaticResource LoginGradient}">
    <!-- Decorative circles -->
    <AbsoluteLayout>
        <!-- Background decoration -->
    </AbsoluteLayout>
    
    <!-- Content -->
    <VerticalStackLayout Padding="30,20"
                         Spacing="15"
                         VerticalOptions="Center">
        <!-- Login form -->
    </VerticalStackLayout>
    
    <!-- Busy overlay -->
    <Grid IsVisible="{Binding IsBusy}">
        <!-- Loading indicator -->
    </Grid>
</Grid>
```

### 3. List Layout

```xml
<RefreshView IsRefreshing="{Binding IsBusy}"
             Command="{Binding RefreshCommand}">
    <CollectionView ItemsSource="{Binding Items}"
                    SelectionMode="None"
                    Margin="16,0">
        <CollectionView.ItemTemplate>
            <DataTemplate>
                <!-- Card template -->
            </DataTemplate>
        </CollectionView.ItemTemplate>
    </CollectionView>
</RefreshView>
```

---

## Responsive Design

### Breakpoints

- **Phone**: < 600pt width
- **Tablet**: 600pt - 900pt width
- **Desktop**: > 900pt width

### Adaptive Layouts

```xml
<!-- Use OnIdiom for device-specific values -->
<Label FontSize="{OnIdiom Phone=14, Tablet=16, Desktop=18}"/>

<!-- Use OnPlatform for platform-specific values -->
<Label Margin="{OnPlatform iOS='0,20,0,0', Android='0,0,0,0'}"/>
```

---

## Accessibility Guidelines

### Touch Targets

**Minimum size**: 44pt × 44pt

```xml
<Button MinimumHeightRequest="44"
        MinimumWidthRequest="44"/>
```

### Color Contrast

- **Normal text**: 4.5:1 contrast ratio
- **Large text**: 3:1 contrast ratio
- **UI components**: 3:1 contrast ratio

### Text Scaling

Support dynamic type by using relative font sizes:

```xml
<Label FontSize="14"/>  <!-- Will scale with system settings -->
```

---

## Dark Mode Support

### Theme-Aware Colors

```xml
<Label TextColor="{AppThemeBinding Light={StaticResource Gray900}, 
                                   Dark={StaticResource White}}"/>

<Frame BackgroundColor="{AppThemeBinding Light={StaticResource White}, 
                                         Dark={StaticResource OffBlack}}"/>
```

### Dark Mode Color Mappings

| Element | Light Mode | Dark Mode |
|---------|-----------|-----------|
| Background | White | `#1f1f1f` |
| Text Primary | `#212121` | White |
| Text Secondary | `#666666` | `#C8C8C8` |
| Primary Button | `#512BD4` | `#ac99ea` |
| Card Background | White | `#2a2a2a` |
| Borders | `#C8C8C8` | `#6E6E6E` |

---

## Common UI Patterns

### 1. Pull-to-Refresh

```xml
<RefreshView IsRefreshing="{Binding IsBusy}"
             Command="{Binding RefreshCommand}"
             RefreshColor="#005A9C">
    <CollectionView ItemsSource="{Binding Items}"/>
</RefreshView>
```

### 2. Infinite Scroll

```xml
<CollectionView ItemsSource="{Binding Items}"
                RemainingItemsThreshold="5"
                RemainingItemsThresholdReachedCommand="{Binding LoadMoreCommand}">
    <CollectionView.Footer>
        <Grid IsVisible="{Binding IsLoadingMore}">
            <ActivityIndicator IsRunning="True"/>
        </Grid>
    </CollectionView.Footer>
</CollectionView>
```

### 3. Swipe Actions

```xml
<CollectionView ItemsSource="{Binding Items}">
    <CollectionView.ItemTemplate>
        <DataTemplate>
            <SwipeView>
                <SwipeView.RightItems>
                    <SwipeItems>
                        <SwipeItem Text="Delete"
                                  BackgroundColor="Red"
                                  Command="{Binding DeleteCommand}"/>
                    </SwipeItems>
                </SwipeView.RightItems>
                <!-- Item content -->
            </SwipeView>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

### 4. Modal Sheets

```xml
<!-- Navigate to modal page -->
await Shell.Current.GoToAsync(nameof(AddAssetPage), animate: true);

<!-- Modal page with close button -->
<ContentPage Shell.PresentationMode="ModalAnimated">
    <Grid RowDefinitions="56,*">
        <Frame Grid.Row="0">
            <Button Text="Close" Command="{Binding CloseCommand}"/>
        </Frame>
        <!-- Content -->
    </Grid>
</ContentPage>
```

---

## Performance Best Practices

### 1. Image Optimization

```xml
<!-- Use appropriate image sizes -->
<Image Source="icon.png"
       WidthRequest="40"
       HeightRequest="40"
       Aspect="AspectFit"/>

<!-- Use FontImageSource for icons (vector, scalable) -->
<Image.Source>
    <FontImageSource FontFamily="MaterialIcons-Regular"
                   Glyph="{mi:Material Icon=Search}"
                   Size="24"/>
</Image.Source>
```

### 2. List Virtualization

```xml
<!-- CollectionView automatically virtualizes -->
<CollectionView ItemsSource="{Binding Items}">
    <!-- Only visible items are rendered -->
</CollectionView>
```

### 3. Compiled Bindings

```xml
<!-- ALWAYS use x:DataType for compiled bindings -->
<ContentPage x:DataType="vm:InventoryViewModel">
    <Label Text="{Binding AssetName}"/>  <!-- 2-3x faster -->
</ContentPage>
```

---

## Component Checklist

When creating a new UI component, ensure:

- [ ] Uses compiled bindings (`x:DataType`)
- [ ] Follows color system (no hardcoded colors)
- [ ] Uses spacing system (multiples of 4pt)
- [ ] Minimum 44pt touch targets
- [ ] Supports dark mode (`AppThemeBinding`)
- [ ] Has loading state (skeleton or spinner)
- [ ] Has empty state (when applicable)
- [ ] Has error state (when applicable)
- [ ] Uses Material Icons
- [ ] Follows typography scale
- [ ] Has proper shadows/elevation
- [ ] Accessible (contrast, labels)

---

## Design Tokens Reference

### Quick Reference Table

| Token | Value | Usage |
|-------|-------|-------|
| `Primary` | `#512BD4` | Primary actions, brand |
| `AppBlue` | `#005A9C` | Headers, navigation |
| `Gray900` | `#212121` | Primary text |
| `Gray600` | `#666666` | Secondary text |
| `Gray300` | `#ACACAC` | Disabled text |
| `Gray100` | `#E1E1E1` | Borders, dividers |
| `White` | `White` | Backgrounds, light text |
| `BrandGreen` | `#7CB342` | Success states |
| `ErrorRed` | `#EF5350` | Error states |
| `WarningOrange` | `#FFA726` | Warning states |

### Spacing Tokens

| Token | Value | Usage |
|-------|-------|-------|
| `xs` | 4pt | Tight spacing |
| `sm` | 8pt | Small spacing |
| `md` | 12pt | Medium spacing |
| `lg` | 16pt | Standard spacing |
| `xl` | 20pt | Large spacing |
| `2xl` | 24pt | Extra large spacing |
| `3xl` | 32pt | Huge spacing |

---

## UI/UX Principles

### 1. Clarity Over Cleverness
- Use standard patterns users recognize
- Clear labels and icons
- Obvious interactive elements

### 2. Feedback is Essential
- Show loading states immediately
- Confirm actions with visual feedback
- Display errors clearly with solutions

### 3. Performance Matters
- Use skeleton loaders for perceived performance
- Instant navigation with cached pages
- Optimize images and assets

### 4. Consistency Builds Trust
- Same patterns across all screens
- Predictable navigation
- Unified visual language

### 5. Accessibility is Not Optional
- 44pt minimum touch targets
- High contrast text
- Support dynamic type
- Meaningful labels for screen readers

---

*This design system should be followed for all new UI components and screens. When in doubt, reference existing screens for patterns.*