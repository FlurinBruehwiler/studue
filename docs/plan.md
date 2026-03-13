# Studue Project Plan

## Goals
- Build a web-based version of the student assignment tracker originally planned with JavaFX.
- Use a React + Vite frontend styled with Tailwind CSS and shadcn/ui.
- Use a Java backend with Gradle and only standard library dependencies.
- Store assignments as JSON files: one folder per class, one file per assignment, with the due date included in the filename for efficient future-date lookups.
- Deploy under the domain `studue.ch`.

## Scope for V1
- Support a single class only.
- Allow public read access without login.
- Allow editing only for authenticated `github.zhaw.ch` users whose email is hardcoded in the backend allowlist.
- Use a JSON-only API between frontend and backend.
- Persist data in files on disk instead of a database.

## Assumptions
- The first version supports a single class only.
- The storage layout should still leave room for multiple classes later.
- File-based storage is acceptable for the expected scale.
- No database or external packages are required on the backend.

## Proposed Architecture
- Frontend: React + Vite, Tailwind CSS, shadcn/ui components.
- Backend: Java (Gradle), standard library only, HTTP server via `com.sun.net.httpserver.HttpServer`.
- Storage: `data/<class-name>/YYYY-MM-DD--<random-id>.json` per assignment, with one configured class in v1.
- API: REST-style JSON endpoints for listing, reading, creating, updating, and deleting assignments.
- Auth: OAuth login via `github.zhaw.ch`, implemented manually with standard-library HTTP handling, secure cookies, and backend-managed session state.

## Repository Structure

```text
studue/
  backend/
    build.gradle
    settings.gradle
    gradlew
    gradlew.bat
    gradle/wrapper/
    src/main/java/ch/studue/
      App.java
      config/
      http/
      auth/
      assignment/
      json/
      storage/
    src/test/java/ch/studue/
  frontend/
    package.json
    vite.config.js
    index.html
    components.json
    src/
      main.jsx
      App.jsx
      index.css
      lib/
      components/
        ui/
      hooks/
      pages/
      api/
  data/
    it25ta_win/
  docs/
    plan.md
    api-contracts.md
```

## Data Model

### Assignment
- `id`
- `className`
- `module`
- `title`
- `dueDate` (`YYYY-MM-DD`)
- `note`
- `mandatory` (`boolean`)
- `createdBy`
- `updatedBy`
- `createdAt`
- `updatedAt`

### User Session
- `githubLogin`
- `displayName`
- `email`
- `isAllowedEditor`
- `sessionId`
- `expiresAt`

### Assignment Example

```json
{
  "id": "2026-03-20--a1b2c3d4",
  "className": "it25ta_win",
  "module": "Software Engineering",
  "title": "Project sketch submission",
  "dueDate": "2026-03-20",
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

## Backend Design

### Main Components
- `App`: bootstrap config, services, and HTTP server.
- `config/`: runtime configuration such as port, base URL, OAuth endpoints, and class name.
- `http/`: shared request/response helpers, routing, and error handling.
- `auth/`: OAuth flow, session storage, cookie handling, and allowlist authorization.
- `assignment/`: assignment model, validation, service layer, and API handlers.
- `json/`: minimal JSON parsing/serialization utilities built in-house.
- `storage/`: file-based repository and atomic writes.

### HTTP Approach
- Use `HttpServer` contexts for `/api/assignments`, `/api/auth`, and `/health`.
- Return JSON for every API response except OAuth redirects.
- Keep handlers thin and move logic into services.
- Add lightweight request parsing helpers for path params, query params, cookies, and JSON bodies.

### Storage Strategy
- Use `data/it25ta_win/` in v1, but keep class name configurable internally.
- Persist each assignment in `YYYY-MM-DD--<random-id>.json`.
- Keep `id` aligned with the filename without the `.json` extension.
- Write updates via temp file + move to reduce corruption risk.
- When listing upcoming assignments, compare the filename prefix date before reading file contents where possible.
- Sort output by `dueDate`, then `title`.

### Validation Rules
- `module`: required, trimmed, 1-120 characters.
- `title`: required, trimmed, 1-200 characters.
- `dueDate`: required, valid ISO date `YYYY-MM-DD`.
- `note`: optional, default empty string, reasonable max length.
- `mandatory`: required boolean.
- `id`, `className`, `createdBy`, `updatedBy`, `createdAt`, `updatedAt`: server controlled.

### JSON Handling
- No third-party JSON package is used.
- Keep request bodies intentionally small and predictable.
- Implement only the JSON features needed by the app: objects, arrays, strings, booleans, null, numbers if needed.
- Return `400` for malformed JSON or invalid field types.

## OAuth Flow (Detailed Draft)
- Unauthenticated users can read all assignment data.
- Authors authenticate with `github.zhaw.ch` using OAuth authorization code flow.
- Backend redirects users to the GitHub enterprise authorization page.
- OAuth callback exchanges the code for an access token using manual HTTPS requests from the Java standard library.
- Backend fetches user identity data and email data.
- Backend checks whether the returned email is in the configured hardcoded allowlist.
- If allowed, the backend creates a server-side session and sets a secure HTTP-only cookie.
- Frontend uses `GET /api/auth/me` to detect login state and show edit actions only to authenticated users.

## Authorization Rules
- Anyone can view assignments without logging in.
- A user may edit only if they successfully authenticate via `github.zhaw.ch`.
- A user must also have an email address present in the backend allowlist.
- The allowlist is initially limited to your class.
- The application should be structured so multi-class authorization can be added later.

## Allowlist Handling (v1)
- Hardcode the allowed editor email addresses in one backend class.
- Keep the allowlist logic isolated behind a small authorization component so it can later move to config or storage without touching the OAuth flow.
- Avoid scattering email checks across handlers; use one central permission check.

## Frontend Design

### Pages
- Main overview page with upcoming assignments and filters.
- Assignment detail panel or page.
- Auth-aware create/edit form.

### UI Sections
- Top bar with app branding, login/logout, and editor state.
- Filter area for module, due date range, and mandatory flag.
- Assignment list grouped or sorted by upcoming date.
- Empty state when no assignments match.
- Form dialog/page for creating and editing assignments.

### State and Data Fetching
- Fetch `GET /api/auth/me` at app startup.
- Fetch `GET /api/assignments` on initial page load and when filters change.
- Use local React state and small hooks instead of adding a larger state library.
- Keep form state local to the create/edit form.

### Styling Direction
- Use Tailwind utility classes and CSS variables.
- Set up the project in a way that shadcn/ui components can be added directly.
- Keep the first version clean, readable, and mobile-friendly.

## Backend Endpoints
- `GET /api/assignments`
- `GET /api/assignments/{id}`
- `POST /api/assignments`
- `PUT /api/assignments/{id}`
- `DELETE /api/assignments/{id}`
- `GET /api/auth/me`
- `GET /api/auth/login`
- `GET /api/auth/callback`
- `POST /api/auth/logout`
- `GET /health`

## API Contract Summary

### General Rules
- JSON request/response format for all API endpoints except OAuth redirects.
- `application/json; charset=utf-8` for successful and error responses.
- Timestamps use ISO-8601 UTC strings.
- Dates use `YYYY-MM-DD`.

### Error Shape

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

### Auth Contract
- `GET /api/auth/me`: returns current session state.
- `GET /api/auth/login`: starts OAuth redirect flow.
- `GET /api/auth/callback`: completes OAuth redirect flow.
- `POST /api/auth/logout`: clears the current session.

### Assignment Contract
- `GET /api/assignments`: optional filters `from`, `to`, `module`, `mandatory`.
- `GET /api/assignments/{id}`: returns a single assignment.
- `POST /api/assignments`: creates an assignment.
- `PUT /api/assignments/{id}`: updates an assignment.
- `DELETE /api/assignments/{id}`: deletes an assignment.

## Config Needed
- `GITHUB_CLIENT_ID`
- `GITHUB_CLIENT_SECRET`
- `GITHUB_OAUTH_BASE_URL`
- `GITHUB_API_BASE_URL`
- `DEFAULT_CLASS_NAME`
- `APP_BASE_URL`
- `SESSION_SECRET`

## Milestones
1. Repo scaffolding
   - Create `frontend/` with Vite + React + Tailwind + shadcn/ui-ready structure.
   - Create `backend/` with Gradle Java app and minimal HTTP server.
2. Storage layer
   - Implement file-based read/write for assignments.
   - Define JSON schema and validation rules.
3. API layer
   - Implement list, get, create, update, delete endpoints.
   - Add JSON error handling.
4. Auth and permissions
   - Implement GitHub Enterprise OAuth.
   - Add session handling and hardcoded email allowlist checks.
5. Frontend UI
   - Build overview, filters, detail view, and form.
6. Deployment
   - Build frontend assets.
   - Serve or proxy the app under `studue.ch`.

## Risks / Open Items
- Concurrency and data integrity with file storage.
- Exact `github.zhaw.ch` OAuth and API endpoints need to be confirmed.
- Session security, CSRF protection, and cookie settings need careful implementation.
- Need to confirm whether the OAuth user-info flow reliably returns the email address needed for allowlist checks.
- Standard-library-only JSON parsing will require careful testing.

## Immediate Next Steps
1. Scaffold the frontend with React, Vite, Tailwind, and shadcn-ready structure.
2. Scaffold the backend with Gradle and Java package structure.
3. Write standalone API documentation in `docs/api-contracts.md`.
4. Implement the assignment storage layer and list endpoint first.
