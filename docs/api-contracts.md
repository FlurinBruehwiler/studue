# Studue API Contracts

## General Rules
- Base API path: `/api`
- Content type: `application/json; charset=utf-8`
- Dates use `YYYY-MM-DD`
- Timestamps use ISO-8601 UTC strings
- All mutating endpoints require an authenticated and allowlisted session

## Error Format

```json
{
  "error": {
    "code": "validation_error",
    "message": "The dueDate field must use YYYY-MM-DD.",
    "details": {
      "field": "dueDate"
    }
  }
}
```

## Auth Endpoints

### `GET /api/auth/me`

Authenticated response:

```json
{
  "authenticated": true,
  "user": {
    "githubLogin": "jdoe",
    "displayName": "Jane Doe",
    "email": "jane.doe@students.zhaw.ch",
    "isAllowedEditor": true
  }
}
```

Anonymous response:

```json
{
  "authenticated": false,
  "user": null
}
```

### `GET /api/auth/login`
- Starts the OAuth authorization-code flow.
- Response is an HTTP redirect, not JSON.

### `GET /api/auth/callback`
- Completes the OAuth flow.
- Response is an HTTP redirect back to the frontend.

### `POST /api/auth/logout`

Response:

```json
{
  "ok": true
}
```

## Assignment Model

```json
{
  "id": "2026-03-20--a1b2c3d4",
  "className": "it25ta_win",
  "module": "Software Engineering",
  "title": "Project sketch submission",
  "dueDate": "2026-03-20",
  "dueTime": "15:00",
  "note": "Upload PDF before class starts.",
  "mandatory": true,
  "createdBy": {
    "githubLogin": "jdoe",
    "displayName": "Jane Doe",
    "email": "jane.doe@students.zhaw.ch"
  },
  "updatedBy": {
    "githubLogin": "jdoe",
    "displayName": "Jane Doe",
    "email": "jane.doe@students.zhaw.ch"
  },
  "createdAt": "2026-03-10T18:42:00Z",
  "updatedAt": "2026-03-10T18:42:00Z"
}
```

## Assignment Endpoints

### `GET /api/assignments`

Optional query params:
- `from`
- `to`
- `module`
- `mandatory`

Response:

```json
{
  "items": [
    {
      "id": "2026-03-20--a1b2c3d4",
      "className": "it25ta_win",
      "module": "Software Engineering",
      "title": "Project sketch submission",
      "dueDate": "2026-03-20",
      "dueTime": "15:00",
      "note": "Upload PDF before class starts.",
      "mandatory": true,
      "createdBy": {
        "githubLogin": "jdoe",
        "displayName": "Jane Doe",
        "email": "jane.doe@students.zhaw.ch"
      },
      "updatedBy": {
        "githubLogin": "jdoe",
        "displayName": "Jane Doe",
        "email": "jane.doe@students.zhaw.ch"
      },
      "createdAt": "2026-03-10T18:42:00Z",
      "updatedAt": "2026-03-10T18:42:00Z"
    }
  ]
}
```

### `GET /api/assignments/{id}`

Response:

```json
{
  "item": {
    "id": "2026-03-20--a1b2c3d4",
    "className": "it25ta_win",
    "module": "Software Engineering",
    "title": "Project sketch submission",
    "dueDate": "2026-03-20",
    "dueTime": "15:00",
    "note": "Upload PDF before class starts.",
    "mandatory": true,
    "createdBy": {
      "githubLogin": "jdoe",
      "displayName": "Jane Doe",
      "email": "jane.doe@students.zhaw.ch"
    },
    "updatedBy": {
      "githubLogin": "jdoe",
      "displayName": "Jane Doe",
      "email": "jane.doe@students.zhaw.ch"
    },
    "createdAt": "2026-03-10T18:42:00Z",
    "updatedAt": "2026-03-10T18:42:00Z"
  }
}
```

### `POST /api/assignments`

Request:

```json
{
  "module": "Software Engineering",
  "title": "Project sketch submission",
  "dueDate": "2026-03-20",
  "dueTime": "15:00",
  "note": "Upload PDF before class starts.",
  "mandatory": true
}
```

Response `201`:

```json
{
  "item": {
    "id": "2026-03-20--a1b2c3d4",
    "className": "it25ta_win",
    "module": "Software Engineering",
    "title": "Project sketch submission",
    "dueDate": "2026-03-20",
    "dueTime": "15:00",
    "note": "Upload PDF before class starts.",
    "mandatory": true,
    "createdBy": {
      "githubLogin": "jdoe",
      "displayName": "Jane Doe",
      "email": "jane.doe@students.zhaw.ch"
    },
    "updatedBy": {
      "githubLogin": "jdoe",
      "displayName": "Jane Doe",
      "email": "jane.doe@students.zhaw.ch"
    },
    "createdAt": "2026-03-10T18:42:00Z",
    "updatedAt": "2026-03-10T18:42:00Z"
  }
}
```

### `PUT /api/assignments/{id}`

Request:

```json
{
  "module": "Software Engineering",
  "title": "Updated title",
  "dueDate": "2026-03-22",
  "dueTime": "10:30",
  "note": "Updated note.",
  "mandatory": false
}
```

Response `200`:

```json
{
  "item": {
    "id": "2026-03-20--a1b2c3d4",
    "className": "it25ta_win",
    "module": "Software Engineering",
    "title": "Updated title",
    "dueDate": "2026-03-22",
    "dueTime": "10:30",
    "note": "Updated note.",
    "mandatory": false,
    "createdBy": {
      "githubLogin": "jdoe",
      "displayName": "Jane Doe",
      "email": "jane.doe@students.zhaw.ch"
    },
    "updatedBy": {
      "githubLogin": "jdoe",
      "displayName": "Jane Doe",
      "email": "jane.doe@students.zhaw.ch"
    },
    "createdAt": "2026-03-10T18:42:00Z",
    "updatedAt": "2026-03-11T07:30:00Z"
  }
}
```

### `DELETE /api/assignments/{id}`

Response:

```json
{
  "ok": true
}
```

## Validation Rules
- `module`: required, trimmed, 1-120 characters
- `title`: required, trimmed, 1-200 characters
- `dueDate`: required, valid `YYYY-MM-DD`
- `dueTime`: optional, valid `HH:MM`
- due date/time must not be in the past
- `note`: optional, defaults to empty string
- `mandatory`: required boolean
- `id` is generated by the server
- `className` is controlled by the server in v1
