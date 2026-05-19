# Workflow Engine

Configurable approval workflow engine for .NET with process versioning, blocks, dynamic assignees, preview-before-save, and definition-level custom statuses.

## Projects

- `src/Workflow.Abstractions` - shared contracts, DTOs, domain models, and definition validator.
- `src/Workflow.Engine` - runtime engine, preview/start/action flow, synchronization primitives.
- `src/Workflow.Persistence.EFCore` - SQL Server EF Core storage adapters.
- `src/Workflow.Api` - REST API for definitions, status CRUD, preview, start, and step actions.
- `src/Workflow.Admin` - lightweight admin API/UI for definition configuration and preview.
- `src/Workflow.Integration.VacationDemo` - demo integration for vacation approval scenario.
- `tests/Workflow.Engine.Tests` - integration-style engine tests.

## Main MVP Features

- Definition versioning: `Draft -> Published -> Archived`.
- Node types: `Step` and `Block` with configurable completion/rework policies.
- Dynamic approvers: user/group/dynamic field (`ApproverFrom` pattern).
- Group claim flow: group tasks start as `Unclaimed`, then `Claim`.
- Preview mode: evaluate future route before saving request.
- Working-day deadlines (weekends excluded).
- Notification bindings and template-based notifications.
- Definition-level custom status catalog and step status bindings.

## Custom Statuses (Definition-Level)

Each process definition contains status catalog entries (`WorkflowStatusDefinition`) and step bindings (`StepStatusBinding`) to semantics:

- `Pending`
- `Unclaimed`
- `InProgress`
- `Approved`
- `Rejected`
- `ReworkRequested`
- `Cancelled`
- `Expired`

Runtime and preview include both:

- technical status (`StepStatus`)
- configured custom status (`CustomStatusKey`, `CustomStatusName`)

## API Endpoints (Core)

Definition management:

- `POST /process-definitions`
- `PUT /process-definitions/{id}`
- `POST /process-definitions/{id}/publish`
- `GET /process-definitions/{id}/versions/{v}`

Definition status CRUD:

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

## Database and Connection String

Connection string name: `WorkflowPrimary`

Default local value:

`Server=(localdb)\MSSQLLocalDB;Database=WorkflowEngine;Trusted_Connection=True;TrustServerCertificate=True`

Configured in:

- `src/Workflow.Api/appsettings.json`
- `src/Workflow.Admin/appsettings.json`

## Build and Test

Build:

`dotnet build Workflow.sln`

Tests:

`dotnet test Workflow.sln -p:RollForward=Major`

## Publish to GitHub

Repository is prepared for open-source publication with MIT license.

After initializing git and setting remote repo name:

1. `git add .`
2. `git commit -m "Initial workflow engine MVP"`
3. `gh repo create <repo-name> --public --source . --remote origin --push`

If `<repo-name>` is not decided yet, choose it first and run step 3.
