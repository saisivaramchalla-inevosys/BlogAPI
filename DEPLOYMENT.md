# Free cloud deployment guide for CodePulse.API

This guide walks you through deploying the ASP.NET Core 8 Web API on a completely free, no-credit-card stack:

| Concern | Free service |
|--------|--------------|
| App hosting | [Render](https://render.com) Web Service (free tier) |
| Database | [Neon](https://neon.tech) serverless PostgreSQL (free tier) |
| Image storage | [Cloudinary](https://cloudinary.com) (free tier) |

## 1. Free service setup

### Neon (PostgreSQL)
1. Sign up at <https://neon.tech> (no credit card required).
2. Create a project and a database (e.g., `codepulse`).
3. Copy the **connection string** from the dashboard. It looks like:
   ```
   Host=<project-id>.us-east-1.aws.neon.tech;Database=codepulse;Username=codepulse_owner;Password=...;SSL Mode=require
   ```

### Cloudinary (image hosting)
1. Sign up at <https://cloudinary.com> (no credit card required).
2. From the dashboard, note:
   - Cloud name
   - API Key
   - API Secret

### Render (app hosting)
1. Sign up at <https://render.com> with your GitHub account (no credit card required).
2. Create a new **Web Service** from your repository.
3. Choose **Docker** runtime and point it to the `Dockerfile` in the repo root.
4. Add the environment variables listed below.

## 2. Required environment variables

Render uses double underscores for nested config (`:` becomes `__`).

| Render env var | Where it is used | How to get it |
|----------------|------------------|---------------|
| `ASPNETCORE_ENVIRONMENT` | Tells ASP.NET to use `appsettings.Production.json` | Set to `Production` |
| `ConnectionStrings__CodePulseConnectionString` | `Program.cs` → `ApplicationDbContext` & `AuthDbContext` | Neon connection string |
| `Jwt__Key` | `Program.cs` JWT signing key | Generate a long random string (≥32 chars) |
| `Jwt__Issuer` | `Program.cs` JWT issuer | e.g. `https://your-render-url.onrender.com` |
| `Jwt__Audience` | `Program.cs` JWT audience | e.g. `https://your-frontend-url.com` |
| `Cloudinary__CloudName` | `ImageRepository.cs` | Cloudinary dashboard |
| `Cloudinary__ApiKey` | `ImageRepository.cs` | Cloudinary dashboard |
| `Cloudinary__ApiSecret` | `ImageRepository.cs` | Cloudinary dashboard |
| `Cors__AllowedOrigin` | `Program.cs` CORS policy | Your deployed Angular frontend URL |

## 3. What changed and where

### `CodePulse.API/CodePulse.API.csproj`
- Removed `Microsoft.EntityFrameworkCore.SqlServer`.
- Added `Npgsql.EntityFrameworkCore.PostgreSQL` (8.0.11) for Neon/PostgreSQL.
- Added `CloudinaryDotNet` (1.27.4) for image uploads.
- Aligned `Microsoft.EntityFrameworkCore.Tools` to 8.0.22.
- Removed the `<Folder Include="Images\" />` item (images no longer stored on disk).

### `CodePulse.API/Program.cs`
- Replaced `UseSqlServer(...)` with `UseNpgsql(...)` for both `ApplicationDbContext` and `AuthDbContext`.
- Removed `using Microsoft.Extensions.FileProviders;`.
- Removed the `UseStaticFiles(...)` block that served the local `Images` folder.
- Made the CORS allowed origin configurable via `Cors:AllowedOrigin` with a localhost fallback.
- Registered `CloudinarySettings` options:
  ```csharp
  builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
  ```

### `CodePulse.API/Models/DTO/CloudinarySettings.cs` (new)
Added a small options class:
```csharp
public class CloudinarySettings
{
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}
```

### `CodePulse.API/Repositories/Implementation/ImageRepository.cs`
- Replaced local disk storage with a Cloudinary upload.
- Constructor now receives `IOptions<CloudinarySettings>`.
- `Upload(...)` streams the file to Cloudinary, persists the returned `SecureUrl`, and saves the `BlogImage` record.
- Removed `IWebHostEnvironment` and `IHttpContextAccessor` dependencies.

### `CodePulse.API/Migrations/` and `CodePulse.API/Migrations/AuthDb/`
- Deleted all old SQL Server migrations.
- Regenerated migrations for PostgreSQL:
  - `20260702101551_InitialPostgres.cs` (ApplicationDb)
  - `20260702101626_InitialAuthPostgres.cs` (AuthDb)

### `CodePulse.API/appsettings.json`
- Removed the SQL Server connection string and JWT key.
- Kept only non-secret defaults (`Logging`, `AllowedHosts`).

### `CodePulse.API/appsettings.Development.json`
- Added local-dev placeholders for PostgreSQL connection string, JWT settings, Cloudinary settings, and CORS origin.

### `CodePulse.API/appsettings.Production.json` (new)
- Contains only `Logging` and `AllowedHosts`.
- All secrets are supplied by Render environment variables.

### `Dockerfile` (new)
Multi-stage Dockerfile based on the official .NET 8 SDK/runtime images. It restores, publishes, and runs the API on port `8080`.

### `.dockerignore` (new)
Excludes build artifacts, Git files, and IDE folders from the Docker context.

### `render.yaml` (new)
Render Blueprint that defines a free Web Service, including all required environment variable keys (values are entered in the Render dashboard).

### `CodePulse.API/Controllers/AuthController.cs`
- Removed an unused `using Azure;` directive that caused a build error after removing the SQL Server package.

## 4. How to run locally

Set environment variables or update `appsettings.Development.json` with your local values, then:

```bash
dotnet ef database update -c ApplicationDbContext
dotnet ef database update -c AuthDbContext
dotnet run --project CodePulse.API/CodePulse.API.csproj
```

## 5. How to deploy

1. Push the updated branch to GitHub.
2. In Render, create a Web Service from the repo and choose **Docker**.
3. Add the environment variables from section 2.
4. Render builds the `Dockerfile` and starts the service.
5. After the first deploy, apply migrations. You can run them from the Render shell or locally against the Neon database:
   ```bash
   dotnet ef database update -c ApplicationDbContext -- "your-neon-connection-string"
   dotnet ef database update -c AuthDbContext -- "your-neon-connection-string"
   ```

## 6. Important notes

- Render free Web Services **spin down after 15 minutes of inactivity**. The first request after a period of inactivity will have a cold start (10–30 seconds).
- Neon free tier includes 0.5 GB of storage — plenty for teaching/demo loads.
- Cloudinary free tier includes 25 GB of storage/bandwidth.
- Never commit secrets. `appsettings.Production.json` contains none.
