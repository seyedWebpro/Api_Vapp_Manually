# Vapp API Development Guidance

These instructions apply to the entire `Api_Vapp_Manually` repository.

## Safety and workflow

- Inspect `git status` before editing and preserve unrelated user changes.
- Keep changes focused on the requested behavior; do not perform broad rewrites without an explicit request.
- Never commit secrets, tokens, `.env` values, production credentials, logs, generated build output, or uploaded user files.
- Ask before adding production dependencies, changing the database schema, applying migrations to a shared database, deploying, or rotating credentials.
- For review or diagnosis requests, do not implement changes unless the user asks for a fix.
- When implementation is requested, edit the files directly, verify the result, and report changed files, commands, failures, and remaining risks.

## Architecture

- Controllers inherit from `VappControllerBase` and only orchestrate HTTP concerns.
- Business logic belongs in `Services/`; data access belongs in `Repositories/`.
- Use dependency injection and existing interfaces. Interface names start with `I`; asynchronous methods end with `Async`.
- Entities normally include `Id`, `CreatedAt`, `UpdatedAt`, and `IsDeleted`; preserve existing model conventions.
- Use `decimal` for money and an appropriate fixed precision such as `decimal(5,2)` for percentages.
- Use soft delete and filter out `IsDeleted` records unless deleted data is explicitly required.
- Prefer `AsNoTracking()` and projection for read-only EF Core queries, and paginate large result sets.
- Use transactions for multi-step financial operations or coordinated changes across multiple tables.

## Validation and safe errors

- DTO validation messages shown to users must be controlled Persian text.
- Use `ExtractModelStateErrors()` from `VappControllerBase`; do not expose raw ModelState exception messages.
- Use the existing `ApiResponse<T>` helpers and constants from `DTOs/Common/ErrorCodes.cs`.
- Use `AppException` for predictable domain failures and `ControlledErrorHelper` for safe user-facing errors.
- Never return `ex.Message`, inner exceptions, stack traces, SQL errors, server paths, or raw third-party responses to clients.
- Unexpected exceptions must be logged with structured logging and returned as a controlled generic response.
- Do not add controller-level `try/catch` merely to duplicate `GlobalExceptionHandlerMiddleware`.
- Preserve `ApiTraceIdResultFilter` behavior so API responses remain traceable.

Before changing error behavior, inspect:

- `Utilities/ControlledErrorHelper.cs`
- `DTOs/Common/ErrorCodes.cs`
- `Exceptions/AppException.cs`
- `Middleware/GlobalExceptionHandlerMiddleware.cs`
- `Filters/ApiTraceIdResultFilter.cs`
- `Controller/VappControllerBase.cs`

## Controllers and API contracts

- Protected endpoints use the established authorization policy and verify resource ownership.
- Obtain the current user through `GetCurrentUserIdAsync()` where applicable.
- Return the standard `ApiResponse<T>` envelope except for an established, documented exception.
- Follow the existing HTTP convention: create/update/delete actions generally use POST and reads use GET.
- Default pagination is page 1 with page size 20; cap page size at 100 unless an existing endpoint specifies otherwise.
- Frontends should display the controlled backend `message`, use `errorCode` for UI behavior, and retain `traceId` for support.

## Logging, caching, and files

- Use structured Serilog templates rather than string interpolation for structured fields.
- Cache read operations only when safe. Never cache balances or sensitive validation results.
- Invalidate related cache entries after create, update, or delete operations.
- Generate controlled server-side upload names, validate file type and size, and return only safe relative paths.

## Verification

- Build the affected projects and run relevant automated tests after code changes.
- After adding or changing an endpoint, test affected endpoints with real HTTP requests when the API and dependencies are available.
- Cover the happy path plus relevant validation, invalid input, missing/invalid authentication, not-found, and forbidden-ownership scenarios.
- Verify HTTP status and the `success`, `errorCode`, `message`, and `traceId` fields.
- If live prerequisites are unavailable, report skipped verification accurately; never fabricate passing results.
- Never put real JWTs or credentials in committed `.http` examples.

## Definition of done

- Requested behavior is implemented within scope.
- Existing unrelated changes remain intact.
- Relevant formatting, build, and tests pass where practical.
- User-facing errors reveal no technical internals or secrets.
- API contracts and dependent clients remain aligned.
- The final report separates verified results from assumptions and skipped checks.
