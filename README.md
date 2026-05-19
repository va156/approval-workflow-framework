# Approval Workflow Framework

Configurable C# approval engine and integration framework for business requests (vacation, procurement, legal approvals, and similar flows).  
The project is intentionally system-agnostic: business systems provide request data, while the engine manages definitions, runtime execution, deadlines, statuses, and audit history.

## Contents

- [Architecture and design docs](#architecture-and-design-docs)
- [Repository structure](#repository-structure)
- [Implemented MVP capabilities](#implemented-mvp-capabilities)
- [Quick start](#quick-start)
- [Connection string](#connection-string)
- [Core API](#core-api)
- [Testing](#testing)
- [Course mapping](#course-mapping)

## Architecture and design docs

- [Architecture overview](docs/ARCHITECTURE.md)
- [Database schema](docs/DATABASE_SCHEMA.md)
- [API usage guide](docs/API_GUIDE.md)
- [Dashboard guide](docs/DASHBOARD_GUIDE.md)
- [Course requirements matrix](docs/COURSE_REQUIREMENTS_MATRIX.md)

## Repository structure

- `src/Workflow.Abstractions` - contracts, DTOs, domain models, and definition validation.
- `src/Workflow.Engine` - workflow runtime orchestration, preview/start/action behavior, synchronization primitives.
- `src/Workflow.Persistence.EFCore` - SQL Server EF Core adapters and repositories.
- `src/Workflow.Framework` - one-line host integration, auto-migration, and embedded dashboard.
- `src/Workflow.Api` - REST API for process definitions, custom statuses, preview/start, and step actions.
- `src/Workflow.Admin` - lightweight admin surface for definition operations and preview.
- `src/Workflow.Integration.VacationDemo` - demo integration scenario for vacation requests.
- `tests/Workflow.Engine.Tests` - engine behavior tests.

## Implemented MVP capabilities

- Versioned process definitions with publish flow (`Draft -> Published -> Archived`).
- One-line framework integration with auto-migration and embedded dashboard.
- `Step` and `Block` nodes with completion/rework policy support.
- Dynamic assignees (`User`, `Group`, `DynamicField`).
- Group claim flow (`Unclaimed -> InProgress`).
- Preview before save (`/process-instances/preview`).
- Working-day deadline calculation (weekends excluded).
- Notification template binding model.
- Definition-level custom statuses and per-step semantic status bindings.
- Runtime status history with technical and custom status values.

## Quick start

1. Build solution:

```bash
dotnet build Workflow.sln
```

2. Ensure SQL Server is accessible (example uses LocalDB):

```text
(localdb)\MSSQLLocalDB
```

3. Apply migrations:

```bash
dotnet tool update --global dotnet-ef
dotnet ef database update --project .\src\Workflow.Persistence.EFCore --startup-project .\src\Workflow.Api
```

4. Run API:

```bash
dotnet run --project .\src\Workflow.Api
```

5. Open Swagger:

```text
https://localhost:5001/swagger
```

6. Open built-in dashboard:

```text
https://localhost:5001/workflow-dashboard
```

Dashboard includes:

- side menu sections (`Overview`, `Definitions`, `Instances`, `Failed`, `Retry`),
- search and state filters,
- direct step actions (`Claim`, `Approve`, `Rework`),
- retry operation for failed/rework instances.

## Connection string

Connection string key: `WorkflowPrimary`

Default local value:

```text
Server=(localdb)\MSSQLLocalDB;Database=WorkflowEngine;Trusted_Connection=True;TrustServerCertificate=True
```

Configured in:

- `src/Workflow.Api/appsettings.json`
- `src/Workflow.Admin/appsettings.json`

## Core API

Definitions:

- `POST /process-definitions`
- `PUT /process-definitions/{id}`
- `POST /process-definitions/{id}/publish`
- `GET /process-definitions/{id}/versions/{version}`

Definition custom statuses:

- `GET /process-definitions/{id}/statuses`
- `POST /process-definitions/{id}/statuses`
- `PUT /process-definitions/{id}/statuses/{statusKey}`
- `DELETE /process-definitions/{id}/statuses/{statusKey}`

Runtime:

- `POST /process-instances/preview`
- `POST /process-instances/start`
- `POST /step-instances/{id}/claim`
- `POST /step-instances/{id}/approve`
- `POST /step-instances/{id}/reject`
- `POST /step-instances/{id}/rework`

Detailed request/response examples are available in [API guide](docs/API_GUIDE.md).

## Library-style integration (Hangfire-like)

In host application:

```csharp
builder.Services.AddWorkflowFramework(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("WorkflowPrimary")!;
    options.DashboardPath = "/workflow-dashboard";
    options.AutoMigrateDatabase = true;
});
```

At runtime startup:

```csharp
app.UseWorkflowFrameworkDashboard();
```

This setup auto-creates/updates the workflow database schema and exposes the dashboard route.

Optional simple Basic auth:

```json
"WorkflowFramework": {
  "DashboardAuthEnabled": true,
  "DashboardUsername": "admin",
  "DashboardPassword": "change-me"
}
```

## Testing

```bash
dotnet test Workflow.sln -p:RollForward=Major
```

## Course mapping

Scoring evidence and criterion-to-implementation mapping are documented in:

- [Course requirements matrix](docs/COURSE_REQUIREMENTS_MATRIX.md)
