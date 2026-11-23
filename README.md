# Allocation Run History & Exception Review

A read-only review tool for saved stock-allocation runs. A merchandiser signs in, sees saved runs newest first, opens a run, and filters it down to the replenishment gaps that are still open.

| Layer | Technology |
|---|---|
| API | ASP.NET Core on **.NET 10**, EF Core 10, ASP.NET Core Identity (cookie auth) |
| Database | **PostgreSQL** (Npgsql EF Core provider) |
| UI | **Angular 21** (standalone components, signals), served by the API in production |
| Tests | xUnit + `WebApplicationFactory` (API), Vitest + Angular TestBed (UI) |

> Frontend choice: the brief allows React or Angular. The hiring team confirmed that Angular is acceptable and that an Angular component test covering a user-facing flow meets the testing requirement.

---

## 1. Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0.x | `dotnet --list-sdks` |
| EF Core CLI | 10.x | `dotnet tool install --global dotnet-ef` (or `dotnet tool update --global dotnet-ef`) |
| Node.js | 22 LTS (npm 10+) | `node -v` |
| PostgreSQL | 15+ (tested with 17) | `psql --version` |

Docker is **not** required.

## 2. Run from a clean checkout

```powershell
git clone <repo-url> allocation-review
cd allocation-review
```

**2.1 Database.** Create an empty database (pgAdmin or psql):

```sql
CREATE DATABASE allocation_review;
```

The development connection string is in `backend/src/AllocationReview.Api/appsettings.Development.json`:

```
Host=localhost;Port=5432;Database=allocation_review;Username=postgres;Password=postgres
```

If your local `postgres` password is different, override it **without editing the file**:

```powershell
cd backend/src/AllocationReview.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=allocation_review;Username=postgres;Password=<your-password>"
cd ../../..
```

**2.2 Schema and seed data.** In Development the API applies EF migrations and seeds data when it starts (`Database:MigrateOnStartup` and `Seed:Enabled` are both `true`). To apply the schema yourself instead:

```powershell
cd backend
dotnet ef database update --project src/AllocationReview.Api
```

The seed is idempotent. It creates the `Admin` and `Viewer` roles, two demo users, and the two synthetic runs R-101 and R-102 from the brief.

**2.3 Start the API** (http://localhost:5080):

```powershell
cd backend
dotnet run --project src/AllocationReview.Api
```

**2.4 Start the UI** (http://localhost:4200). Open a second terminal:

```powershell
cd frontend
npm ci
npm start
```

The Angular dev server proxies `/api` to the API (`frontend/proxy.conf.json`), so the browser sees a single origin.

### Demo sign-in

| Role | Email | Password | What they see |
|---|---|---|---|
| Admin | `admin@demo.local` | `Admin#2026demo` | Run history and run review |
| Viewer | `viewer@demo.local` | `Viewer#2026demo` | A "No access" page. The API returns 403 for review endpoints |

These are local demo credentials, supplied through `appsettings.Development.json`. Change them with user-secrets or environment variables (`Seed__AdminPassword`, `Seed__ViewerPassword`). Seeding is off by default outside Development.

## 3. Tests

```powershell
# API: calculation, pagination, authorization, acceptance data (in-memory SQLite, no PostgreSQL needed)
cd backend
dotnet test

# UI: user-facing flows (sign-in -> history -> review -> exceptions filter, viewer, empty, expired, error)
cd frontend
npm test
```

| Area | File |
|---|---|
| Multi-batch calculation and status rules | `backend/tests/.../GapCalculatorTests.cs` |
| Acceptance data (R-102: 3 gaps, 5 units; filter shows G-01 and G-03; R-101 G-01 is separate) | `RunReviewTests.cs` |
| Pagination: order, bounds, stable paging, 400 on invalid input | `RunHistoryTests.cs` |
| 401 unauthenticated, 403 Viewer, 404 unknown run, login | `AuthorizationTests.cs` |
| Angular user flows | `frontend/src/app/app.flow.spec.ts` |

API tests run the real middleware, authentication and controllers through `WebApplicationFactory`. Only the database is swapped for in-memory SQLite, so they need no external systems or client data.

## 4. Production build

```powershell
cd frontend
npm run build                     # Angular output goes to backend/src/AllocationReview.Api/wwwroot
cd ../backend
dotnet publish src/AllocationReview.Api -c Release -o ../publish
cd ../publish
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__Default = "Host=localhost;Database=allocation_review;Username=postgres;Password=<pw>"
dotnet AllocationReview.Api.dll   # serves the UI and the API from one origin
```

## 5. Configuration by environment

Settings use standard ASP.NET Core configuration layers: `appsettings.json` → `appsettings.{Environment}.json` → user-secrets (Development only) → environment variables. Each layer overrides the one before it.

| Setting | Development | Testing (automated tests) | Production |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` (launchSettings) | `Testing` (set by test factory) | `Production` |
| Database | `ConnectionStrings:Default` in appsettings.Development.json / user-secrets | In-memory SQLite, registered by the test project | `ConnectionStrings__Default` **or** `DATABASE_URL` env var (a `postgres://` URL is accepted) |
| Migrations on start | on | n/a (`EnsureCreated`) | off by default (`Database__MigrateOnStartup=true` to enable) |
| Seed demo data | on | on (test users) | off by default (`Seed__Enabled=true` + passwords via env) |
| Logs | readable console with scopes | test output | JSON lines with scopes |
| Auth cookie | `SameAsRequest` | `SameAsRequest` | `Secure` always |
| Session length | `Auth:SessionMinutes` (30, sliding) | same | same |
| API base URL (UI) | `/api` proxied to :5080 | mocked | same origin (UI served from `wwwroot`) |

No secrets are stored in `appsettings.json`. The only credentials in the repository are the local demo values in `appsettings.Development.json`.

## 6. API

| Method | Path | Access | Notes |
|---|---|---|---|
| POST | `/api/auth/login` | anonymous | `{ email, password }` sets an HttpOnly cookie. Returns 401 for any bad credentials (no account enumeration) and 423 when the account is locked out |
| POST | `/api/auth/logout` | signed in | |
| GET | `/api/auth/me` | signed in | `{ email, roles }`, used to restore or detect a session |
| GET | `/api/runs?page=1&pageSize=20` | **Admin** | Newest first. `pageSize` 1–50, `page` ≥ 1, otherwise 400 |
| GET | `/api/runs/{runId}?exceptionsOnly=false` | **Admin** | Totals and status counts always cover the full run. The filter narrows only the gap rows. Unknown run returns 404 |
| GET | `/health` | anonymous | Liveness check |

All errors are RFC 7807 `ProblemDetails` bodies with a `correlationId`. `backend/src/AllocationReview.Api/AllocationReview.Api.http` contains every acceptance check, ready to run.

## 7. Design

### Data model (PostgreSQL)

```
allocation_runs   (id PK, run_code UNIQUE, run_date)                 index (run_date DESC, id DESC)
allocation_gaps   (id PK, run_id FK, gap_code, store, brand,
                   product_type, shortfall numeric(12,2))           UNIQUE (run_id, gap_code)
batch_allocations (id PK, gap_id FK, batch_code, quantity)          index (gap_id)
```

The saved rows in the brief are flat: one row per batch, with the gap's shortfall repeated on each. I normalised them into **run → gap → batch allocation** rows. Because the shortfall is stored once per gap, it can't be double-counted however many batches a gap has. Since gap codes are only unique within a run, R-101 G-01 and R-102 G-01 are different gaps.

### Business rule

`Domain/GapCalculator.cs` is a pure function with no EF or HTTP dependencies, so it is unit tested directly:

- original shortfall = `ceil(shortfall)`, counted once
- allocated = sum of the batch quantities
- remaining = `max(0, original − allocated)`
- **Filled** when remaining = 0; **Unfilled** when remaining > 0 and nothing was allocated; **Partially filled** otherwise

### Responsibilities

```
Features/Auth/AuthController     sign-in, sign-out, current user
Features/Runs/RunsController     HTTP concerns only: authorization, input validation, status codes
Features/Runs/RunQueryService    data access and shaping into DTOs
Domain/GapCalculator             business rule
Infrastructure/                  correlation ID, exception handling, connection-string parsing
Data/                            DbContext, model configuration, idempotent seed
```

On the UI side, `core/` holds the services, auth interceptor and guards, and `pages/` holds one component per screen.

### Pagination

The history query is `ORDER BY run_date DESC, id DESC` with `OFFSET/LIMIT`, backed by a matching composite index. The run total is computed with a correlated `SUM` in the same SQL statement, so only one page of runs is ever read. `id` breaks ties, so runs that share a date never move between pages. Page size is capped at 50.

### Security

- Server-side authorization: the `AdminOnly` policy (role `Admin`) applies to the whole `RunsController`. The Angular guards only shape the UI and are not relied on for access control.
- The API returns 401/403 rather than redirecting to a login page (cookie events are overridden).
- The cookie is HttpOnly and `SameSite=Strict`, and `Secure` in Production. The UI never handles a token.
- Lockout after 5 failed attempts. Login failures get the same message whether or not the account exists.
- `returnUrl` after login only accepts in-app paths, which prevents open redirects.

### Logging

- `CorrelationIdMiddleware` accepts a safe `X-Correlation-ID` (8–64 characters, `[A-Za-z0-9-]`) or generates one. It returns the ID in the response header, adds it to the logging scope, and writes one summary line per API request: method, path and status. Query strings, headers and bodies are not logged.
- The run review adds a `RunId` scope.
- Unhandled exceptions are logged with their stack trace on the server. The client gets a generic 500 with the correlation ID only.
- Login logs record user IDs, never emails or passwords.

## 8. Assumptions

1. "Batch None / 0 units" (R-102 G-03) means *no allocation rows*, so the gap has zero batch allocations.
2. A run's *total allocated units* is the sum of all its batch quantities, including any over-allocation. Remaining units are clamped at 0.
3. Shortfall is stored as `numeric(12,2)` so a fractional source value is kept. It is rounded up only when computed.
4. History shows runs only. Counts by status are part of the review screen.
5. The exceptions filter narrows the gap rows only, and totals and counts still describe the whole run. The hiring team confirmed this.
6. Run identifiers in URLs are the business codes (`R-102`), not database IDs.
7. Gap rows are ordered by gap ID.
8. Demo users are seeded from configuration. There is no registration or recovery flow, as the brief asks.

## 9. Trade-offs

- **Normalised tables vs. storing the flat rows as given.** Normalising makes "count each gap once" a property of the schema instead of a grouping step every query must remember. The cost is that importing flat saved rows would need a mapping step, which doesn't exist here because the data is seeded.
- **Status computed on read vs. stored.** Computing it keeps a single source of truth and cannot drift. At large volume, a review would load every gap of a run into memory. Moving the calculation into SQL, or persisting it when a run is saved, would fix that.
- **Cookie auth vs. JWT.** The UI and API share an origin, so an HttpOnly cookie avoids storing tokens in the browser and is the framework's built-in mechanism. A separate mobile or third-party client would need bearer tokens.
- **SQLite for API tests.** Tests are fast and need no setup, but SQL-dialect differences from PostgreSQL wouldn't be caught. A Testcontainers PostgreSQL suite would be the next step.

## 10. Diagnosing a failure (example)

*A merchandiser reports: "Run review shows 'Something went wrong' with reference 3f2a…".*

1. Search the JSON logs for `CorrelationId = 3f2a…`. Every line for that request shares it.
2. The `HTTP GET /api/runs/R-205 responded 500` line, plus the `RunId = R-205` scope, identifies the run.
3. The `Unhandled exception` entry with the same ID holds the stack trace, for example a PostgreSQL connection timeout or a data problem in that run.
4. Reproduce it with the `.http` file against a copy of the data, then fix it and add a regression test in `RunReviewTests`.

## 11. Guiding an associate developer through a follow-up change

*Example: "Add a store filter to the run review."*

1. Start from the test: add a case in `RunReviewTests` for `?store=Store A`.
2. Add the query parameter in `RunsController`, validated the same way as the paging inputs.
3. Filter in `RunQueryService` next to `exceptionsOnly`. Totals stay full-run, matching the existing rule.
4. Add the parameter in `runs.service.ts`, then add a select in `run-review.page.ts` bound to a query parameter, the same pattern as `exceptions`.
5. Extend `app.flow.spec.ts` with the new interaction.

Each layer has one job, so a change touches one file per layer.

## 12. Optional: hosting on Render

The brief puts cloud deployment out of scope, but a `Dockerfile` is included. Render builds it on its own servers.

1. Create a **PostgreSQL** instance on Render and copy its *Internal Database URL*.
2. Create a **Web Service** from the repository with runtime **Docker**.
3. Set these environment variables: `DATABASE_URL` (the internal URL), `Database__MigrateOnStartup=true`, `Seed__Enabled=true`, `Seed__AdminPassword`, `Seed__ViewerPassword`.
4. Deploy and open the service URL.

## 13. Known limitations

- There's no CSRF token on the two POST endpoints. `SameSite=Strict` plus same-origin hosting mitigates this, and an antiforgery token would be added if the API were ever served cross-origin.
- PostgreSQL-specific behaviour is not covered by an automated test suite (see trade-offs).
