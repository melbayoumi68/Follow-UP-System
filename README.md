[Uploading README.md…]()
# InvoicesERP

An **ASP.NET Core** ERP system for managing contracts, invoices, projects, cost control, and related construction/operations workflows, with a built-in web UI.

## Table of Contents

1. [Tech Stack](#tech-stack)
2. [Project Structure](#project-structure)
3. [Requirements](#requirements)
4. [Getting Started](#getting-started)
5. [Main Modules](#main-modules)
6. [Permissions](#permissions)
7. [API Endpoints](#api-endpoints)
8. [Example: Contract Supply Items](#example-contract-supply-items)
9. [Database Overview](#database-overview)
10. [Development](#development)
11. [Security Checklist (Production)](#security-checklist-production)
12. [Contributing](#contributing)
13. [License](#license)

---

## Tech Stack

| Technology | Details |
|------------|---------|
| .NET | `net10.0` |
| ASP.NET Core | MVC + API Controllers |
| Entity Framework Core | 10.x with SQL Server |
| API Docs | Swashbuckle (Swagger / OpenAPI) |
| Frontend | Razor Views + Vanilla JavaScript |
| Auth | Custom Bearer token + module/action permissions |

---

## Project Structure

```
InvoicesErp/
├── Auth/                 # Authentication models & permission attributes
├── Common/               # Shared helpers (paging, etc.)
├── Controllers/          # API controllers
├── Data/                 # AppDbContext
├── DTOs/                 # Data transfer objects
├── Interfaces/           # Service contracts (SOLID)
├── Middleware/           # Auth & exception handling
├── Migrations/           # EF Core migrations
├── Models/               # Database entities
├── Services/             # Business logic
├── Views/                # Razor UI
├── wwwroot/              # Static CSS / JS
├── Program.cs
└── appsettings*.json
```

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (any recent version supporting `nvarchar`, `decimal`, `date`)
- Optional: Visual Studio 2022, VS Code, or Rider

---

## Getting Started

### 1. Clone the repository

```bash
git clone <repo-url>
cd InvoicesErp
```

### 2. Configure the connection string and secrets

Edit `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=InvoicesERP;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Auth": {
    "SigningKey": "Change-This-To-A-Strong-Secret-In-Production",
    "TokenHours": 12
  },
  "Cors": {
    "Origins": [
      "http://localhost:5173",
      "http://localhost:3000",
      "http://localhost:5080"
    ]
  }
}
```

> **Security:** Do not commit real SQL passwords or signing keys. Use [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) or environment variables in production.

### 3. Apply database migrations

```bash
dotnet ef database update
```

On first run, the app also:

- checks database connectivity,
- ensures the users table exists,
- seeds an admin user if the table is empty.

Some optional columns/tables are created via idempotent SQL for backward compatibility with older databases.

### 4. Run the application

```bash
dotnet restore
dotnet run
```

| Resource | URL |
|----------|-----|
| Web UI | `http://localhost:<port>/` |
| Swagger (Development only) | `http://localhost:<port>/swagger` |

---

## Main Modules

| Module | Description |
|--------|-------------|
| **Dashboard** | KPIs and charts |
| **Owners** | Clients / contracting parties |
| **Projects** | Projects linked to owners and sectors |
| **Contracts** | Contracts, payment values, and change logs |
| **Supply Items** | Material supply lines per contract |
| **Invoices** | Invoices, line items, and deductions |
| **Exec Position** | Execution position snapshots |
| **Cost Control** | Cost centers (study vs. actual sub-items) |
| **Social Insurance** | Contract insurance data and payments |
| **Escalations** | Escalations on invoice deductions |
| **Reports** | Reporting views |
| **Accounts** | Users and permissions |
| **Sectors / Sector Managers** | Organizational sectors and their managers |

---

## Permissions

Access is controlled by **Modules × Actions**.

**Modules:**
`dashboard` · `owners` · `projects` · `contracts` · `invoices` · `execPosition` · `escalations` · `socialInsurance` · `reports` · `importExport` · `accounts` · `costControl`

**Actions:**
`view` · `create` · `edit` · `delete` · `export` · `manage` · `approve`

Usage example:

```csharp
[RequirePermission(PermissionModules.Contracts, PermissionActions.View)]
```

Authenticated requests must include:

```http
Authorization: Bearer <token>
```

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Login |
| GET | `/api/auth/me` | Current user |
| GET / POST / PUT / DELETE | `/api/auth/users` | User management |
| GET / PUT / POST | `/api/bootstrap` | Bulk load / save |
| GET / POST / PUT / DELETE | `/api/contracts` | Contracts |
| GET / POST / PUT / DELETE | `/api/contracts/{id}/supply-items` | Contract supply items |
| GET | `/api/contracts/{id}/supply-items/summary` | Supply summary |
| GET / POST / PUT / DELETE | `/api/invoices` | Invoices |
| GET / POST / PUT / DELETE | `/api/execution-positions` | Execution positions |
| GET / POST / DELETE | `/api/cost-control` | Cost control |
| GET / POST / PUT / DELETE | `/api/projects` | Projects |
| GET / POST / PUT / DELETE | `/api/sectors` | Sectors |
| GET / POST / PUT / DELETE | `/api/sector-managers` | Sector managers |
| GET | `/api/health` | Health check |

Full request/response schemas are available in Swagger when running in Development.

---

## Example: Contract Supply Items

### Create a supply item

```http
POST /api/contracts/{contractId}/supply-items
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "description": "Portland cement",
  "unit": "bag",
  "qty": 100,
  "unitPrice": 250.50,
  "supplierName": "Supply Co.",
  "supplyDate": "2026-10-15",
  "note": "Phase 1 delivery"
}
```

**Rules:**

- Do **not** send `total`. The backend calculates it as `qty × unitPrice`.
- `qty` must be greater than zero.
- `unitPrice` must not be negative.

### List supply items

```http
GET /api/contracts/{contractId}/supply-items
```

### Get supply summary

```http
GET /api/contracts/{contractId}/supply-items/summary
```

---

## Database Overview

Core relationships:

- `Owners` → `Projects` → `Contracts`
- `Contracts` → `Invoices` / `ExecPositions` / `CostControl` / `ContractSupplyItems` / Social Insurance
- `Invoices` → `InvoiceItems` / `InvoiceDeductions`
- `CostControl` → `CostControlItems` → `CostControlSubItems`
- `AppUsers`: users and permissions (stored as JSON)
- `Sectors` / `SectorManagers`

---

## Development

### Common commands

```bash
dotnet restore
dotnet build
dotnet run
dotnet watch run
```

### EF Core migrations

```bash
dotnet ef migrations add <Name>
dotnet ef database update
```

### Architecture notes

- Services depend on interfaces (Dependency Inversion Principle).
- Custom middleware handles authentication and unhandled exceptions.
- CORS is configured to support optional separate frontends during transition.
- The same controllers serve both the JSON API (`/api/...`) and the MVC UI.

---

## Security Checklist (Production)

1. Replace `Auth:SigningKey` with a strong random secret.
2. Never commit real DB passwords or keys; use secrets or environment variables.
3. Enforce HTTPS.
4. Review the default seeded admin credentials after the first run.
5. Check for vulnerable packages: `dotnet list package --vulnerable`.

---

## Contributing

1. Branch from `main` (or the team's default branch).
2. Make your changes and ensure `dotnet build` succeeds.
3. Open a Pull Request with a clear description.

---

## License

Internal / team use unless a public license is added. Add an appropriate license (e.g. MIT or proprietary) before any public release.

---

**InvoicesERP**: contracts, invoices, and projects in one place.
