# Dashboard Guide

Built-in dashboard route (default):

```text
/workflow-dashboard
```

## Sections

- `Overview`: aggregate metrics
- `Definitions`: process definitions and versions
- `Instances`: runtime instances with search and state filtering
- `Failed`: rejected/rework instances
- `Retry`: operational retry area

## Supported actions

From dashboard:

- `claim` step
- `approve` step
- `reject` step
- `rework` step
- retry failed/rework instance

## Dashboard API endpoints

- `GET /workflow-dashboard/api/stats`
- `GET /workflow-dashboard/api/definitions?search=...`
- `GET /workflow-dashboard/api/instances?search=...&state=...`
- `GET /workflow-dashboard/api/failed`
- `POST /workflow-dashboard/api/steps/{stepId}/{actionName}`
- `POST /workflow-dashboard/api/retry/{instanceId}`

## Authentication

Simple Basic auth can be enabled:

```json
"WorkflowFramework": {
  "DashboardAuthEnabled": true,
  "DashboardUsername": "admin",
  "DashboardPassword": "change-me"
}
```

For production systems, integrate host-level authentication/authorization and secure secrets storage.
