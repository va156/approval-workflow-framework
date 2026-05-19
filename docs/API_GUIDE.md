# API Guide

Base URL example:

```text
https://localhost:5001
```

Dashboard URL:

```text
https://localhost:5001/workflow-dashboard
```

## 1. Create definition draft

`POST /process-definitions`

Example request body:

```json
{
  "processKey": "vacation_approval",
  "name": "Vacation Approval",
  "statuses": [
    { "key": "pending", "displayName": "Pending", "semantic": 0, "isDefault": true },
    { "key": "approved", "displayName": "Approved", "semantic": 3, "isDefault": true }
  ],
  "nodes": [
    {
      "type": 0,
      "name": "Manager Approval",
      "sortOrder": 1,
      "deadlineWorkingDays": 2,
      "assigneeRule": { "type": 2, "value": "ManagerId" },
      "statusBindings": []
    }
  ]
}
```

## 2. Publish definition

`POST /process-definitions/{id}/publish`

Publishing sets selected version to `Published` and archives previous published version.

## 3. Preview route

`POST /process-instances/preview`

Example:

```json
{
  "processKey": "vacation_approval",
  "draftData": {
    "ManagerId": "user.manager.1",
    "DaysRequested": 10
  }
}
```

## 4. Start workflow runtime

`POST /process-instances/start`

Example:

```json
{
  "processKey": "vacation_approval",
  "requestId": "VAC-2026-001",
  "requestData": {
    "ManagerId": "user.manager.1",
    "InitiatorId": "user.employee.5"
  }
}
```

## 5. Execute step action

Supported endpoints:

- `POST /step-instances/{id}/claim`
- `POST /step-instances/{id}/approve`
- `POST /step-instances/{id}/reject`
- `POST /step-instances/{id}/rework`

Payload:

```json
{
  "userId": "user.manager.1",
  "comment": "Looks good"
}
```

## 6. Manage custom statuses

- `GET /process-definitions/{id}/statuses`
- `POST /process-definitions/{id}/statuses`
- `PUT /process-definitions/{id}/statuses/{statusKey}`
- `DELETE /process-definitions/{id}/statuses/{statusKey}`

Status deletion is blocked if status key is used in step bindings.

## 7. Dashboard operational endpoints

- `GET /workflow-dashboard/api/stats`
- `GET /workflow-dashboard/api/definitions`
- `GET /workflow-dashboard/api/instances`
- `GET /workflow-dashboard/api/failed`
- `POST /workflow-dashboard/api/steps/{stepId}/{actionName}` where `actionName` is `claim|approve|reject|rework`
- `POST /workflow-dashboard/api/retry/{instanceId}`
