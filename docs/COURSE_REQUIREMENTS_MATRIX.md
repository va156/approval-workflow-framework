# Course Requirements Matrix

## Score Mapping

1. Database (10): EF Core persistence with SQL Server adapter in `src/Workflow.Persistence.EFCore`.
2. Asynchrony (5): async repository/engine/API methods across all runtime flows.
3. Synchronization primitives (15):
   - `SemaphoreSlim` for controlled parallel dynamic-assignee recalculation.
   - `lock` for per-instance transition safety.
   - `ConcurrentDictionary` for condition and instance lock caches.
4. Design patterns (30):
   - Strategy-like behavior via policies (`BlockCompletionPolicy`, `ReworkPolicy`).
   - Factory-style provider wiring for identity/group implementations.
   - State transitions via `StepStatus` and process state machine.
   - Repository pattern for definition/runtime access.
5. Work scope (30): engine + persistence + APIs + admin + demo + tests.
6. In-time defense (10): scope constrained as MVP and ready for extension.

## Required Functional Coverage

- Configurable process definitions with versioning.
- Step and block support with rework behavior.
- User/group/dynamic-field approver assignment.
- Group task claim flow.
- Dynamic group reassignment for non-finalized steps.
- Working-day deadlines.
- Step statuses and custom definition-level status catalog.
- Notification template bindings and runtime notifications.
- Preview of route before request save.
