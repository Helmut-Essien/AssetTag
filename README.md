# AssetTag - Methodist University Asset Management System

A comprehensive, enterprise-grade asset management platform for Methodist University Ghana, enabling efficient tracking, management, and reporting of organizational assets with AI-powered query capabilities and real-time analytics.

---

## System Overview

**AssetTag** is a full-stack asset management solution designed to streamline the complete lifecycle of organizational assets—from procurement through depreciation and disposal. The system provides:

- **Centralized Asset Registry**: Track all organizational assets with comprehensive details including purchase information, current value, condition, location, and depreciation metrics
- **Real-Time Dashboard Analytics**: Monitor asset distribution by status, condition, department, and location with interactive charts and performance metrics
- **Smart Asset Search & Filtering**: Quickly locate assets using multi-criteria filtering (status, condition, category, department, location) with full-text search
- **AI-Powered Reporting**: Natural language query interface powered by Groq AI that generates SQL queries and executes reports without manual SQL knowledge
- **Audit Trail & History Tracking**: Complete audit log of all asset movements, status changes, maintenance activities, and user actions
- **User & Role Management**: Role-based access control (Admin, Manager, User) with department-level organization
- **Financial Tracking**: Monitor asset values, depreciation, acquisition costs, disposal values, and warranty information
- **Email Notifications**: Automated communications for password resets, user invitations, and system alerts
- **JWT-Based Authentication**: Secure token-based authentication with refresh token support and detailed diagnostic tools
- **Multi-Tenant Support**: Department and location-based asset organization with user role restrictions

---

## Architecture

### Project Structure

The solution is organized into **four main projects** following a layered architecture pattern:

```
AssetTag.sln
├─ AssetTag/                                    # API Backend (.NET 9 Web API)
│  ├─ Controllers/                              # REST API endpoint handlers
│  │  ├─ AssetsController.cs                    # Asset CRUD operations
│  │  ├─ AuthController.cs                      # Authentication & token endpoints
│  │  ├─ CategoriesController.cs                # Asset category management
│  │  ├─ DashboardController.cs                 # Dashboard analytics data
│  │  ├─ DepartmentsController.cs               # Department management
│  │  ├─ DiagnosticsController.cs               # System diagnostics
│  │  ├─ AssetHistoriesController.cs            # Audit trail queries
│  │  ├─ LocationsController.cs                 # Location management
│  │  ├─ ReportsController.cs                   # Report generation & AI queries
│  │  ├─ RoleController.cs                      # Role management
│  │  ├─ TestController.cs                      # Test endpoints
│  │  └─ UsersController.cs                     # User management
│  │
│  ├─ Services/                                 # Business logic & external integrations
│  │  ├─ TokenService.cs                        # JWT token creation & validation
│  │  ├─ ITokenService.cs                       # Token service interface
│  │  ├─ EmailService.cs                        # SMTP-based email communications
│  │  ├─ IEmailService.cs                       # Email service interface
│  │  └─ AIQueryService.cs                      # Groq AI integration for SQL generation
│  │
│  ├─ Data/                                     # Database configuration & migrations
│  │  ├─ ApplicationDbContext.cs                # Entity Framework Core context
│  │  ├─ DesignTimeDbContextFactory.cs          # Design-time context factory
│  │  ├─ SeedData.cs                            # Initial database seeding
│  │  └─ Migrations/                            # EF Core database migrations
│  │     ├─ 20251014093448_initialDb.cs
│  │     ├─ 20251031105441_AddAssetFinancialFields.cs
│  │     └─ 20251111191210_AddedInvitationTable.cs
│  │
│  ├─ Filters/                                  # Custom action filters
│  │  └─ ActiveUserAttribute.cs                 # Validates user is active
│  │
│  ├─ Program.cs                                # DI container & middleware configuration
│  ├─ appsettings.json                          # Default configuration
│  ├─ appsettings.Development.json              # Development overrides
│  └─ AssetTag.csproj                           # Project file
│
├─ Portal/                                      # Web Frontend (.NET 9 Razor Pages)
│  ├─ Pages/                                    # Razor Pages UI components
│  │  ├─ Index.cshtml                           # Dashboard home page
│  │  ├─ Index.cshtml.cs                        # Dashboard code-behind
│  │  ├─ LoginRedirect.cshtml.cs                # Post-login redirect
│  │  ├─ Unauthorized.cshtml                    # Unauthorized access page
│  │  ├─ Unauthorized.cshtml.cs                 # Unauthorized logic
│  │  ├─ Privacy.cshtml.cs                      # Privacy page
│  │  ├─ Error.cshtml.cs                        # Error page
│  │  ├─ Forbidden.cshtml.cs                    # Forbidden page
│  │  │
│  │  ├─ Account/                               # Authentication pages
│  │  │  ├─ Login.cshtml                        # Login form
│  │  │  ├─ Login.cshtml.cs                     # Login logic
│  │  │  ├─ Logout.cshtml.cs                    # Logout handler
│  │  │  ├─ Register.cshtml                     # Registration form
│  │  │  ├─ Register.cshtml.cs                  # Registration logic
│  │  │  ├─ ForgotPassword.cshtml.cs            # Forgot password handler
│  │  │  └─ ResetPassword.cshtml.cs             # Reset password handler
│  │  │
│  │  ├─ Assets/                                # Asset management pages
│  │  │  ├─ Index.cshtml                        # Asset listing & search
│  │  │  ├─ Index.cshtml.cs                     # Asset list logic
│  │  │  ├─ Details.cshtml                      # Asset detail view
│  │  │  ├─ Details.cshtml.cs                   # Asset details logic
│  │  │  └─ AssetHistories.cshtml.cs            # Asset history page
│  │  │
│  │  ├─ Categories/                            # Category management pages
│  │  │  ├─ Index.cshtml                        # Category list
│  │  │  └─ Index.cshtml.cs                     # Category logic
│  │  │
│  │  ├─ Departments/                           # Department management pages
│  │  │  ├─ Index.cshtml                        # Department list
│  │  │  └─ Index.cshtml.cs                     # Department logic
│  │  │
│  │  ├─ Diagnostics/                           # System diagnostic pages
│  │  │  ├─ TokenDiagnostics.cshtml.cs          # JWT token inspector
│  │  │  └─ TimeCheck.cshtml.cs                 # Server time synchronization
│  │  │
│  │  ├─ Locations/                             # Location management pages
│  │  │  ├─ Index.cshtml                        # Location list
│  │  │  └─ Index.cshtml.cs                     # Location logic
│  │  │
│  │  ├─ Reports/                               # Report generation pages
│  │  │  ├─ Index.cshtml                        # Reports interface
│  │  │  └─ Index.cshtml.cs                     # Reports logic
│  │  │
│  │  ├─ Users/                                 # User management pages
│  │  │  ├─ Index.cshtml                        # User list
│  │  │  └─ Index.cshtml.cs                     # User logic
│  │  │
│  │  └─ Shared/                                # Shared layout & components
│  │     ├─ _Layout.cshtml                      # Main layout template
│  │     ├─ _AuthLayout.cshtml                  # Authentication pages layout
│  │     ├─ _ValidationScriptsPartial.cshtml    # Validation scripts
│  │     ├─ _ViewStart.cshtml                   # View engine initialization
│  │     └─ _ViewImports.cshtml                 # View imports
│  │
│  ├─ Services/                                 # Frontend business logic
│  │  ├─ ApiAuthService.cs                      # API authentication client
│  │  ├─ IApiAuthService.cs                     # Auth service interface
│  │  ├─ UserRoleService.cs                     # Role-based access control
│  │  ├─ IUserRoleService.cs                    # Role service interface
│  │  ├─ ReportsService.cs                      # Report data retrieval
│  │  └─ IReportsService.cs                     # Reports service interface
│  │
│  ├─ Handlers/                                 # HTTP message handlers
│  │  ├─ TokenRefreshHandler.cs                 # Automatic JWT token refresh
│  │  └─ UnauthorizedRedirectHandler.cs         # 401 to login redirect
│  │
│  ├─ wwwroot/                                  # Static assets (client-side)
│  │  ├─ css/                                   # Stylesheets
│  │  │  ├─ authLayout.css                      # Auth pages styles
│  │  │  └─ dashboard.css                       # Dashboard styles
│  │  └─ js/                                    # JavaScript files
│  │     └─ dashboard.js                        # Dashboard initialization
│  │
│  ├─ Program.cs                                # DI container & middleware setup
│  ├─ appsettings.json                          # Default configuration
│  ├─ appsettings.Development.json              # Development overrides
│  └─ Portal.csproj                             # Project file
│
├─ Shared/                                      # Class Library - Shared DTOs & Models
│  ├─ DTOs/                                     # Data Transfer Objects
│  │  ├─ AssetDto.cs                            # Asset DTO
│  │  ├─ AssetHistoryDto.cs                     # Audit history DTO
│  │  ├─ Auth.cs                                # Authentication DTOs
│  │  ├─ RefreshTokenDto.cs                     # Refresh token DTO
│  │  ├─ UserDto.cs                             # User DTO
│  │  ├─ CategoryDto.cs                         # Category DTO
│  │  ├─ DepartmentDto.cs                       # Department DTO
│  │  ├─ LocationDto.cs                         # Location DTO
│  │  ├─ DashboardDTO.cs                        # Dashboard data DTO
│  │  ├─ InvitationDto.cs                       # Invitation DTO
│  │  ├─ AiDTO.cs                               # AI query DTO
│  │  └─ GroqDTO.cs                             # Groq API DTO
│  │
│  ├─ Models/                                   # Entity models (shared definitions)
│  │  ├─ ApplicationUser.cs                     # User model
│  │  ├─ Asset.cs                               # Asset model
│  │  ├─ AssetHistory.cs                        # Audit trail model
│  │  ├─ Category.cs                            # Category model
│  │  ├─ Department.cs                          # Department model
│  │  ├─ Location.cs                            # Location model
│  │  ├─ RefreshTokens.cs                       # Token model
│  │  └─ Invitation.cs                          # Invitation model
│  │
│  └─ Shared.csproj                             # Project file
│
├─ MobileApp/                                   # Mobile Frontend (.NET 9 MAUI)
│  ├─ Platforms/                                # Platform-specific code
│  │  ├─ Android/                               # Android platform implementations
│  │  │  ├─ MainActivity.cs                     # Android main activity
│  │  │  └─ MainApplication.cs                  # Android application class
│  │  ├─ iOS/                                   # iOS platform implementations
│  │  ├─ MacCatalyst/                           # macOS Catalyst implementations
│  │  └─ Windows/                               # Windows platform implementations
│  │
│  ├─ Data/                                     # Local data access layer
│  │  └─ LocalDbContext.cs                      # SQLite EF Core context for offline storage
│  │
│  ├─ Resources/                                # Application resources
│  │  ├─ Raw/                                   # Raw resource files
│  │  │  └─ AboutAssets.txt                     # Asset documentation
│  │  └─ Styles/                                # App-wide styles
│  │     ├─ Colors.xaml                         # Color definitions
│  │     └─ Styles.xaml                         # Style definitions
│  │
│  ├─ App.xaml                                  # Main app resource dictionary
│  ├─ App.xaml.cs                               # App code-behind
│  ├─ AppShell.xaml                             # Navigation shell structure
│  ├─ AppShell.xaml.cs                          # Navigation shell code-behind
│  ├─ MainPage.xaml                             # Main page UI
│  ├─ MainPage.xaml.cs                          # Main page code-behind
│  ├─ GlobalXmlns.cs                            # Global XML namespace definitions
│  ├─ MauiProgram.cs                            # MAUI DI & configuration
│  └─ MobileApp.csproj                          # Project file
│
├─ README.md                                    # Project documentation
├─ LICENSE                                      # MIT License
├─ .gitignore                                   # Git ignore rules
├─ DIAGRAM_FIXES_SUMMARY.md                     # Architecture fixes log
├─ GITHUB_RENDERING_FIXES.md                    # GitHub rendering fixes log
└─ AssetTag.sln                                 # Solution file
```

### Key Directory Structure Overview

**API Backend (AssetTag/)**
- **Controllers**: REST API endpoints handling HTTP requests and responses
- **Services**: Business logic, AI integration, email, token management
- **Data**: Database context, migrations, seed data, and design-time factories
- **Filters**: Custom authorization and validation filters

**Web Frontend (Portal/)**
- **Pages**: Razor Pages organized by feature (Account, Assets, Reports, Users, etc.)
- **Services**: Frontend logic for authentication, reports, role management
- **Handlers**: HTTP message handlers for token refresh and error handling
- **wwwroot**: Static CSS, JavaScript, images, and third-party libraries

**Mobile Frontend (MobileApp/)**
- **Platforms**: Platform-specific code for Android, iOS, macOS Catalyst, and Windows
- **Data**: SQLite local database for offline asset data storage
- **Resources**: App resources including styles and raw files
  - Styles: Color and style definitions in XAML
  - Raw: Static asset files
- **App Shell & Pages**: XAML-based UI components for navigation and screens

**Shared Library (Shared/)**
- **DTOs**: Data Transfer Objects for inter-project communication
- **Models**: Common entity models used across all projects

### Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Frontend (Web)** | Razor Pages, Bootstrap 5, Chart.js | Server-rendered web UI with responsive design |
| **Frontend (Mobile)** | .NET MAUI, XAML | Cross-platform mobile app (Android, iOS, Windows, macOS) |
| **Backend** | ASP.NET Core 9, C# 13 | RESTful API development |
| **Database** | SQL Server, Entity Framework Core 9 | Data persistence with ORM |
| **Authentication** | JWT (JSON Web Tokens), ASP.NET Identity | Stateless token-based authentication |
| **AI Integration** | Groq API, HTTP Client | Natural language to SQL query generation |
| **Email** | SMTP (Gmail, Outlook, etc.) | Email communications |
| **Logging** | ILogger (built-in) | Structured logging |

### Architectural Layers

#### **1. API Backend (AssetTag Project)**

**Purpose**: Provides RESTful API endpoints for all business operations.

**Key Components**:
- **Controllers** (`/Controllers`): Handle HTTP requests and route them to appropriate services
  - `AssetsController` - Asset CRUD operations
  - `CategoriesController` - Category management
  - `DepartmentsController` - Department management
  - `LocationsController` - Location management
  - `UsersController` - User management
  - `AuthController` - Authentication & token generation
  - `ReportsController` - Report generation
  - `DashboardController` - Dashboard analytics data
  - `AssetHistoriesController` - Audit trail queries
  - `RoleController` - Role management
  - `DiagnosticsController` - System diagnostics

- **Services** (`/Services`): Business logic and external integrations
  - `TokenService` - JWT token creation and validation
  - `AIQueryService` - Groq AI integration for SQL generation and execution
  - `EmailService` - SMTP-based email sending
  - `ITokenService`, `IEmailService` - Service interfaces

- **Models** (`/Models`): Entity classes representing database tables
  - `Asset` - Asset entity with financial and depreciation fields
  - `Category`, `Department`, `Location` - Master data entities
  - `ApplicationUser` - User entity (extends ASP.NET Identity)
  - `AssetHistory` - Audit trail entries
  - `RefreshTokens` - Token management

- **Data** (`/Data`): Database configuration and initialization
  - `ApplicationDbContext` - EF Core DbContext
  - `SeedData` - Initial data seeding (default admin user)
  - `DesignTimeDbContextFactory` - Design-time context for migrations

- **Filters** (`/Filters`): Custom action filters
  - `ActiveUserAttribute` - Validates user is active before allowing API access

#### **2. Web Frontend (Portal Project)**

**Purpose**: Provides a user-friendly interface for asset management, leveraging server-side rendering.

**Key Components**:
- **Pages** (`/Pages`): Razor Pages for different features
  - `Index.cshtml` - Dashboard with real-time analytics and charts
  - `Assets/Index.cshtml` - Asset listing with search and filters
  - `Account/Login.cshtml` - Authentication page
  - `Reports/Index.cshtml` - Report interface with AI assistant
  - `Diagnostics/` - Token and system diagnostics
  - `Categories/`, `Departments/`, `Locations/`, `Users/` - Master data management

- **Services** (`/Services`): Frontend business logic
  - `ApiAuthService` - Authentication with API backend
  - `ReportsService` - Report data retrieval
  - `UserRoleService` - Role-based access control
  - `TokenRefreshHandler` - Automatic JWT token refresh

- **Handlers** (`/Handlers`): HTTP message handlers
  - `UnauthorizedRedirectHandler` - Redirects 401 responses to login page

- **Static Assets** (`/wwwroot`)
  - CSS: Bootstrap, custom dashboard styles
  - JavaScript: Chart.js for analytics, dashboard initialization
  - Libraries: jQuery, Bootstrap JS, validation libraries
  - Images: University logos and branding

#### **3. Shared Library (Shared Project)**

**Purpose**: Provides DTOs and utilities used by both frontend and backend.

**Key Components**:
- **DTOs** (`/DTOs`): Data Transfer Objects
  - `AiDTO` - AI query request/response models
  - `GroqDTO` - Groq API request/response models
  - `AssetDto`, `CategoryDto`, `DepartmentDto`, `LocationDto` - Entity DTOs
  - `UserDto`, `AssetHistoryDto` - User and audit trail DTOs
  - `Auth.cs` - Authentication/token DTOs
  - `RefreshTokenDto` - Token refresh models
  - `InvitationDto` - User invitation models
  - `DashboardDTO` - Dashboard data models

#### **4. Mobile Frontend (MobileApp Project)**

**Purpose**: Provides a cross-platform mobile application for iOS, Android, Windows, and macOS with offline capabilities.

**Key Components**:
- **Platforms** (`/Platforms`): Platform-specific implementations
  - `Android/` - Android-specific code and configurations
  - `iOS/` - iOS-specific code and configurations
  - `Windows/` - Windows-specific code and configurations
  - `MacCatalyst/` - macOS Catalyst-specific code and configurations

- **Data** (`/Data`): Local data persistence layer
  - `LocalDbContext` - SQLite Entity Framework Core context for offline storage
  - Enables offline-first functionality with syncing when connectivity resumes

- **Resources** (`/Resources`): Application resources
  - `Styles/`: XAML style and color definitions
    - `Colors.xaml` - Centralized color palette
    - `Styles.xaml` - Reusable component styles
  - `Raw/`: Static resource files and documentation

- **App Shell & Navigation**:
  - `App.xaml/cs` - Application-level configuration and resources
  - `AppShell.xaml/cs` - Navigation structure and routing
  - `MainPage.xaml/cs` - Landing page UI and logic
  - `GlobalXmlns.cs` - Global XML namespace definitions for XAML

- **Core Configuration**:
  - `MauiProgram.cs` - Dependency injection and MAUI platform configuration

### Data Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           USER BROWSER LAYER                                 │
│                          (HTTP Client)                                       │
└────────────────────────────┬────────────────────────────────────────────────┘
                             │
                    HTTP Requests & Responses
                    (Razor Pages + AJAX)
                             │
          ┌──────────────────┴──────────────────┐
          │                                     │
          ▼                                     ▼
   ┌─────────────────┐              ┌──────────────────────┐
   │  Portal Layer   │              │   API Backend        │
   │ (Frontend)      │◄──────JWT────►   (AssetTag)        │
   │                 │              │                      │
   │ • Razor Pages   │  REST Calls  │ • Controllers        │
   │ • Auth Service  │◄───JSON──────►  • Services          │
   │ • Token Mgmt    │              │ • Models             │
   │ • Caching       │              │ • Filters            │
   └────────┬────────┘              └──────┬───────────────┘
            │                               │
            │                    HTTP (REST API)
            │                               │
            │          ┌────────────────────┼────────────────────┐
            │          │                    │                    │
            │          ▼                    ▼                    ▼
            │     ┌─────────────┐    ┌──────────────┐    ┌─────────────┐
            │     │   SQL       │    │   Groq AI    │    │SMTP Email   │
            │     │   Server    │    │   (API)      │    │ (Server)    │
            │     │  Database   │    │              │    │             │
            │     │             │    │ • SQL Gen    │    │ • Pwd Reset │
            │     │ • Assets    │    │ • Validation │    │ • Invite    │
            │     │ • Users     │    │ • Execution  │    │ • Alerts    │
            │     │ • History   │    │              │    │             │
            │     │ • Tokens    │    └──────────────┘    └─────────────┘
            │     └─────────────┘
            │           ▲
            │           │
            │      EF Core ORM
            │    (Parameterized Queries)
            │
            └──► Cached Data Display
                 (Performance Optimization)


REQUEST/RESPONSE FLOW:

1. User Action (Browser)
   ↓
2. Portal sends HTTP request to API
   ↓
3. API Controller validates & authenticates (JWT)
   ↓
4. Service Layer processes business logic
   ↓
5. Database query execution
   ↓
6. Response marshaled to JSON
   ↓
7. Portal receives & renders response
   ↓
8. JavaScript updates UI (Dashboard charts, tables)
   
   
ASYNC FLOWS:

Email Notifications:
  Portal/API → EmailService → SMTP Server → User Inbox

AI Queries:
  Portal → Reports Controller → AIQueryService → Groq API → SQL Generated → DB Query → Results

Authentication:
  Login → TokenService creates JWT → Portal stores in cookie → Refresh Handler auto-updates
```

### Security Architecture

- **Authentication**: JWT-based with configurable expiration and refresh tokens
- **Authorization**: Role-based access control (RBAC) with custom attributes
- **Token Validation**: Enhanced logging and diagnostics for token-related issues
- **API Security**: Stateless design with secure token exchange
- **Active User Validation**: Filter ensures only active users can access APIs
- **SQL Injection Prevention**: Parameterized queries and whitelist-based table validation for AI-generated SQL

### Database Schema

**Core Tables**:
- `Assets` - Asset registry with financial fields
- `Categories` - Asset classification
- `Departments` - Organizational departments
- `Locations` - Physical locations
- `AssetHistories` - Complete audit trail
- `AspNetUsers` - User accounts (Identity tables)
- `AspNetRoles`, `AspNetUserRoles` - Role management
- `RefreshTokens` - Token lifecycle management

### Configuration Management

- **appsettings.json**: Default configuration (database, logging, features)
- **appsettings.Development.json**: Development-specific overrides
- **User Secrets**: Sensitive data (API keys, connection strings, credentials)
  - AssetTag User Secrets ID: `48e1817c-9eb7-4bc2-b9f6-6fa87e951008`
  - Portal User Secrets ID: `27aaf5cf-affe-4e6f-a34d-fbe0ff896331`

### Key Design Patterns

1. **Separation of Concerns**: Controllers ? Services ? Data Access
2. **Dependency Injection**: All services registered in DI container
3. **DTOs**: Decoupled entities from API contracts
4. **Interfaces**: Service abstractions for testability
5. **Middleware**: Custom middleware for logging, token mapping, and CORS
6. **Repository Pattern** (implicit): EF Core DbContext acts as repository

---

## Database Schema

### Tables Overview

The application uses SQL Server with Entity Framework Core. Below is a detailed breakdown of all tables, their columns, data types, and relationships.

#### **1. AspNetUsers** (Application User Accounts)

Extends ASP.NET Identity `IdentityUser` with custom fields.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | nvarchar(450) | No | Primary key (GUID) |
| UserName | nvarchar(256) | Yes | Unique username |
| Email | nvarchar(256) | Yes | Unique email address |
| NormalizedEmail | nvarchar(256) | Yes | Normalized email (indexed) |
| NormalizedUserName | nvarchar(256) | Yes | Normalized username (indexed, unique) |
| PasswordHash | nvarchar(max) | Yes | Hashed password |
| SecurityStamp | nvarchar(max) | Yes | Security stamp for token validation |
| ConcurrencyStamp | nvarchar(max) | Yes | Concurrency token |
| PhoneNumber | nvarchar(max) | Yes | Optional phone number |
| PhoneNumberConfirmed | bit | No | Flag: phone verified |
| EmailConfirmed | bit | No | Flag: email verified |
| TwoFactorEnabled | bit | No | Flag: 2FA enabled |
| LockoutEnabled | bit | No | Flag: lockout enabled |
| LockoutEnd | datetimeoffset | Yes | Lockout expiration time |
| AccessFailedCount | int | No | Failed login attempt count |
| FirstName | nvarchar(max) | No | User's first name (required) |
| Surname | nvarchar(max) | No | User's last name (required) |
| OtherNames | nvarchar(max) | Yes | Middle or additional names |
| DateOfBirth | datetime2 | Yes | User's birth date |
| Address | nvarchar(max) | Yes | Physical address |
| JobRole | nvarchar(max) | Yes | Job title or position |
| ProfileImage | nvarchar(max) | Yes | Profile image URL/path |
| IsActive | bit | No | Flag: account active (default: true) |
| DateCreated | datetime2 | No | Account creation timestamp |
| DepartmentId | nvarchar(450) | Yes | FK to Departments (nullable, SetNull) |

**Indexes**: EmailIndex (NormalizedEmail), UserNameIndex (NormalizedUserName, unique)

**Relationships**:
- FK: `DepartmentId` ? `Departments.DepartmentId` (SetNull on delete)
- OneToMany: User ? Assets (via AssignedToUserId)
- OneToMany: User ? RefreshTokens (Cascade on delete)

---

#### **2. Assets** (Asset Registry)

Core asset management table with comprehensive financial and operational fields.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| AssetId | nvarchar(450) | No | Primary key (ULID) |
| AssetTag | nvarchar(450) | No | Unique asset tag/identifier (indexed, unique) |
| OldAssetTag | nvarchar(max) | Yes | Previous asset tag (for history) |
| Name | nvarchar(max) | No | Asset name/description |
| Description | nvarchar(max) | Yes | Detailed description |
| Status | nvarchar(max) | No | Status (Available, In Use, Under Maintenance, Retired, Lost) |
| Condition | nvarchar(max) | No | Condition (New, Good, Fair, Poor, Broken) |
| CategoryId | nvarchar(450) | No | FK to Categories (Restrict on delete) |
| LocationId | nvarchar(450) | No | FK to Locations (Restrict on delete) |
| DepartmentId | nvarchar(450) | No | FK to Departments (Restrict on delete) |
| AssignedToUserId | nvarchar(450) | Yes | FK to AspNetUsers (SetNull on delete) |
| PurchaseDate | datetime2 | Yes | Asset purchase date |
| PurchasePrice | decimal(18,2) | Yes | Purchase price |
| CurrentValue | decimal(18,2) | Yes | Current monetary value |
| WarrantyExpiry | datetime2 | Yes | Warranty expiration date |
| SerialNumber | nvarchar(max) | Yes | Manufacturer serial number |
| VendorName | nvarchar(max) | Yes | Vendor/supplier name |
| InvoiceNumber | nvarchar(max) | Yes | Invoice/receipt number |
| Quantity | int | No | Quantity (default: 1) |
| CostPerUnit | decimal(18,2) | Yes | Cost per individual unit |
| TotalCost | decimal(18,2) | Yes | Total acquisition cost |
| DepreciationRate | decimal(18,2) | Yes | Annual depreciation rate (%) |
| AccumulatedDepreciation | decimal(18,2) | Yes | Total depreciation to date |
| NetBookValue | decimal(18,2) | Yes | Current net book value |
| UsefulLifeYears | int | Yes | Expected useful life in years |
| DisposalDate | datetime2 | Yes | Date asset was disposed |
| DisposalValue | decimal(18,2) | Yes | Proceeds from disposal |
| Remarks | nvarchar(max) | Yes | Additional notes |
| CreatedAt | datetime2 | No | Asset record creation timestamp |
| DateModified | datetime2 | No | Last modification timestamp |

**Indexes**: AssetTag (unique), CategoryId, LocationId, DepartmentId, AssignedToUserId

**Relationships**:
- FK: `CategoryId` ? `Categories.CategoryId` (Restrict)
- FK: `LocationId` ? `Locations.LocationId` (Restrict)
- FK: `DepartmentId` ? `Departments.DepartmentId` (Restrict)
- FK: `AssignedToUserId` ? `AspNetUsers.Id` (SetNull)
- OneToMany: Asset ? AssetHistories (Cascade)

---

#### **3. AssetHistories** (Audit Trail)

Complete audit log of all asset changes, movements, and actions.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| HistoryId | nvarchar(450) | No | Primary key (ULID) |
| AssetId | nvarchar(450) | No | FK to Assets (Cascade on delete) |
| UserId | nvarchar(450) | No | FK to AspNetUsers (Restrict on delete) |
| Action | nvarchar(max) | No | Action type (Create, Update, Maintain, Transfer, etc.) |
| Description | nvarchar(max) | No | Action description |
| Timestamp | datetime2 | No | When the action occurred |
| OldLocationId | nvarchar(450) | Yes | FK to old Location (Restrict on delete) |
| NewLocationId | nvarchar(450) | Yes | FK to new Location (SetNull on delete) |
| OldStatus | nvarchar(max) | Yes | Previous status |
| NewStatus | nvarchar(max) | Yes | New status |

**Indexes**: AssetId, UserId, OldLocationId, NewLocationId

**Relationships**:
- FK: `AssetId` ? `Assets.AssetId` (Cascade)
- FK: `UserId` ? `AspNetUsers.Id` (Restrict)
- FK: `OldLocationId` ? `Locations.LocationId` (Restrict)
- FK: `NewLocationId` ? `Locations.LocationId` (SetNull)

---

#### **4. Categories** (Asset Classification)

Asset categories for organization and filtering.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| CategoryId | nvarchar(450) | No | Primary key (ULID) |
| Name | nvarchar(max) | No | Category name |
| Description | nvarchar(max) | Yes | Category description |

**Relationships**:
- OneToMany: Category ? Assets (Restrict on delete)

---

#### **5. Departments** (Organizational Units)

Organizational departments for asset allocation and user assignment.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| DepartmentId | nvarchar(450) | No | Primary key (ULID) |
| Name | nvarchar(450) | No | Department name (indexed, unique) |
| Description | nvarchar(max) | Yes | Department description |

**Indexes**: Name (unique)

**Relationships**:
- OneToMany: Department ? Assets
- OneToMany: Department ? AspNetUsers

---

#### **6. Locations** (Physical Locations)

Physical locations where assets are stored or deployed.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| LocationId | nvarchar(450) | No | Primary key (ULID) |
| Name | nvarchar(450) | No | Location name |
| Description | nvarchar(max) | Yes | Location description |
| Campus | nvarchar(450) | No | Campus/site name |
| Building | nvarchar(max) | Yes | Building name/number |
| Room | nvarchar(max) | Yes | Room number/identifier |
| Latitude | float | Yes | Geographic latitude |
| Longitude | float | Yes | Geographic longitude |

**Indexes**: Name + Campus (composite, unique)

**Relationships**:
- OneToMany: Location ? Assets

---

#### **7. RefreshTokens** (Token Management)

Tracks issued refresh tokens for token rotation and revocation.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | int | No | Primary key (auto-increment) |
| Token | nvarchar(max) | No | Refresh token value |
| ApplicationUserId | nvarchar(450) | No | FK to AspNetUsers (Cascade on delete) |
| Created | datetime2 | No | Token creation timestamp |
| CreatedByIp | nvarchar(max) | No | IP address that created token |
| Expires | datetime2 | No | Token expiration date |
| Revoked | datetime2 | Yes | Revocation timestamp (null if active) |
| RevokedByIp | nvarchar(max) | Yes | IP address that revoked token |
| ReplacedByToken | nvarchar(max) | Yes | Token that replaced this one |

**Indexes**: ApplicationUserId

**Relationships**:
- FK: `ApplicationUserId` ? `AspNetUsers.Id` (Cascade)

---

#### **8. Invitations** (User Invitations)

Manages user invitations for onboarding new team members.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | nvarchar(450) | No | Primary key (ULID) |
| Email | nvarchar(450) | No | Invitee's email (indexed) |
| Token | nvarchar(450) | No | Invitation token (indexed, unique) |
| InvitedByUserId | nvarchar(450) | No | FK to AspNetUsers (Restrict on delete) |
| CreatedAt | datetime2 | No | Invitation creation timestamp |
| ExpiresAt | datetime2 | No | Token expiration timestamp |
| IsUsed | bit | No | Flag: invitation accepted (indexed) |
| UsedAt | datetime2 | Yes | When invitation was used |
| Role | nvarchar(max) | Yes | Role to assign on registration |

**Indexes**: Email, Token (unique), IsUsed, InvitedByUserId

**Relationships**:
- FK: `InvitedByUserId` ? `AspNetUsers.Id` (Restrict)

---

#### **9. AspNetRoles** (Identity Roles)

Role definitions for role-based access control.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | nvarchar(450) | No | Primary key |
| Name | nvarchar(256) | Yes | Role name |
| NormalizedName | nvarchar(256) | Yes | Normalized name (indexed, unique) |
| ConcurrencyStamp | nvarchar(max) | Yes | Concurrency token |

**Default Roles**: Admin, Manager, User

---

#### **10. AspNetUserRoles** (User Role Mapping)

Junction table mapping users to roles (many-to-many).

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| UserId | nvarchar(450) | No | FK to AspNetUsers (Cascade) |
| RoleId | nvarchar(450) | No | FK to AspNetRoles (Cascade) |

**Composite Key**: UserId + RoleId

---

### Entity Relationship Diagram (ERD)

```
AspNetUsers (User Accounts)
├── Id (PK)
├── Email (Unique)
├── FirstName, Surname
├── IsActive
├── DepartmentId (FK) ──────────────┐
│                                   ▼
│                            Departments
│                            ├── DepartmentId (PK)
│                            └── Name (Unique)
│
├── OneToMany Relationships:
│   ├── Assets (via AssignedToUserId)
│   ├── RefreshTokens (Cascade Delete)
│   └── AssetHistories (via UserId)

Categories (Asset Classification)          Departments (Organizational Units)
├── CategoryId (PK)                         ├── DepartmentId (PK)
├── Name (Unique)                           ├── Name (Unique, Indexed)
└── Description                             └── Description

        ▼                                              ▼
        │                                              │
        │                 Assets (Core Table)          │
        │              ┌──────────────────┬───────────┘
        │              │  AssetId (PK)    │
        ├─────────────►  Name             │
        │              │  Status          │
        │              │  Condition       │
        │              │  CurrentValue    │
        │              │  DepreciationRate
        │              │  CategoryId (FK) ◄─────────┐
        │              │  LocationId (FK) ◄─────────┼───────┐
        │              │  DepartmentId (FK)─────────┘       │
        │              │  AssignedToUserId (FK)             │
        │              │  CreatedAt                          │
        │              │  DateModified                       │
        │              └──────────────────┘                  │
        │                      ▼                             │
        │                      │                             │
        │            AssetHistories (Audit Trail)      Locations
        │            ├── HistoryId (PK)               ├── LocationId (PK)
        │            ├── AssetId (FK) ◄───┐           ├── Name
        │            ├── UserId (FK)       │           ├── Campus
        │            ├── Action            │           ├── Building
        │            ├── Description       │           ├── Room
        │            ├── Timestamp         │           ├── Latitude
        │            ├── OldStatus         │           └── Longitude
        │            ├── NewStatus         │                 ▲
        │            ├── OldLocationId (FK)├─────────┐      │
        │            └── NewLocationId (FK)──────────┼──────┘
        │                                   (SetNull)└────────┐
        │
        └─► (Cascade Delete)

Invitations (User Onboarding)
├── Id (PK)
├── Email (Indexed)
├── Token (Unique, Indexed)
├── InvitedByUserId (FK) ──────────►  AspNetUsers
├── CreatedAt
├── ExpiresAt
├── IsUsed (Indexed)
└── Role

RefreshTokens (Token Lifecycle)
├── Id (PK)
├── Token
├── ApplicationUserId (FK) ──────────►  AspNetUsers
├── Created
├── CreatedByIp
├── Expires
├── Revoked
├── RevokedByIp
└── ReplacedByToken

AspNetRoles & AspNetUserRoles (Identity)
├── Roles (3 default: Admin, Manager, User)
└── UserRoles (Many-to-Many Junction)
```

---

## API Endpoints

All API endpoints are accessible at base URL: `https://api.example.com/api/` (or configured API URL).

**Authentication**: Most endpoints require JWT Bearer token in Authorization header.
**Authorization**: Role-based access control (Admin, Manager, User).

### **Authentication Endpoints** (`/api/auth`)

#### POST `/api/auth/login`
**Description**: Authenticate user and obtain JWT access token and refresh token.
**Auth**: None (public)
**Body**:
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```
**Response (200 OK)**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64encodedtoken",
  "expiresIn": 3600,
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "firstName": "John",
    "roles": ["User"]
  }
}
```

#### POST `/api/auth/refresh`
**Description**: Exchange refresh token for new access token.
**Auth**: None (public)
**Body**:
```json
{
  "refreshToken": "base64encodedtoken"
}
```
**Response (200 OK)**: Same as login response.

#### POST `/api/auth/logout`
**Description**: Revoke refresh token and invalidate session.
**Auth**: Required (Bearer token)
**Body**: Empty
**Response (200 OK)**: `{ "message": "Logged out successfully" }`

#### POST `/api/auth/validate-token`
**Description**: Validate current JWT token and return claims.
**Auth**: Required (Bearer token)
**Response (200 OK)**:
```json
{
  "isValid": true,
  "userId": "user-id",
  "email": "user@example.com",
  "roles": ["Admin"],
  "expiresAt": "2024-12-31T23:59:59Z",
  "issuedAt": "2024-12-24T23:59:59Z"
}
```

---

### **Assets Endpoints** (`/api/assets`)

#### GET `/api/assets`
**Description**: Retrieve paginated list of all assets with search and filtering.
**Auth**: Required
**Query Parameters**:
- `searchTerm` (string, optional): Search assets by name, tag, serial number
- `statusFilter` (string, optional): Filter by status (Available, In Use, Under Maintenance, Retired, Lost)
- `conditionFilter` (string, optional): Filter by condition (New, Good, Fair, Poor, Broken)
- `categoryId` (string, optional): Filter by category ID
- `departmentId` (string, optional): Filter by department ID
- `locationId` (string, optional): Filter by location ID
- `page` (int, default: 1): Page number for pagination
- `pageSize` (int, default: 10): Records per page

**Response (200 OK)**:
```json
[
  {
    "assetId": "asset-id",
    "assetTag": "ASSET-001",
    "name": "Laptop",
    "status": "In Use",
    "condition": "Good",
    "currentValue": 800.00,
    "categoryId": "cat-id",
    "locationId": "loc-id",
    "departmentId": "dept-id",
    "purchaseDate": "2023-01-15T00:00:00Z"
  }
]
```

#### GET `/api/assets/{id}`
**Description**: Retrieve single asset details by ID.
**Auth**: Required
**Response (200 OK)**: Single asset object (same structure as list)

#### POST `/api/assets`
**Description**: Create new asset (Admin only).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "assetTag": "ASSET-NEW",
  "name": "New Equipment",
  "description": "Description here",
  "status": "Available",
  "condition": "New",
  "categoryId": "cat-id",
  "locationId": "loc-id",
  "departmentId": "dept-id",
  "currentValue": 1000.00,
  "purchaseDate": "2024-01-01",
  "serialNumber": "SN12345"
}
```
**Response (201 Created)**: Created asset object with ID.

#### PUT `/api/assets/{id}`
**Description**: Update existing asset (Admin only).
**Auth**: Required (Admin role)
**Body**: Partial asset data (same fields as POST)
**Response (200 OK)**: Updated asset object.

#### DELETE `/api/assets/{id}`
**Description**: Soft delete asset (mark as inactive).
**Auth**: Required (Admin role)
**Response (204 No Content)**: Empty response.

---

### **Categories Endpoints** (`/api/categories`)

#### GET `/api/categories`
**Description**: Retrieve all categories.
**Auth**: Required
**Response (200 OK)**:
```json
[
  {
    "categoryId": "cat-001",
    "name": "Electronics",
    "description": "Electronic equipment"
  }
]
```

#### GET `/api/categories/{id}`
**Description**: Retrieve single category.
**Auth**: Required
**Response (200 OK)**: Category object.

#### POST `/api/categories`
**Description**: Create category (Admin only).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "name": "Furniture",
  "description": "Office furniture"
}
```
**Response (201 Created)**: Created category object.

#### PUT `/api/categories/{id}`
**Description**: Update category (Admin only).
**Auth**: Required (Admin role)
**Body**: Category partial data.
**Response (204 No Content)**: Empty.

#### DELETE `/api/categories/{id}`
**Description**: Delete category (Admin only, Restrict if assets exist).
**Auth**: Required (Admin role)
**Response (204 No Content)**: Empty.

---

### **Departments Endpoints** (`/api/departments`)

Same structure as Categories with additional validation (unique department names).

---

### **Locations Endpoints** (`/api/locations`)

#### GET `/api/locations`
**Description**: Retrieve all locations.
**Auth**: Required
**Response (200 OK)**:
```json
[
  {
    "locationId": "loc-001",
    "name": "Main Office",
    "campus": "Downtown",
    "building": "Building A",
    "room": "101",
    "latitude": 40.7128,
    "longitude": -74.0060
  }
]
```

#### POST `/api/locations`
**Description**: Create location (Admin only, unique Name+Campus constraint).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "name": "Conference Room",
  "campus": "Midtown",
  "building": "Tower B",
  "room": "500",
  "latitude": 40.7580,
  "longitude": -73.9855
}
```

---

### **Asset Histories Endpoints** (`/api/assethistories`)

#### GET `/api/assethistories`
**Description**: Retrieve paginated audit history with filters.
**Auth**: Required
**Query Parameters**:
- `assetId` (string, optional): Filter by asset
- `userId` (string, optional): Filter by user who made change
- `action` (string, optional): Filter by action type
- `fromDate` (datetime, optional): Start date
- `toDate` (datetime, optional): End date
- `page` (int, default: 1)
- `pageSize` (int, default: 10)

**Response (200 OK)**:
```json
[
  {
    "historyId": "hist-001",
    "assetId": "asset-id",
    "action": "Update",
    "description": "Status changed to In Use",
    "timestamp": "2024-01-20T15:30:00Z",
    "userId": "user-id",
    "oldStatus": "Available",
    "newStatus": "In Use"
  }
]
```

#### GET `/api/assethistories/asset/{assetId}`
**Description**: Retrieve history for specific asset.
**Auth**: Required

---

### **Users Endpoints** (`/api/users`)

#### GET `/api/users`
**Description**: Retrieve paginated list of users (public, no auth required).
**Query Parameters**:
- `search` (string, optional): Search by name or email
- `departmentId` (string, optional): Filter by department
- `isActive` (bool, optional): Filter by active status
- `page` (int, default: 1)
- `pageSize` (int, default: 10)

**Response (200 OK)**:
```json
[
  {
    "id": "user-id",
    "userName": "johndoe",
    "email": "john@example.com",
    "firstName": "John",
    "surname": "Doe",
    "isActive": true,
    "departmentId": "dept-id"
  }
]
```

#### GET `/api/users/{id}`
**Description**: Retrieve specific user details.
**Auth**: Required

#### GET `/api/users/by-email/{email}`
**Description**: Retrieve user by email.
**Auth**: Required

#### PUT `/api/users/{id}`
**Description**: Update user profile (Admin only).
**Auth**: Required (Admin role)

#### PATCH `/api/users/{id}/activation`
**Description**: Toggle user active status (Admin only).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "isActive": true
}
```

#### DELETE `/api/users/{id}`
**Description**: Soft delete user (deactivate).
**Auth**: Required (Admin role)

#### GET `/api/users/{id}/roles`
**Description**: Get user's assigned roles (Admin only).
**Auth**: Required (Admin role)

#### POST `/api/users/{id}/roles`
**Description**: Assign role to user (Admin only).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "roleName": "Admin"
}
```

#### DELETE `/api/users/{id}/roles`
**Description**: Remove role from user (Admin only).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "roleName": "Manager"
}
```

---

### **Invitations Endpoints** (`/api/invitations`)

#### POST `/api/invitations`
**Description**: Create and send invitation (Admin only).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "email": "newuser@example.com",
  "role": "User"
}
```
**Response (200 OK)**: Invitation object with token.

#### GET `/api/invitations`
**Description**: Retrieve all invitations (Admin only).
**Auth**: Required (Admin role)

#### POST `/api/invitations/multiple`
**Description**: Create multiple invitations (Admin only, bulk operation).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "emails": ["user1@example.com", "user2@example.com"],
  "role": "User"
}
```
**Response (200 OK)**:
```json
{
  "successfulInvitations": [...],
  "failedInvitations": [...],
  "totalProcessed": 2,
  "successfulCount": 2,
  "failedCount": 0
}
```

#### POST `/api/invitations/resend/{id}`
**Description**: Resend invitation email (Admin only).
**Auth**: Required (Admin role)

#### DELETE `/api/invitations/{id}`
**Description**: Delete invitation (Admin only).
**Auth**: Required (Admin role)

#### GET `/api/invitations/validate/{token}`
**Description**: Validate invitation token (public, for registration).
**Auth**: None (public)
**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "email": "user@example.com",
    "token": "token",
    "role": "User",
    "expiresAt": "2024-12-31T23:59:59Z"
  }
}
```

---

### **Dashboard Endpoints** (`/api/dashboard`)

#### GET `/api/dashboard/data`
**Description**: Retrieve comprehensive dashboard analytics data.
**Auth**: Required
**Response (200 OK)**:
```json
{
  "totalAssets": 150,
  "availableAssets": 100,
  "inUseAssets": 40,
  "underMaintenanceAssets": 5,
  "retiredAssets": 3,
  "lostAssets": 2,
  "totalAssetValue": 50000.00,
  "monthlyDepreciation": 416.67,
  "recentActivities": 25,
  "assetsDueForMaintenance": 5,
  "warrantyExpiringSoon": 3,
  "statusChartData": [...],
  "conditionChartData": [...],
  "monthlyValueData": [...],
  "recentAssetHistories": [...],
  "totalCategories": 8,
  "totalLocations": 5,
  "totalDepartments": 4,
  "dataLoadTimeMs": 245,
  "timestamp": "2024-12-24T15:30:00Z"
}
```

#### GET `/api/dashboard/quick-stats`
**Description**: Lightweight stats endpoint (cacheable).
**Auth**: Required
**Response (200 OK)**:
```json
{
  "totalAssets": 150,
  "availableAssets": 100,
  "totalValue": 50000.00,
  "lastUpdated": "2024-12-24T15:30:00Z"
}
```

---

### **Diagnostics Endpoints** (`/api/diagnostics`)

#### GET `/api/diagnostics/server-time`
**Description**: Get server UTC time (for clock skew detection).
**Auth**: None (public)
**Response (200 OK)**:
```json
{
  "serverUtc": "2024-12-24T15:30:00Z"
}
```

---

### **Reports Endpoints** (`/api/reports`)

#### POST `/api/reports/generate`
**Description**: Generate report with AI or predefined template (Admin only).
**Auth**: Required (Admin role)
**Body**:
```json
{
  "reportType": "assets-by-status",
  "filters": {
    "dateFrom": "2024-01-01",
    "dateTo": "2024-12-31"
  }
}
```

#### POST `/api/reports/ai-query`
**Description**: Execute natural language AI query for custom reports.
**Auth**: Required
**Body**:
```json
{
  "question": "How many laptops are in the IT department?"
}
```
**Response (200 OK)**:
```json
{
  "sqlQuery": "SELECT COUNT(*) FROM Assets...",
  "results": [...],
  "executionTimeMs": 125,
  "rowCount": 45,
  "timestamp": "2024-12-24T15:30:00Z"
}
```

---

## Authentication Flow

### Overview

AssetTag uses **JWT (JSON Web Token)** based authentication with refresh token rotation for secure, stateless API communication. The system implements industry-standard OAuth2-like patterns with automatic token refresh and revocation support.

### Authentication Sequence Diagram

```
???????????????                    ????????????????                  ????????????????
?   Portal    ?                    ?   AssetTag   ?                  ?  Database    ?
?  (Frontend) ?                    ?   (API)      ?                  ?   (SQL Srv)  ?
???????????????                    ????????????????                  ????????????????
       ?                                   ?                                ?
       ?  1. POST /api/auth/login          ?                                ?
       ?  (email, password)                ?                                ?
       ?????????????????????????????????????                                ?
       ?                                   ?  2. Find user & verify         ?
       ?                                   ?     password                   ?
       ?                                   ??????????????????????????????????
       ?                                   ??????????????????????????????????
       ?                                   ?  3. Return user + IsActive     ?
       ?                                   ?                                ?
       ?                                   ?  4. Get user roles             ?
       ?                                   ??????????????????????????????????
       ?                                   ??????????????????????????????????
       ?                                   ?  5. Return roles list          ?
       ?                                   ?                                ?
       ?  9. Return tokens & user info     ?  6-8. Create/Store tokens      ?
       ?????????????????????????????????????                                ?
       ?                                   ?                                ?
       ???????????? AUTHENTICATED SESSION ???????????????????????????????????
       ?  10-12. Make API requests with token                              ?
       ?  13. GET /api/assets              ?                                ?
       ?  Authorization: Bearer <token>    ?                                ?
       ????????????????????????????????????? 14-18. Validate token & data   ?
       ?                                   ??????????????????????????????????
       ?  19. Return asset list            ?                                ?
       ?????????????????????????????????????                                ?
       ?                                   ?                                ?
       ???????????? TOKEN EXPIRATION ????????????????????????????????????????
       ?  20. Token expires (401 response) ?                                ?
       ?  22. Auto-refresh refresh token   ?                                ?
       ?  23-25. Validate refresh token    ?                                ?
       ?????????????????????????????????????                                ?
       ?                                   ??????????????????????????????????
       ?  31. Receive new tokens           ?  26-30. Create new tokens      ?
       ?????????????????????????????????????                                ?
       ?  32. Retry original request       ?                                ?
       ?  33. Request succeeds             ?                                ?
       ??????????????????????????????????????????????????????????????????????
```

### Key Components

#### **1. Login Process** (`POST /api/auth/login`)

**Request**:
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Validation Steps**:
1. Find user by email in database
2. Verify password hash using ASP.NET Identity
3. Check if user account is active (`IsActive == true`)
4. Retrieve user roles from database
5. Create new access token with claims
6. Create new refresh token (random 48-byte value)
7. Store refresh token in database with IP address, creation time, and expiration
8. Return both tokens to client

**Response (200 OK)**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64encodedtoken123..."
}
```

#### **2. Access Token Structure**

**Type**: JWT with HS256 (HMAC SHA256) signature

**Claims Include**:
- `sub` - Subject (User ID)
- `unique_name` - Username
- `email` - User email
- `role` - User roles (can be multiple)
- `exp` - Expiration timestamp (Unix time)
- `iat` - Issued at timestamp
- `jti` - JWT ID (unique identifier)
- `is_active` - Boolean flag for active status
- `aud` - Audience (configured in appsettings.json)
- `iss` - Issuer (configured in appsettings.json)

**Configuration** (in `appsettings.json`):
```json
{
  "JwtSettings": {
    "SecurityKey": "your-very-long-secret-key-min-32-chars",
    "Issuer": "AssetTagAPI",
    "Audience": "AssetTagPortal",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

**Default Expiration**:
- Access Token: 60 minutes
- Refresh Token: 7 days

#### **3. Token Validation Middleware**

All requests with Authorization header are validated in the JWT bearer handler:

**Validation Steps**:
1. Extract token from `Authorization: Bearer <token>` header
2. Verify token signature using `SecurityKey`
3. Check token hasn't expired (`exp` claim vs server UTC time)
4. Verify issuer matches configured value
5. Verify audience matches configured value
6. Extract and validate all claims
7. Add `ClaimsPrincipal` to HttpContext.User
8. Allow request to proceed

**Error Cases**:
- **401 Unauthorized**: Token missing, invalid, expired, or signature verification failed
- **403 Forbidden**: Token valid but user lacks required role
- Comprehensive logging captures all validation details

#### **4. Token Refresh Flow** (`POST /api/auth/refresh-token`)

**When It Triggers**:
- Frontend detects 401 response while token in claims
- Automatically calls refresh endpoint
- `TokenRefreshHandler` interceptor handles this transparently

**Validation Steps**:
1. Extract refresh token from request body
2. Find token in database
3. Verify not revoked (`Revoked == null`)
4. Verify not expired (`Expires > current UTC time`)
5. Load associated user
6. Verify user is still active
7. Get current user roles
8. Create new access token (same claims structure)
9. Create new refresh token
10. Mark old refresh token as revoked
11. Store revocation info (IP, timestamp)
12. Store new refresh token in database
13. Return new tokens to client

**Response (200 OK)**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "newbase64encodedtoken456..."
}
```

#### **5. Portal Implementation** (Razor Pages)

**Login Process** (in `Portal/Pages/Account/Login.cshtml.cs`):
1. User submits email/password form
2. Call `ApiAuthService.LoginAsync()` ? API `/api/auth/login`
3. Extract email and roles from returned access token
4. Create authentication cookie with claims:
   - `ClaimTypes.Name` = email
   - `ClaimTypes.Email` = email
   - `ClaimTypes.Role` = each role
   - Custom claim `"AccessToken"` = access token string
   - Custom claim `"RefreshToken"` = refresh token string
5. Cookie expires based on "Remember Me" checkbox
6. Redirect to dashboard

**Token Refresh Handler** (in `Portal/Services/TokenRefreshHandler.cs`):
- Intercepts HTTP responses
- Detects 401 status codes
- Extracts refresh token from cookie claims
- Calls `/api/auth/refresh-token` endpoint
- Updates cookie with new tokens
- Retries original request

**Automatic Token Mapping**:
- Middleware maps `Authorization: X-Auth-Token` custom header to standard Authorization header
- Allows flexible authentication header handling

#### **6. Token Revocation**

**Manual Revocation** (`POST /api/auth/revoke`):
- Mark specific refresh token as revoked
- Record revocation IP address and timestamp
- Token becomes invalid for future refresh requests

**Automatic Revocation**:
- On password reset: all user's refresh tokens revoked
- On account deactivation: tokens rejected on validation

#### **7. User Deactivation Flow**

When admin deactivates user account:
1. Set `IsActive = false` on user record
2. Active tokens continue working until expiration
3. Token refresh requests check `IsActive` and reject if false
4. Login attempts rejected immediately
5. API requests with valid tokens still work briefly (until expiration)
6. Post-expiration, refresh fails due to `IsActive` check

#### **8. Diagnostics & Troubleshooting**

**Token Diagnostics Page** (`/Diagnostics/TokenDiagnostics`):
- Displays raw token (truncated for security)
- Decodes JWT header and all claims
- Calls API `/api/auth/validate-token` endpoint
- Shows token expiration countdown
- Highlights expiring-soon tokens (< 5 minutes)
- Allows pasting custom tokens for validation
- Comprehensive logging with correlation IDs

**Server Time Diagnostics** (`/Diagnostics/TimeCheck`):
- Check server UTC time vs browser time
- Diagnoses clock skew issues
- Helps identify token expiration problems

### Security Considerations

1. **Token Storage**: Stored in HTTP-only cookies (not accessible to JavaScript)
2. **HTTPS Only**: Cookies marked secure (transmitted only over HTTPS)
3. **SameSite Protection**: Cookies use SameSite=Lax to prevent CSRF
4. **Token Rotation**: Old refresh token revoked when new one created
5. **Expiration**: Short-lived access tokens (60 min) + longer refresh tokens (7 days)
6. **Signature Validation**: All tokens validated against signing key
7. **Claim Validation**: Issuer and audience must match configured values
8. **IP Tracking**: Token creation/revocation IP addresses logged
9. **Clock Skew Protection**: 5-minute tolerance in token validation
10. **Active User Check**: Deactivated users rejected even with valid tokens

---

## Entity Models

All entity models are defined in `AssetTag/Models/` and implement EF Core with data annotations. ULID is used for primary keys to provide better distributed ID generation compared to GUIDs.

### **ApplicationUser** (Extended ASP.NET Identity)

```csharp
public class ApplicationUser : IdentityUser
{
    // Core Identity Fields (inherited)
    // public string Id { get; set; }
    // public string UserName { get; set; }
    // public string NormalizedUserName { get; set; }
    // public string Email { get; set; }
    // public string NormalizedEmail { get; set; }
    // public string PasswordHash { get; set; }
    // public string SecurityStamp { get; set; }
    // ... other Identity fields

    // Custom Profile Fields
    public required string FirstName { get; set; }
    public required string Surname { get; set; }
    public string? OtherNames { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? JobRole { get; set; }
    public string? ProfileImage { get; set; }

    // Account Status
    public bool IsActive { get; set; } = true;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    // Department Association
    public string? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Navigation Properties
    public ICollection<Asset>? Assets { get; set; } = new List<Asset>();
    public List<RefreshTokens> RefreshTokens { get; set; } = new();
}
```

**Key Features**:
- Extends `IdentityUser` for built-in password hashing, claims management
- Soft delete via `IsActive` flag (not hard deletion)
- Optional department association for organizational structure
- Tracks account creation timestamp
- Maintains list of refresh tokens for token rotation
- Supports multiple assets assignment

---

### **Asset** (Core Asset Management)

```csharp
public class Asset
{
    // Identity
    public string AssetId { get; set; } = Ulid.NewUlid().ToString();
    public required string AssetTag { get; set; }  // Unique, indexed
    public string? OldAssetTag { get; set; }

    // Basic Information
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }

    // Classification & Location
    public required string CategoryId { get; set; }
    public required string LocationId { get; set; }
    public required string DepartmentId { get; set; }
    public string? AssignedToUserId { get; set; }

    // Status & Condition
    public required string Status { get; set; }  // Available, In Use, Under Maintenance, Retired, Lost
    public required string Condition { get; set; }  // New, Good, Fair, Poor, Broken

    // Financial Tracking
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public string? VendorName { get; set; }
    public string? InvoiceNumber { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? CostPerUnit { get; set; }
    public decimal? TotalCost { get; set; }

    // Depreciation Calculations
    public decimal? DepreciationRate { get; set; }  // Percentage
    public decimal? AccumulatedDepreciation { get; set; }
    public decimal? NetBookValue { get; set; }
    public int? UsefulLifeYears { get; set; }

    // Warranty & Lifecycle
    public DateTime? WarrantyExpiry { get; set; }
    public DateTime? DisposalDate { get; set; }
    public decimal? DisposalValue { get; set; }
    public string? Remarks { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public Category Category { get; set; }
    public Location Location { get; set; }
    public Department Department { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }
    public ICollection<AssetHistory> AssetHistories { get; set; } = new List<AssetHistory>();
}
```

**Key Features**:
- Comprehensive financial tracking with depreciation calculations
- Status and condition enums for filtering and reporting
- Historical audit trail via `AssetHistories` collection
- Soft associations (nullable ForeignKeys) for flexibility
- Timestamps for creation and modification tracking
- Supports bulk items with quantity field

---

### **Category** (Asset Classification)

```csharp
public class Category
{
    public string CategoryId { get; set; } = Ulid.NewUlid().ToString();
    public required string Name { get; set; }
    public string? Description { get; set; }

    // Navigation
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
```

**Constraint**: Cannot delete category if assets exist (Restrict delete behavior)

---

### **Department** (Organizational Structure)

```csharp
public class Department
{
    public string DepartmentId { get; set; } = Ulid.NewUlid().ToString();
    public required string Name { get; set; }  // Unique, indexed
    public string? Description { get; set; }

    // Navigation
    public required ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Asset>? Assets { get; set; } = new List<Asset>();
}
```

**Constraint**: Department names must be unique. Cannot delete if assets or users assigned (Restrict delete behavior)

---

### **Location** (Physical Locations)

```csharp
public class Location
{
    public string LocationId { get; set; } = Ulid.NewUlid().ToString();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Campus { get; set; }
    public string? Building { get; set; }
    public string? Room { get; set; }

    // Geographic Information
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    public double? Latitude { get; set; }
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    public double? Longitude { get; set; }

    // Navigation
    public required ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
```

**Constraint**: Composite unique index on (Name, Campus) - name must be unique per campus. Cannot delete if assets exist (Restrict delete behavior)

---

### **AssetHistory** (Audit Trail)

```csharp
public class AssetHistory
{
    // Identity
    public string HistoryId { get; set; } = Ulid.NewUlid().ToString();

    // References
    public required string AssetId { get; set; }
    public required string UserId { get; set; }
    public string? OldLocationId { get; set; }
    public string? NewLocationId { get; set; }

    // Audit Information
    public required string Action { get; set; }  // Create, Update, Maintain, Transfer, etc.
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }

    // Navigation
    public Asset Asset { get; set; }
    public ApplicationUser User { get; set; }
    public Location? OldLocation { get; set; }
    public Location? NewLocation { get; set; }
}
```

**Key Features**:
- Complete audit log of all asset changes
- Tracks status transitions and location movements
- Records who made changes and when
- Automatically deleted when asset is deleted (Cascade)
- Enables compliance and troubleshooting

---

### **RefreshTokens** (Token Lifecycle Management)

```csharp
public class RefreshTokens
{
    public int Id { get; set; }  // Auto-increment
    public required string Token { get; set; }
    public required string ApplicationUserId { get; set; }

    // Lifecycle Tracking
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public required string CreatedByIp { get; set; }
    public DateTime Expires { get; set; }
    public DateTime? Revoked { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }

    // Computed Properties
    public bool IsExpired => DateTime.UtcNow >= Expires;
    public bool IsActive => Revoked == null && !IsExpired;

    // Navigation
    public ApplicationUser? ApplicationUser { get; set; }
}
```

**Key Features**:
- Tracks token creation, expiration, revocation
- Records IP addresses for security auditing
- Enables token rotation and chain tracking
- Deleted when user is deleted (Cascade)
- Used for automatic cleanup of expired tokens

---

### **Invitation** (User Onboarding)

```csharp
public class Invitation
{
    [Key]
    public string Id { get; set; } = Ulid.NewUlid().ToString();

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = Guid.NewGuid().ToString();  // Unique, indexed

    // Lifecycle
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }

    // Authorization
    public string? Role { get; set; } = "User";

    // Audit
    public required string InvitedByUserId { get; set; }
    public ApplicationUser? InvitedByUser { get; set; }
}
```

**Key Features**:
- Time-limited invitation links (7-day default)
- Self-destructing after first use
- Tracks who sent the invitation
- Prevents invitation reuse
- Role assignment upon registration

---

### Data Annotations & EF Core Configuration

**Common Patterns Used**:

1. **ULID Primary Keys**:
   ```csharp
   public string AssetId { get; set; } = Ulid.NewUlid().ToString();
   ```
   - Distributed ID generation (no database round-trip)
   - Ordered by timestamp (better for indexing)
   - 128-bit like GUID but more efficient

2. **Required Fields**:
   ```csharp
   public required string Name { get; set; }
   ```
   - Enforces non-null in C# compiler and EF Core
   - Becomes NOT NULL in database schema

3. **Unique Constraints**:
   ```csharp
   [Index(nameof(AssetTag), IsUnique = true)]
   public required string AssetTag { get; set; }
   ```

4. **Composite Unique Indexes**:
   ```csharp
   [Index(nameof(Name), nameof(Campus), IsUnique = true)]
   ```

5. **Range Validation**:
   ```csharp
   [Range(-90.0, 90.0)]
   public double? Latitude { get; set; }
   ```

6. **Email Validation**:
   ```csharp
   [EmailAddress]
   public string Email { get; set; }
   ```

7. **Default Values**:
   ```csharp
   public bool IsActive { get; set; } = true;
   public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
   ```

8. **Delete Behaviors**:
   - **Cascade**: Automatically delete related records (AssetHistories when Asset deleted)
   - **SetNull**: Set FK to null on parent delete (AssignedToUser when user deactivated)
   - **Restrict**: Prevent delete if related records exist (Category/Location with assets)

---

## Main Features & User Stories

### Core Features Overview

AssetTag implements a comprehensive set of features designed to streamline asset management across Methodist University Ghana. The following sections outline key functionalities organized by user roles and operational workflows.

---

### **1. Asset Management & Lifecycle**

#### **Asset Registry & CRUD Operations**
- ? Create new assets with comprehensive details (name, tag, category, location, department, condition)
- ? Read/view detailed asset information including financial and depreciation data
- ? Update asset properties (status, condition, location, assigned user, values)
- ? Soft-delete assets (mark as inactive without permanent removal)
- ? Bulk asset import/export capabilities (via CSV/Excel for future enhancement)
- ? Asset tagging system with unique ULID-based identifiers
- ? Serial number and vendor tracking

**User Story**: *As an asset manager, I want to maintain a comprehensive registry of all organizational assets so that I can track their location, condition, and financial information at any time.*

#### **Asset Classification**
- ? Multi-level categorization (Electronics, Furniture, Equipment, etc.)
- ? Category management (CRUD operations)
- ? Asset filtering by category
- ? Category-based reporting and analytics

**User Story**: *As an inventory officer, I want to organize assets into logical categories so that I can quickly find and report on specific asset types.*

#### **Asset Condition & Status Tracking**
- ? Condition states: New, Good, Fair, Poor, Broken
- ? Status states: Available, In Use, Under Maintenance, Retired, Lost
- ? Real-time status change logging with audit trail
- ? Condition-based filtering and reporting

**User Story**: *As a maintenance coordinator, I want to track asset condition and status so that I can schedule maintenance and identify problem assets.*

#### **Location & Department Management**
- ? Physical location tracking (campus, building, room)
- ? Geographic coordinates (latitude/longitude) for mapping
- ? Department-based asset allocation
- ? Multi-location asset organization
- ? Location-based analytics and reporting

**User Story**: *As a facilities manager, I want to track where each asset is physically located and which department owns it so that I can optimize resource distribution.*

---

### **2. Financial Management & Depreciation**

#### **Asset Valuation**
- ? Purchase price and current value tracking
- ? Cost-per-unit for bulk purchases
- ? Total acquisition cost calculations
- ? Disposal value recording
- ? Real-time asset portfolio value calculation

**User Story**: *As a finance officer, I want to track the financial value of all assets so that I can maintain accurate inventory valuations for financial reporting.*

#### **Depreciation Calculations**
- ? Configurable depreciation rates per asset
- ? Useful life tracking (in years)
- ? Accumulated depreciation calculation
- ? Net book value computation
- ? Depreciation-based financial reporting

**User Story**: *As an accountant, I want to calculate asset depreciation automatically so that our financial statements accurately reflect asset values.*

#### **Warranty & Lifecycle Management**
- ? Warranty expiration date tracking
- ? Warranty expiring soon alerts
- ? Asset disposal date and value recording
- ? Lifecycle phase identification
- ? End-of-life asset reporting

**User Story**: *As an asset coordinator, I want to track warranties and lifecycle milestones so that I can plan for maintenance contracts and asset replacement.*

---

### **3. Dashboard & Analytics**

#### **Real-Time Dashboard**
- ? Asset count statistics (total, available, in-use, under maintenance, retired, lost)
- ? Total asset value display with currency formatting
- ? Performance indicators and trend calculations
- ? Data load time metrics and cache status indicators
- ? Last updated timestamp
- ? Quick-refresh functionality for real-time data

**User Story**: *As an executive, I want to see a dashboard with key asset metrics so that I can monitor organizational asset health at a glance.*

#### **Interactive Charts & Visualizations**
- ? Asset distribution by status (pie/doughnut chart)
- ? Asset distribution by condition (bar chart)
- ? Monthly asset value trends
- ? Depreciation trends over time
- ? Department-based asset distribution
- ? Location-based asset distribution
- ? Responsive, mobile-friendly charts using Chart.js

**User Story**: *As a manager, I want to visualize asset distribution and trends so that I can identify patterns and make data-driven decisions.*

#### **Key Alerts & Notifications**
- ? Assets due for maintenance alerts
- ? Warranty expiring soon notifications
- ? Asset condition alerts (Poor/Broken items)
- ? Overdue audit reminders

**User Story**: *As a maintenance manager, I want to receive alerts about assets needing attention so that I can proactively schedule maintenance.*

---

### **4. Audit Trail & Compliance**

#### **Complete Audit History**
- ? Asset history tracking for every change
- ? Action logging (Create, Update, Maintain, Transfer, etc.)
- ? User attribution (who made the change)
- ? Timestamp recording (when changes occurred)
- ? Before/after value tracking for modifications

**User Story**: *As a compliance officer, I want a complete audit trail of all asset changes so that I can verify accountability and maintain regulatory compliance.*

#### **Status & Location Change Tracking**
- ? Status transition history (Available ? In Use ? etc.)
- ? Location movement history with source and destination
- ? User-to-asset assignments with timestamps
- ? Queryable audit logs with filters

**User Story**: *As an auditor, I want to trace the complete history of asset movements and status changes so that I can verify proper asset management procedures.*

#### **Audit Filtering & Reporting**
- ? Filter by asset, user, date range, or action type
- ? Export audit trails for compliance documentation
- ? Pagination for large audit logs
- ? Search and drill-down capabilities

**User Story**: *As a manager, I want to query asset history to understand when and how assets were used so that I can resolve disputes and improve processes.*

---

### **5. Search & Filtering**

#### **Multi-Criteria Asset Search**
- ? Full-text search by asset name, tag, or serial number
- ? Filter by status (Available, In Use, etc.)
- ? Filter by condition (New, Good, Fair, etc.)
- ? Filter by category
- ? Filter by department
- ? Filter by location
- ? Combined filter capabilities (AND logic)
- ? Real-time search with auto-complete suggestions

**User Story**: *As a user, I want to quickly find assets using multiple search criteria so that I can locate specific items without browsing entire lists.*

#### **Pagination & Performance**
- ? Configurable page sizes (10, 25, 50, 100 items)
- ? Large dataset handling without performance degradation
- ? Quick navigation between pages
- ? Search result count display

**User Story**: *As a user with large asset inventories, I want efficient pagination so that the system remains responsive with thousands of assets.*

---

### **6. User Management & Authorization**

#### **User CRUD Operations**
- ? Create new user accounts with detailed profiles
- ? Edit user information (name, contact, job role, department, profile image)
- ? View user details and assignment history
- ? Soft-delete users (deactivate without removal)
- ? Bulk user operations (future enhancement)

**User Story**: *As an admin, I want to manage user accounts so that I can control access and maintain user information.*

#### **Role-Based Access Control (RBAC)**
- ? Three default roles: Admin, Manager, User
- ? Create custom roles dynamically
- ? Assign multiple roles to individual users
- ? Remove roles from users
- ? Role-specific API endpoint access
- ? Role-specific UI component visibility

**User Story**: *As an administrator, I want to assign roles to users so that I can control what features each user can access.*

#### **User Activation/Deactivation**
- ? Toggle user active status
- ? Deactivated users cannot login
- ? Active user check in API validation
- ? Historical tracking of activation changes

**User Story**: *As an admin, I want to deactivate user accounts so that I can prevent unauthorized access without permanently deleting user data.*

#### **User Invitations**
- ? Send invitation emails to new users
- ? Bulk invitation support (send to multiple emails at once)
- ? 7-day expiring invitation links
- ? Resend invitation functionality
- ? One-time use invitation tokens
- ? Role assignment via invitations
- ? Email-based user onboarding

**User Story**: *As an admin, I want to invite new users via email so that they can self-register without requiring manual account creation.*

---

### **7. Authentication & Security**

#### **JWT-Based Authentication**
- ? Secure login with email and password
- ? JWT access token generation (60-minute default expiration)
- ? Refresh token support (7-day default expiration)
- ? Automatic token refresh on expiration
- ? Token revocation on logout
- ? Token validation middleware
- ? Clock skew tolerance (5-minute window)

**User Story**: *As a user, I want secure authentication so that only authorized users can access the system.*

#### **Password Management**
- ? Secure password hashing (ASP.NET Identity)
- ? Minimum length requirements (6 characters)
- ? Complexity requirements (digits, uppercase)
- ? Password reset via email link
- ? Admin password reset functionality
- ? Token expiration on password reset (revokes all active sessions)

**User Story**: *As a user, I want to reset my password securely so that I can regain access if I forget my credentials.*

#### **Token Diagnostics & Troubleshooting**
- ? Comprehensive token diagnostics page
- ? JWT claims inspection and display
- ? Token expiration countdown
- ? Manual token validation for debugging
- ? Server time synchronization check
- ? Detailed error messages for authentication failures
- ? Correlation IDs for request tracing

**User Story**: *As a developer, I want to diagnose token-related issues so that I can troubleshoot authentication problems efficiently.*

---

### **8. Reporting & AI-Powered Queries**

#### **Predefined Reports**
- ? Assets by status report
- ? Assets by condition report
- ? Assets by category report
- ? Assets by department report
- ? Financial summary report (values, depreciation)
- ? Warranty expiration report
- ? Maintenance due report

**User Story**: *As a manager, I want pre-built reports so that I can quickly generate insights without writing custom queries.*

#### **AI-Powered Natural Language Queries** (via Groq API)
- ? Ask questions in plain English (e.g., "How many laptops are in IT?")
- ? Automatic SQL generation from natural language
- ? SQL validation and safety checks
- ? Query execution and result display
- ? Execution time metrics
- ? Result row count and pagination
- ? Query history for future reference

**User Story**: *As a non-technical user, I want to ask questions in plain language so that I can get custom reports without SQL knowledge.*

#### **Report Customization**
- ? Date range filtering
- ? Department and category filtering
- ? Export to CSV/PDF (future enhancement)
- ? Scheduled report delivery (future enhancement)

**User Story**: *As a manager, I want to customize reports with specific filters so that I can focus on relevant data.*

---

### **9. Email Notifications**

#### **Automated Email Communications**
- ? Password reset emails with secure links
- ? User invitation emails with registration links
- ? Welcome emails for new accounts
- ? Maintenance due notifications (future enhancement)
- ? Warranty expiration alerts (future enhancement)
- ? Configurable SMTP settings
- ? HTML email templates

**User Story**: *As a user, I want to receive email notifications so that I stay informed about system activities and important dates.*

---

### **10. Real-Time Data & Caching**

#### **Live Data Updates**
- ? Real-time dashboard statistics
- ? Live asset count calculations
- ? Current value computations
- ? Data load time metrics
- ? Cache vs. live data indicators

**User Story**: *As a manager, I want to see live asset data so that my decisions are based on current information.*

#### **Performance Optimization**
- ? Database connection pooling
- ? Query optimization with indexes
- ? Response caching for frequently accessed data
- ? Pagination for large datasets
- ? Async/await for non-blocking operations

**User Story**: *As a user, I want the system to be responsive even with large asset inventories so that I can work efficiently.*

---

### **11. Data Integrity & Validation**

#### **Input Validation**
- ? Email format validation
- ? Required field enforcement
- ? Data type validation (dates, numbers, etc.)
- ? Unique constraint enforcement (asset tags, department names, etc.)
- ? Range validation (latitude/longitude)
- ? Business rule validation (can't delete category with assets)

**User Story**: *As a user, I want the system to validate my input so that I don't accidentally create invalid data.*

#### **Referential Integrity**
- ? Foreign key constraints
- ? Cascading deletes (asset histories deleted with asset)
- ? Restrict deletes (prevent deletion if dependencies exist)
- ? Orphan prevention

**User Story**: *As a DBA, I want referential integrity so that the database maintains consistency and prevents orphaned records.*

---

### **12. Responsive Web Interface**

#### **Front-End Technologies**
- ? Bootstrap 5 responsive design
- ? Mobile-friendly layouts
- ? Accessible form controls
- ? Interactive modals and dialogs
- ? Client-side form validation
- ? Smooth animations and transitions
- ? Dark-mode compatible styling (future enhancement)

**User Story**: *As a user, I want to access AssetTag on any device so that I can work from desktop, tablet, or mobile.*

#### **User Interface Components**
- ? Intuitive navigation menu
- ? Contextual action buttons
- ? Success/error message alerts
- ? Loading indicators for async operations
- ? Breadcrumb navigation
- ? Responsive tables with overflow handling
- ? Modal dialogs for confirmations and forms

**User Story**: *As a user, I want a clean, intuitive interface so that I can navigate the system without extensive training.*

---

### **13. API-First Architecture**

#### **RESTful API**
- ? Comprehensive REST API for all operations
- ? Standard HTTP methods (GET, POST, PUT, DELETE, PATCH)
- ? Proper HTTP status codes (200, 201, 400, 401, 403, 404, 409)
- ? JSON request/response format
- ? Versioning support (future enhancement)
- ? CORS support for cross-origin requests

**User Story**: *As a developer, I want a well-documented API so that I can build integrations with other systems.*

#### **API Documentation**
- ? OpenAPI/Swagger support (built-in)
- ? Interactive API explorer
- ? Endpoint descriptions and parameters
- ? Request/response examples

**User Story**: *As a developer, I want API documentation so that I can understand how to use the API without reverse-engineering.*

---

### **14. Scalability & Performance**

#### **Performance Features**
- ? Database connection pooling
- ? Connection retry logic (3 retries with 5-second delay)
- ? Pagination for large datasets
- ? Async/await throughout
- ? Efficient query design with indexes
- ? Minimal data transfer (select only needed fields)
- ? Logging for performance monitoring

**User Story**: *As a system owner, I want a scalable system so that it can handle growing user and asset counts.*

---

### **15. Compliance & Audit**

#### **Compliance Features**
- ? Comprehensive audit logs
- ? User action attribution
- ? Data retention policies
- ? Access logging
- ? Change tracking with before/after values
- ? IP address logging for security
- ? Correlation IDs for request tracing

**User Story**: *As a compliance officer, I want comprehensive audit logs so that I can demonstrate regulatory compliance.*

---

### **Future Enhancements**

The following features are identified for future implementation:

1. **Asset Import/Export**
   - Bulk CSV/Excel import for asset creation
   - Excel export of asset lists and reports
   - Data format validation during import

2. **Advanced Scheduling**
   - Scheduled maintenance reminders
   - Scheduled report generation and email delivery
   - Recurring task automation

3. **Enhanced Notifications**
   - SMS alerts for critical items
   - Slack/Teams integration
   - Custom alert rules

4. **Mobile App**
   - iOS/Android native apps
   - Offline capability
   - Barcode/QR code scanning for asset identification

5. **Advanced Reporting**
   - Custom report builder
   - Dashboard customization per user
   - Data export to Power BI/Tableau
   - Scheduled report delivery

6. **GIS Integration**
   - Asset location mapping
   - Geographic asset distribution visualization
   - Route optimization for asset tracking

7. **Integration APIs**
   - SAP/Oracle ERP integration
   - Finance system sync
   - LDAP/Active Directory user sync
   - Third-party inventory systems

8. **Multi-Tenancy**
   - Support for multiple organizations
   - Organization-level data isolation
   - Shared infrastructure with separate databases

9. **Machine Learning**
   - Predictive maintenance recommendations
   - Anomaly detection for asset usage
   - Asset lifecycle prediction

10. **Mobile Barcode Scanning**
    - QR code generation for assets
    - Mobile app for quick asset lookup
    - Inventory audit via scanning

---

### **Technology Stack Summary**

| Component | Technology | Version |
|-----------|-----------|---------|
| **Backend** | ASP.NET Core | 9.0 |
| **Language** | C# | 13.0 |
| **Frontend (Web)** | Razor Pages | .NET 9 |
| **Frontend (Mobile)** | .NET MAUI | .NET 9 |
| **Mobile Platforms** | iOS, Android, Windows, macOS Catalyst | .NET 9 |
| **Database** | SQL Server | 2019+ |
| **ORM** | Entity Framework Core | 9.0 |
| **Authentication** | JWT + ASP.NET Identity | Built-in |
| **UI Framework (Web)** | Bootstrap | 5.x |
| **Charts** | Chart.js | 4.x |
| **AI Integration** | Groq API | mixtral-8x7b |
| **ID Generation** | ULID | 1.7.x |
| **Email** | SMTP | Standard |
| **Logging** | ILogger | Built-in |

---

### **Getting Started**

For detailed setup and configuration instructions, please refer to the complementary documentation or contact the development team. The README above provides comprehensive information about:

- **System Architecture**: How components interact
- **Database Schema**: Data structure and relationships
- **API Endpoints**: Complete REST API reference
- **Authentication Flow**: Security and token management
- **Entity Models**: C# class definitions
- **Features & User Stories**: Functionality overview

---

**Generated**: December 2024  
**Version**: 1.0  
**Status**: Production Ready

For questions or contributions, please refer to the project repository or contact the development team.
