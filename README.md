# ApexPost MVC Branch Operations Platform

A .NET 10 MVC-first branch operations application for ApexPost. The solution is intentionally structured around service-layer business logic, JSON-backed persistence, and a secure MVC front end. The active app is `ResourceManagementSystem.MVC`; the root `ResourceManagementSystem` project remains a legacy reference implementation rather than the current runtime path.

## Solution structure

- `ResourceManagementSystem.MVC` - active MVC app, controllers, Razor views, auth, routing, middleware, and filters
- `ResourceManagementSystem.Services` - domain/business logic and validation rules
- `ResourceManagementSystem.Database` - persistence models and JSON seed data
- `ResourceManagementSystem.Models` - DTOs, view models, and value objects
- `ResourceManagementSystem.Repositories` - generic JSON repository abstraction
- `ResourceManagementSystem.Utilities` - audit, middleware, security helpers, and cross-cutting behavior
- `ResourceManagementSystem.Tests` - xUnit and integration tests
- `ResourceManagementSystem` - legacy console/reference project, not the active application

## Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or any IDE that supports .NET 10
- Optional: Postman or curl for sample integration requests

## Run the app

From the repository root:

```powershell
dotnet restore .\ResourceManagementSystem.slnx
dotnet run --project .\ResourceManagementSystem.MVC\ResourceManagementSystem.MVC.csproj
```

To watch for file changes during local development:

```powershell
dotnet watch --project .\ResourceManagementSystem.MVC\ResourceManagementSystem.MVC.csproj run
```

Open the browser at the local ASP.NET Core URL shown in the console, usually:

- https://localhost:5001
- http://localhost:5000

## Architecture principle

The active implementation follows this rule:

- Controller -> Service -> Repository
- No controller should call repositories directly
- Business rules remain in the service layer
- Persistence models, seed JSON, and audit examples stay in the Database project

## Demo login accounts

These demo accounts are available from the default seed data in `ResourceManagementSystem.Database\JSONs\StaffEntity.json`.

| Email | Password | Role | Home branch | Notes |
| --- | --- | --- | --- | --- |
| `admin@apexpost.local` | `ApexPost123!` | Administrator | `BR-001` | System administrator and full access |
| `north.manager@apexpost.local` | `ApexPost123!` | BranchManager | `BR-001` | Branch manager for North operations |
| `midtown.manager@apexpost.local` | `ApexPost123!` | BranchManager | `BR-003` | Regional manager example |
| `harbor.clerk@apexpost.local` | `ApexPost123!` | CounterClerk | `BR-002` | Front counter operations |
| `riverside.auditor@apexpost.local` | `ApexPost123!` | Auditor | `BR-004` | Audit and reporting access |

The password is intentionally simple for assignment/demo purposes and is read from `Authentication:DemoPassword` in app settings.

## API key configuration with user-secrets

Protected integration endpoints use the `x-api-key` header. To set the application key locally without editing source files:

```powershell
dotnet user-secrets init --project .\ResourceManagementSystem.MVC\ResourceManagementSystem.MVC.csproj
dotnet user-secrets set "Integration:ApiKey" "ApexPost-Partner-Key-2026" --project .\ResourceManagementSystem.MVC\ResourceManagementSystem.MVC.csproj
```

The middleware reads `Integration:ApiKey` and validates it against the incoming request header. The project also includes a default value in `appsettings.json` for local demo use.

## Implemented features

- MVC-first branch operations dashboard and navigation
- Branch, catalog, shipping, money-order, transaction, and reporting views
- Service-layer validation for stock sufficiency, shipping pricing, and money-order verification
- Authorization policies for branch manager access and same-branch access
- Cookie-based authentication for MVC staff accounts
- API-key validation for protected integration endpoints
- Correlation ID and request logging middleware
- Security headers, IP restriction, maintenance mode, and exception handling
- Audit filter and JSON-backed audit persistence
- Seed data for branches, staff, products, stock, shipping orders, and transactions

## Seed data and sample artifacts

The solution ships with realistic assignment data in `ResourceManagementSystem.Database\JSONs`:

- `BranchEntity.json` - 5 branch records
- `StaffEntity.json` - 20 staff records across roles
- `ProductEntity.json` - 30 catalog products
- `BranchStockEntity.json` - branch stock snapshots
- `ShippingOrderEntity.json` - historical shipping samples
- `TransactionEntity.json` - 40 historical transactions
- `MoneyOrderEntity.json` - sample money-order activity
- `IdentityVerificationEntity.json` - verification records
- `audit-log.json` - generated runtime audit store
- `AUDIT-SAMPLE.json` - representative sample audit entries for documentation and demos

## Sample requests

A ready-to-import Postman collection is included at the repository root as `ApexPost-Postman-Collection.json`.

Typical curl examples:

```powershell
# Login (MVC form post)
curl -X POST "https://localhost:5001/Account/Login" -H "Content-Type: application/x-www-form-urlencoded" -d "Email=admin@apexpost.local&Password=ApexPost123!"

# Protected integration call using API key
curl -X GET "https://localhost:5001/Integration/Partner" -H "x-api-key: ApexPost-Partner-Key-2026"

# Health check
curl https://localhost:5001/health
```

The Postman collection contains examples for login, dashboard access, catalog retrieval, reporting, and integration requests.

## Testing

Run the automated suite:

```powershell
dotnet test .\ResourceManagementSystem.Tests\ResourceManagementSystem.Tests.csproj
```

The tests cover:

- stock sufficiency logic
- money-order verification rules
- shipping price and parcel limit checks
- MVC security flows and middleware behavior
- login and authorization behavior
- request logging and audit output

## Notes

This project is intentionally structured for training/assignment use with JSON persistence, service-layer rules, and MVC UI patterns. It is not a production credential store; the demo password and API key are intentionally simple and are meant to demonstrate authentication and integration flow in a local environment.
