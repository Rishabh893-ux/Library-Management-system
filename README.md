# Library & Resource Management API

An ASP.NET Core Web API (.NET 8) for managing a library: the book catalogue, loans with late fines, a FIFO reservation queue, and librarian reports. It uses SQL Server via Entity Framework Core (code-first migrations) and JWT authentication with two roles.

> **Status:** all code is written, but it has **not yet been compiled or tested**. The machine it was written on had no .NET SDK. Remaining steps:
> 1. `dotnet build`
> 2. Generate the initial migration: `dotnet ef migrations add InitialCreate --project src/LibraryManagement.Api --output-dir Data/Migrations`
> 3. `dotnet test`

## Contents

- [Quick start](#quick-start)
- [Configuration](#configuration)
- [Database: migrations and seed data](#database-migrations-and-seed-data)
- [Using the API](#using-the-api)
- [Architecture](#architecture)
- [Business rules](#business-rules)
- [Tests](#tests)

---

## Quick start

**Prerequisites**

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server. LocalDB (installed with Visual Studio) works as-is. SQL Server Express, Developer or a Docker container also work.

```bash
dotnet tool restore                    # installs the pinned dotnet-ef tool
# first time only: create the initial migration (see "Database" below)
dotnet ef migrations add InitialCreate --project src/LibraryManagement.Api --output-dir Data/Migrations
dotnet run --project src/LibraryManagement.Api --launch-profile https
```

Open **https://localhost:7180/swagger**.

In the `Development` environment the app applies migrations and seeds sample data on startup, so there is nothing else to run. See [Database](#database-migrations-and-seed-data) to do it manually.

## Configuration

All settings live in `src/LibraryManagement.Api/appsettings.json`. `appsettings.Development.json` overrides them for local development.

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:LibraryDb` | SQL Server connection string | LocalDB, database `LibraryDb` |
| `Jwt:Key` | HMAC signing key, **at least 32 characters** | empty in `appsettings.json`, dev key in Development |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes` | Token settings | `LibraryManagement.Api` / `LibraryManagement.Clients` / 60 |
| `LoanPolicy:LoanPeriodDays` | Loan length | 14 |
| `LoanPolicy:FinePerDay` | Late fine in ₹ per day | 5 |
| `Database:ApplyMigrationsOnStartup` | Run `Migrate()` when the app starts | `false` (`true` in Development) |
| `Database:SeedSampleData` | Insert demo data into an empty DB | `false` (`true` in Development) |

Configuration is validated at startup. For example, the app refuses to start if `Jwt:Key` is too short.

### Connection string examples

```jsonc
// LocalDB (default)
"Server=(localdb)\\MSSQLLocalDB;Database=LibraryDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"

// SQL Server Express
"Server=.\\SQLEXPRESS;Database=LibraryDb;Trusted_Connection=True;TrustServerCertificate=True"

// SQL Server in Docker (SQL authentication)
"Server=localhost,1433;Database=LibraryDb;User Id=sa;Password=<your-password>;TrustServerCertificate=True"
```

### Keep secrets out of source control

For anything beyond local development, use user-secrets or environment variables instead of editing the JSON files:

```bash
cd src/LibraryManagement.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<a random string of 32+ characters>"
dotnet user-secrets set "ConnectionStrings:LibraryDb" "<connection string>"

# or, as environment variables (double underscore = section separator)
Jwt__Key=...  ConnectionStrings__LibraryDb=...
```

## Database: migrations and seed data

Migrations go in `src/LibraryManagement.Api/Data/Migrations`. The `InitialCreate` migration hasn't been generated yet (see Status above). Create it once with the first command below, then commit it.

```bash
# from the repository root
dotnet tool restore

# one-time: generate the initial migration from the entity model
dotnet ef migrations add InitialCreate --project src/LibraryManagement.Api --output-dir Data/Migrations

# create or upgrade the database
dotnet ef database update --project src/LibraryManagement.Api

# after changing entities or LibraryDbContext, add a new migration
dotnet ef migrations add <Name> --project src/LibraryManagement.Api --output-dir Data/Migrations

# generate an idempotent SQL script (for DBAs or CI deployments)
dotnet ef migrations script --idempotent --project src/LibraryManagement.Api -o library.sql
```

### Sample data

With `Database:SeedSampleData = true`, `DbSeeder` fills an **empty** database (it skips seeding if any member exists) with:

| Account | Email | Password | Role |
|---|---|---|---|
| Head Librarian | `admin@library.local` | `Admin@123` | Librarian |
| Alice Sharma | `alice@example.com` | `Member@123` | Member |
| Bob Verma | `bob@example.com` | `Member@123` | Member |
| Carol Iyer | `carol@example.com` | `Member@123` | Member |

It also adds:

- 10 books across Software, Fiction, History, Science and other categories.
- Loan history, so the most-borrowed report has a clear ranking.
- Two **overdue** active loans (6 and 16 days late) for the overdue report.
- *The God of Small Things* and *Midnight's Children*, with every copy on loan and members waiting in their reservation queues.

To reseed, drop the database and restart: `dotnet ef database drop --project src/LibraryManagement.Api`.

## Using the API

1. `POST /api/auth/login` with `{ "email": "admin@library.local", "password": "Admin@123" }`.
2. Copy the `token` from the response, click **Authorize** in Swagger, and paste it.

| Method | Route | Who | Description |
|---|---|---|---|
| POST | `/api/auth/register` | anyone | Create a **Member** account; returns a JWT |
| POST | `/api/auth/login` | anyone | Returns a JWT |
| GET | `/api/books?title=&author=&category=&page=&pageSize=` | any user | Search (partial, case-insensitive), paged |
| GET | `/api/books/{id}` | any user | Book details |
| POST | `/api/books` | Librarian | Create a book |
| PUT | `/api/books/{id}` | Librarian | Update a book (changing TotalCopies adjusts AvailableCopies) |
| DELETE | `/api/books/{id}` | Librarian | Delete a book (refused while copies are on loan) |
| POST | `/api/loans` | any user | Borrow `{ bookId, memberId? }`. `memberId` is for librarians only |
| POST | `/api/loans/{id}/return` | owner or Librarian | Return a book, charge any fine, process the queue |
| GET | `/api/loans/active?memberId=` | self or Librarian | A member's unreturned loans |
| POST | `/api/reservations` | any user | Reserve `{ bookId, memberId? }` a book with no free copies |
| DELETE | `/api/reservations/{id}` | owner or Librarian | Cancel a reservation |
| GET | `/api/reservations?memberId=` | self or Librarian | A member's reservations, with queue positions |
| GET | `/api/reservations/book/{bookId}` | Librarian | The queue for a book |
| GET | `/api/reports/most-borrowed` | Librarian | Top 5 books by loan count (raw SQL `GROUP BY` / `ORDER BY`) |
| GET | `/api/reports/overdue-loans` | Librarian | Overdue loans with the fine owed so far and a total |

**Roles.** The spec's "Admin/Librarian" role is `Librarian` in code. Self-registration always creates a `Member`. The only librarian account is the seeded one.

**Errors** are returned as RFC 7807 `ProblemDetails`:

| Status | Meaning |
|---|---|
| 400 | Validation errors, per field |
| 401 | Missing or invalid token, or bad login |
| 403 | Wrong role, or acting on another member's data |
| 404 | Unknown id |
| 409 | Business rule broken (e.g. no copies left), or a concurrent update |

## Architecture

```
LibraryManagement/
├── src/LibraryManagement.Api/
│   ├── Controllers/     HTTP only: routing, [Authorize], resolving "who is acting", status codes
│   ├── DTOs/            Request models (DataAnnotations validation) and response records
│   ├── Services/        All business rules: Auth, Token, Book, Loan, Reservation, ReservationQueue,
│   │                    FineCalculator, Report, Notification, plus entity→DTO Mapping
│   ├── Entities/        EF Core entities: Book, Member, Loan, Reservation, enums
│   ├── Data/            LibraryDbContext (Fluent API config), DbSeeder, Migrations/
│   ├── Common/          Exceptions → ProblemDetails handler, role names, ClaimsPrincipal helpers
│   ├── Options/         Strongly-typed settings (JwtOptions, LoanPolicyOptions)
│   └── Program.cs       Composition root: DI, auth, Swagger, pipeline, migrate/seed
└── tests/LibraryManagement.Tests/   xUnit tests on in-memory SQLite
```

**Request flow:** `Controller → Service → LibraryDbContext → SQL Server`

- **Controllers stay thin.** They never touch `DbContext` or return entities. Everything that crosses the API boundary is a DTO, mapped in `Services/Mapping.cs`.
- **Services use `DbContext` directly** as the unit of work. EF Core's `DbSet` already is a repository, so a separate generic repository layer would add indirection without adding value. Each service is behind an interface so it can be substituted in tests.
- **Services report errors by throwing typed exceptions** (`NotFoundException`, `BusinessRuleException`, `ForbiddenException`). `GlobalExceptionHandler` turns them into `ProblemDetails` with the matching status code, so controllers have no try/catch.
- **Time comes from `TimeProvider`**, injected everywhere, so tests control the clock precisely for due dates and fines.
- **`FineCalculator` is a pure class** holding the loan-period and fine policy (values from configuration), so it is easy to unit test.
- **`ReservationQueue`** holds the queue logic in one place. Returning a book, cancelling a reservation and adding copies to a book all use it.
- **`INotificationService`** is a seam for email/SMS. The default implementation only writes a log line. Notifications are sent *after* the database commit, so a failed send never rolls back a return.

### Data model

```
Member 1 ── * Loan        * ── 1 Book
Member 1 ── * Reservation * ── 1 Book
```

- Unique indexes on `Book.Isbn` and `Member.Email`.
- Check constraints: `0 <= AvailableCopies <= TotalCopies`.
- An index on `(BookId, Status, ReservationDate)` serves the FIFO queue lookup.
- Enums are stored as readable strings.
- Deleting a book cascades to its loans and reservations. Deleting a member is restricted.

## Business rules

| Rule | Where |
|---|---|
| A book can't be borrowed when `AvailableCopies` is 0; the member must reserve it instead (409 with a hint) | `LoanService.BorrowAsync` |
| The loan period is 14 days; the fine is ₹5 per **calendar day** after the due date (returning on the due date is free) | `FineCalculator` |
| Returning stamps `ReturnDate`, stores `FineAmount`, and increments `AvailableCopies` | `LoanService.ReturnAsync` |
| Returning processes the book's queue (FIFO by `ReservationDate`, then `Id`) | `ReservationQueue.ProcessAsync` |
| A book can only be reserved when no copy is free. A member can have one open reservation per book and can't reserve a book they already have | `ReservationService.ReserveAsync` |
| A member can't have two copies of the same book on loan | `LoanService.BorrowAsync` |

### How the reservation queue works

The spec asks for the next member to be notified when a copy comes back. If the copy then simply went back on the shelf, another member could borrow it before the notified member arrived, and the queue would mean nothing. So a notified reservation **holds** the copy:

1. When a copy comes back, `AvailableCopies` goes up by 1. If there are `Pending` reservations, the oldest one becomes **`Notified`** (with `NotifiedDate`) and a notification is sent.
2. A `Notified` reservation holds one available copy. The number of copies anyone can borrow is `AvailableCopies − (Notified reservations)`.
3. When the notified member borrows the book, the reservation becomes **`Fulfilled`**. This is a fourth status alongside Pending, Notified and Cancelled, so closed reservations stay in the history.
4. If a notified member **cancels**, the held copy goes straight to the next `Pending` member.
5. If a librarian **raises TotalCopies**, the new copies are given to waiting members immediately.

**Concurrency.** `Book.AvailableCopies` is an EF Core concurrency token. If two requests race for the last copy, one succeeds and the other gets `409 Concurrent update`, so a copy can never be lent twice.

**Known simplifications:**

- Held copies don't expire. A real system would add a pickup window, e.g. a background job that cancels `Notified` reservations older than N days.
- Fine days are counted in UTC calendar days.

## Tests

```bash
dotnet test
```

The tests (xUnit) cover the logic most likely to go wrong. They run against **in-memory SQLite** rather than the EF InMemory provider, so foreign keys, check constraints and the concurrency token behave as they would in production. Each service call gets a fresh `DbContext`, just like separate HTTP requests.

- **`FineCalculatorTests`**: the 14-day due date; fines on and around the due date (same day, next day before 24 hours have passed, 10 and 30 days late); configurable rates.
- **`LoanServiceTests`**:
  - Borrowing decrements copies, and borrowing with no copies fails.
  - Late returns charge a fine and on-time returns don't.
  - A double return is rejected.
  - Members can only return their own loans.
  - The active-loans list and the overdue report.
- **`ReservationQueueTests`**:
  - FIFO positions, and a return notifies only the head of the queue.
  - The held copy can't be taken by others, and the notified member's borrow fulfils the reservation.
  - The queue advances across successive returns.
  - Cancelling a held reservation passes the copy on, and adding copies serves the queue.
  - Reservation guard rules.

The most-borrowed report uses SQL Server syntax (`TOP`), so it isn't covered by the SQLite tests. Check it against a real SQL Server, e.g. with Swagger and the seeded data.
