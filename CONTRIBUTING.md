# Contributing to Api Vapp

Thank you for your interest in contributing! This document outlines the workflow and standards for this project.

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Create a feature branch from `main`
4. Copy `appsettings.Example.json` to `appsettings.json` and configure your local environment
5. Run migrations: `dotnet ef database update`
6. Make your changes and test locally

## Branch Naming

| Prefix | Use case |
|--------|----------|
| `feature/` | New features |
| `fix/` | Bug fixes |
| `refactor/` | Code restructuring |
| `docs/` | Documentation only |

Example: `feature/add-redis-cache`

## Coding Standards

Follow the conventions in [`RoleCodingManually_Vapp.txt`](RoleCodingManually_Vapp.txt):

- **Architecture:** Controllers → Services → Repositories
- **Naming:** `IProductService`, `GetByIdAsync`, `ProductRepository`
- **Models:** Every entity must have `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`
- **Validation:** Use Persian error messages in DTOs
- **Responses:** Always return `ApiResponse<T>`
- **Async:** All database and I/O operations must be async

## Commit Messages

Write clear, imperative commit messages:

```
Add wallet transaction export endpoint

Fix OTP expiration validation in AuthService

Update README with deployment instructions
```

## Pull Request Checklist

- [ ] Code builds without errors (`dotnet build`)
- [ ] New endpoints are documented in Swagger XML comments
- [ ] No secrets or credentials committed
- [ ] Follows existing naming and folder conventions
- [ ] Persian validation messages for user-facing errors

## Reporting Issues

Use the [Bug Report](.github/ISSUE_TEMPLATE/bug_report.yml) or [Feature Request](.github/ISSUE_TEMPLATE/feature_request.yml) templates on GitHub.

## Questions

Open a GitHub Issue with the `question` label if you need clarification before starting work.
