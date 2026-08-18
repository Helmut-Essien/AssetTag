---
name: ci-cd
description: "Use when changing GitHub Actions deploy, Web Deploy, EF migrations in CI, Android APK signing, or repository secrets for API/Portal/mobile hosts."
---

# CI/CD

Workflow: `.github/workflows/main.yml`. Runs on push to `master` when `AssetTag/`, `Portal/`, `Shared/`, `MobileApp/`, `MobileData/`, or the workflow file change.

## Pipeline

detect-changes → get-mobile-version → build-and-publish → run-migrations → deploy API/Portal (Web Deploy) → build-android (signed APK) → release-android (GitHub Release)

Path filters skip API, Portal, or mobile jobs when those trees did not change. `Shared/` counts as both API and Portal (and mobile).

## Hard rules

- **Never hardcode** production/test hostnames or sender identity in the workflow or base `appsettings.json`. Inject at deploy time from secrets.
- `FRONTEND_BASE_URL` → API `FrontendBaseUrl` (invitation / password-reset links).
- `API_BASE_URL` → Portal `Api:BaseUrl`.
- Mobile version for tags and API `LatestVersion` comes from `MobileApp/MobileApp.csproj`: `ApplicationDisplayVersion` (name) and `ApplicationVersion` (Android versionCode, must increase).
- Migrations run in CI against production (`dotnet ef database update`). They are **not** applied on API startup.
- Android signing uses `ANDROID_KEYSTORE_BASE64`, keystore password, and key alias secrets.

## Secrets (Actions)

| Secret | Used by |
|--------|---------|
| `FRONTEND_BASE_URL` | API email links |
| `API_BASE_URL` | Portal HttpClient |
| `EMAIL_USERNAME` / `EMAIL_PASSWORD` / `EMAIL_FROM` / `EMAIL_FROM_NAME` | API SMTP |
| `JWT_SECURITY_KEY` | API JWT |
| `MONSTERASP_DATABASE_CONNECTION` | API SQL + migrations |
| `API_*` / `PORTAL_*` deploy | Web Deploy site, URL, credentials |
| `ANDROID_KEYSTORE_*` | Signed APK |

Local: `appsettings.Development.json` or user secrets. Do not commit connection strings or hosted URLs.
