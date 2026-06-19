# Api Vapp — Customer Club & Marketing Automation API

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![SQL Server](https://img.shields.io/badge/Database-SQL%20Server-CC2927?logo=microsoft-sql-server&logoColor=white)](https://www.microsoft.com/sql-server)

A production-ready **ASP.NET Core 8** REST API for customer club management, SMS marketing, digital wallet operations, cashback programs, and automated messaging workflows.

Built with a clean **layered architecture** (Controllers → Services → Repositories) and designed for Persian-language business applications.

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Overview](#api-overview)
- [Background Services](#background-services)
- [Project Structure](#project-structure)
- [Development](#development)
- [Security Notes](#security-notes)
- [License](#license)

---

## Features

### Authentication & Users
- OTP-based registration and login
- JWT access tokens with refresh token rotation
- Token blacklist for secure logout
- Role-based access control (RBAC)
- User profile management and notification settings

### Contacts & CRM
- Contact notebooks and tagging
- Special occasions (birthdays, anniversaries, etc.)
- Quick actions for fast SMS workflows
- Contact cashback balance tracking

### Messaging & Automation
- Single, bulk, and array SMS sending
- Message templates and template groups
- Campaign scheduling
- Automated message rules with background execution
- Delivery status and inbox integration

### Wallet & Payments
- Digital wallet with balance and transaction history
- Wallet top-up via payment gateways
- **ZarinPal** and **Behpardakht (Mellat)** integration
- Cashback programs with scheduled payouts

### Platform Capabilities
- Swagger / OpenAPI documentation with JWT support
- Global exception handling with standardized API responses
- Large file uploads (up to 2 GB)
- In-memory caching (Redis-ready configuration)
- Rate limiting policies (configurable)
- Soft delete across entities
- Persian validation error messages

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8 |
| ORM | Entity Framework Core 9 |
| Database | SQL Server |
| Auth | JWT Bearer + BCrypt |
| API Docs | Swashbuckle (Swagger) |
| Background Jobs | `IHostedService` workers |
| Payments | ZarinPal, Behpardakht |
| SMS | Iran Novin SMS |
| Excel Export | ClosedXML |
| Scheduling | Hangfire (referenced) |

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Controllers (API)                    │
├─────────────────────────────────────────────────────────┤
│              Services (Business Logic)                  │
├─────────────────────────────────────────────────────────┤
│           Repositories (Data Access Layer)              │
├─────────────────────────────────────────────────────────┤
│         EF Core DbContext  →  SQL Server                │
└─────────────────────────────────────────────────────────┘
```

**Design principles:**
- Dependency injection throughout
- Controllers orchestrate requests only — no business logic
- Async/await for all I/O operations
- Consistent `ApiResponse<T>` envelope for all endpoints
- Soft delete via `IsDeleted` flag on entities

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) (LocalDB, Express, or full instance)
- [EF Core CLI tools](https://learn.microsoft.com/ef/core/cli/dotnet) (for migrations)

```bash
dotnet tool install --global dotnet-ef
```

### 1. Clone the repository

```bash
git clone https://github.com/seyedWebpro/Api_Vapp_Manually.git
cd Api_Vapp_Manually
```

### 2. Configure settings

Copy the example configuration and fill in your values:

```bash
cp appsettings.Example.json appsettings.json
```

Or use [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for local development:

```bash
dotnet user-secrets init
dotnet user-secrets set "defultConnection" "Data Source=.;Initial Catalog=DbVappLocal;Integrated Security=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Secret" "YourSecretKey_AtLeast32CharactersLong"
```

### 3. Apply database migrations

```bash
dotnet ef database update
```

### 4. Run the API

```bash
dotnet run
```

The API starts at **http://localhost:5054** (HTTP) by default.  
Open Swagger UI at: **http://localhost:5054/swagger**

---

## Configuration

Key sections in `appsettings.json`:

| Section | Description |
|---------|-------------|
| `localConnection` / `defultConnection` | SQL Server connection strings |
| `Jwt` | Secret, issuer, audience, token lifetimes |
| `Development` | `DisableAuth`, `DisableWalletCheck` flags |
| `FileUpload` | Max size, allowed MIME types, upload folder |
| `Payment` / `ZarinPal` | Payment gateway credentials |
| `Sms` | SMS provider API key and sender number |
| `Cache` | Memory or Redis cache profiles |
| `RateLimit` | Per-endpoint rate limiting policies |

See [`appsettings.Example.json`](appsettings.Example.json) for a full template with placeholder values.

---

## API Overview

| Controller | Base Route | Description |
|------------|------------|-------------|
| `Auth` | `/api/Auth` | OTP login, registration, token refresh |
| `User` | `/api/User` | User CRUD, profile, notification settings |
| `Role` | `/api/Role` | Role management |
| `UserRole` | `/api/UserRole` | User–role assignments |
| `Contact` | `/api/Contact` | Contact management |
| `ContactNotebook` | `/api/ContactNotebook` | Contact notebooks |
| `Message` | `/api/Message` | Messages and campaigns |
| `Template` | `/api/Template` | SMS templates and groups |
| `Sms` | `/api/Sms` | Direct SMS operations |
| `AutomatedMessage` | `/api/AutomatedMessage` | Automation rules |
| `SpecialOccasion` | `/api/SpecialOccasion` | Occasion-based triggers |
| `QuickAction` | `/api/QuickAction` | Quick-send shortcuts |
| `Wallet` | `/api/Wallet` | Balance, transactions, top-up |
| `Payment` | `/api/Payment` | Payment gateways and callbacks |
| `Cashback` | `/api/Cashback` | Cashback programs |

All protected endpoints require a Bearer token:

```
Authorization: Bearer <your-jwt-token>
```

Obtain a token via `POST /api/Auth/login` after OTP verification.

---

## Background Services

Four hosted background workers run automatically when the API starts:

| Service | Purpose |
|---------|---------|
| `AutomatedMessageBackgroundService` | Executes automated messaging rules |
| `ScheduledCampaignBackgroundService` | Dispatches scheduled campaigns |
| `ScheduledMessageBackgroundService` | Sends time-scheduled messages |
| `ScheduledCashbackBackgroundService` | Processes pending cashback payouts |

---

## Project Structure

```
Api_Vapp_Manually/
├── Controller/          # API endpoints
├── Services/            # Business logic
│   └── BackgroundServices/
├── Repositories/        # Data access
├── Models/              # EF Core entities
├── DTOs/                # Request/response models
├── Data/                # DbContext
├── Interfaces/          # Service & repository contracts
├── Middleware/          # Global exception handler
├── Filters/             # Swagger filters
├── Migrations/          # EF Core migrations
├── Utilities/           # Helpers and extensions
├── wwwroot/uploads/     # Uploaded files
├── Program.cs           # App bootstrap & DI
└── appsettings.json     # Configuration
```

---

## Development

### Coding standards

See [`RoleCodingManually_Vapp.txt`](RoleCodingManually_Vapp.txt) for project-specific conventions (Persian error messages, naming, response format).

### Standard API response

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": { },
  "errors": null
}
```

### Run with hot reload

```bash
dotnet watch run
```

### Create a new migration

```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

---

## Security Notes

> **Important:** Never commit real credentials to source control.

- Rotate any secrets that were previously committed
- Use environment variables or User Secrets in development
- Set `Development:DisableAuth` to `false` before deploying to production
- Configure CORS with specific origins in production (currently `AllowAnyOrigin`)
- Review rate limiting settings before going live

Report security issues privately — see [SECURITY.md](SECURITY.md).

---

## License

This project is licensed under the [MIT License](LICENSE).

Copyright (c) 2024 Vapp Customer Club
