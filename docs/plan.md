# Studue Project Plan

## Goals
- Build a web-based version of the student assignment tracker originally planned with JavaFX.
- Use a React + Vite frontend styled with Tailwind CSS and shadcn/ui.
- Use a Java backend with Gradle and only standard library dependencies.
- Store assignments as JSON files: one folder per class, one file per assignment, with the due date included in the filename for efficient future-date lookups.
- Deploy under the domain `studue.ch`.

## Assumptions
- The first version supports a single class only.
- The storage layout should still leave room for multiple classes later.
- File-based storage is acceptable for the expected scale.
- No database or external packages are required on the backend.

## Proposed Architecture
- Frontend: React + Vite, Tailwind CSS, shadcn/ui components.
- Backend: Java (Gradle), standard library only, HTTP server via `com.sun.net.httpserver.HttpServer`.
- Storage: `data/<class-name>/YYYY-MM-DD--<random-id>.json` per assignment, with one configured class in v1.
- API: REST-style JSON endpoints for listing, reading, creating, updating assignments.
- Auth: OAuth login via `github.zhaw.ch`, implemented manually with standard-library HTTP handling, secure cookies, and backend-managed session state.

## Data Model (Draft)
- Assignment
  - id
  - className
  - module
  - title
  - dueDate (YYYY-MM-DD)
  - note
  - mandatory (boolean)
  - createdBy
  - updatedBy
  - createdAt
  - updatedAt

- User session
  - githubLogin
  - displayName
  - email
  - isAllowedEditor
  - sessionId
  - expiresAt

## Milestones
1. Repo scaffolding
   - Create `frontend/` with Vite + React + Tailwind + shadcn/ui.
   - Create `backend/` with Gradle Java app and minimal HTTP server.
2. Storage layer
   - Implement file-based read/write for classes and assignments.
   - Define JSON schema and validation rules.
3. API layer
   - Endpoints for list, get, create, update, delete assignments.
   - Basic error handling and validation.
4. Frontend UI
   - Main list page (filters, upcoming tasks).
   - Detail view and edit form (authors only).
5. Auth and permissions
   - Implement OAuth login with `github.zhaw.ch`.
   - Add login start, callback, logout, session, and current-user endpoints.
   - Restrict create/update/delete actions to authenticated users whose email is in a backend allowlist.
   - Keep read access public.
6. Deployment
   - Build frontend and serve static assets.
   - Run backend and configure reverse proxy for `studue.ch`.
   - Configure OAuth app callback URL and production secrets.

## OAuth Flow (Draft)
- Unauthenticated users can read all assignment data.
- Authors authenticate with `github.zhaw.ch` using OAuth authorization code flow.
- Backend redirects users to the GitHub enterprise authorization page.
- OAuth callback exchanges the code for an access token using manual HTTPS requests from the Java standard library.
- Backend fetches user identity data, checks whether the returned email is in the configured allowlist, creates a server-side session, and sets a secure HTTP-only cookie.
- Frontend uses a `me` endpoint to detect login state and show edit actions only to authenticated users.

## Authorization Rules
- Anyone can view assignments without logging in.
- A user may edit only if they successfully authenticate via `github.zhaw.ch`.
- A user must also have an email address present in the backend allowlist.
- The allowlist is initially limited to your class.
- The application should be structured so multi-class authorization can be added later.

## Allowlist Handling (v1)
- For the first version, hardcode the allowed editor email addresses in the backend source code.
- Keep the allowlist logic isolated behind a small authorization component so it can later move to config or storage without touching the OAuth flow.
- Avoid scattering email checks across handlers; use one central permission check.

## Backend Endpoints (Draft)
- `GET /api/assignments`
- `GET /api/assignments/{id}`
- `POST /api/assignments`
- `PUT /api/assignments/{id}`
- `DELETE /api/assignments/{id}`
- `GET /api/auth/me`
- `GET /api/auth/login`
- `GET /api/auth/callback`
- `POST /api/auth/logout`

## Storage Layout (v1)
- Use a single configured class directory, for example `data/it25ta_win/`.
- Store each assignment as `YYYY-MM-DD--<random-id>.json`.
- Expose class data through simplified single-class endpoints in v1.
- Keep the internal code organized so class-aware endpoints can be introduced later without rewriting the storage layer.

## Frontend Pages (Draft)
- Public overview page with upcoming assignments and filters.
- Assignment detail drawer or page.
- Auth-aware create/edit form.
- Login button for authors and visible author metadata on entries.

## Config Needed
- `GITHUB_CLIENT_ID`
- `GITHUB_CLIENT_SECRET`
- `GITHUB_OAUTH_BASE_URL`
- `GITHUB_API_BASE_URL`
- `DEFAULT_CLASS_NAME`
- `APP_BASE_URL`
- `SESSION_SECRET`

## Storage Notes
- Use a filename format like `YYYY-MM-DD--<random-id>.json`.
- The date stays visible and sortable in the filename.
- The random ID prevents collisions when multiple assignments share the same due date.
- Future-assignment queries can be optimized by checking filenames before reading file contents.

## Risks / Open Items
- Concurrency and data integrity with file storage.
- Exact `github.zhaw.ch` OAuth and API endpoints need to be confirmed.
- Session security, CSRF protection, and cookie settings need careful implementation.
- Need to confirm whether the OAuth user-info flow reliably returns the email address needed for allowlist checks.

## Next Steps
1. Confirm the exact GitHub enterprise OAuth endpoints and how to retrieve verified email addresses.
2. Scaffold frontend and backend skeletons.
3. Implement single-class storage, hardcoded allowlist authorization, session handling, and API endpoints.
4. Build the public overview UI and authenticated editing flow.
