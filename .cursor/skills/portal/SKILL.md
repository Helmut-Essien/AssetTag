---
name: portal
description: "Use when working on the AssetTag Razor Pages web frontend (Portal/ project): pages, cookie session, HttpClient to the API, login/register/reset, or reports."
---

# AssetTag Portal

Razor Pages UI. It does **not** talk to SQL Server directly. All data goes through the API over HTTP.

## Hard rules

- **Api:BaseUrl** is required (`appsettings.Development.json`, user secrets, or CI `API_BASE_URL`). Startup throws if missing. Never hardcode production API hosts.
- Two HttpClients: `AuthApi` (login/refresh, no bearer handler) and `AssetTagApi` (with `TokenRefreshHandler` + `UnauthorizedRedirectHandler`).
- Cookie session (`Portal.Session`, 60 min). Production cookies are `SecurePolicy.Always`.
- Auth pages use `_AuthLayout.cshtml`; app pages use `_Layout.cshtml`.
- Invitation register and password reset are Portal routes (`/Account/Register`, `/Account/ResetPassword`) linked from API emails via `FrontendBaseUrl`.
- Culture is `en-GH` / `₵` in `Program.cs`.

## Layout

- `Portal/Pages/` — Assets, Locations, Categories, Departments, Users, Reports, Account, Diagnostics
- `Portal/Services/ApiAuthService.cs` — login, tokens, current user
- `Portal/Services/TokenRefreshHandler.cs` — cookie-backed API bearer refresh
- `Portal/Program.cs` — HttpClient registration, session, forwarded headers

## Local

API and Portal are **separate processes**. Run both:

```bash
dotnet run --project AssetTag/AssetTag.csproj --launch-profile https
dotnet run --project Portal/Portal.csproj --launch-profile https
```

Portal HTTPS `https://localhost:7207`. Compound launch: `Launch AssetTag + Portal` in `.vscode/launch.json`.
