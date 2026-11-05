# Existing application baseline

The working application is `src/NexMed360.API.Old` (.NET 10, PostgreSQL,
ASP.NET Identity and JWT). `src/NexMed360.Api` is a scaffold; run the old host
for the existing portal. The architecture sections below describe the intended
structure, not completed migration or verified academic alignment.

Baseline evidence on 5 November 2026: the UI production build passed, including
lint/type checks and page generation. The user reported the API build, local
startup, database connectivity, current migrations, initializer invocation,
portal login and existing portal smoke checks working. The initializer may skip
existing data; this does not prove new-user seeding. Broader security, coverage,
performance and deployment verification remains pending.

The sibling `NexMed-360-UI` application uses Next.js and has a committed npm
lockfile for dependency installation. `tests/NexMed360.API.Old.Tests` contains
focused regression tests for patient-record ownership, account-update
authorization, booking eligibility, concurrent booking, appointment reads and
status-change and cancellation authorization, described below.

## Startup controls

- Set `AzureAppConfiguration:Enabled=true` to use Azure App Configuration,
  or `AzureAppConfiguration:Enabled=false` to use normal ASP.NET
  configuration (appsettings, Development user secrets, environment and CLI).
  Local mode makes no Azure configuration calls and requires no Azure login.
- With Azure enabled, `APP_CONFIG_ENDPOINT` and an authorized Azure identity
  are required. Explicit environment and command-line values override Azure
  configuration. Environment names still select the Azure labels.
- `DatabaseInitialization:MigrateOnStartup` and
  `DatabaseInitialization:SeedOnStartup` both default to `false`.
  These switches control startup only; normal API requests still use the
  configured database. No database is deleted by these controls.
- Migration and seeding are independent opt-ins. On an empty disposable
  database, migration must complete before seeding. Seeding creates roles and
  the existing `superuser@gmail.com` account when the relevant tables are empty;
  it is not a full synthetic dataset or a repair of partially populated data.
- Missing connection/JWT settings fail startup with key names, without printing
  secret values. Seeding also requires `Auth:SuperUserPassword`.

## Build checks (PowerShell)

These commands do not launch either application or initialize a database.
Record the SDK/Node/npm versions and actual output for the baseline evidence.
Use a .NET 10 SDK and a Node version supported by the committed UI lockfile
(Next 15.5.2 declares `^18.18.0 || ^19.8.0 || >=20.0.0`).

```powershell
Set-Location 'D:\Repos\NexMed360 Old\NexMed360-API'
dotnet --info
dotnet restore .\src\NexMed360.sln
dotnet build .\src\NexMed360.sln --no-restore

Set-Location 'D:\Repos\NexMed360 Old\NexMed-360-UI'
node --version
npm --version
npm ci
node .\node_modules\typescript\bin\tsc --noEmit --incremental false
npm run build
```

The user reported the patient-record and account-update builds and automated
tests passing on 5 November 2026, followed by confirmation of the
booking-eligibility build/tests. Concurrency verification subsequently passed
as recorded below. Appointment-read build/tests were also reported passing.
Status-change build/tests were subsequently reported passing. The new
cancellation checks and requester-ID migration are Not run. Manual
security/booking checks remain unverified. The UI's
existing `lint` script uses `next lint`; review that separately rather than
treating a build as a standalone lint or coverage result. Docker/deployment are outside this baseline item: the
old host's Dockerfile still names `NexMed360.API.csproj` and
`NexMed360.API.dll`, which no longer match this checkout.

## Local runtime checks (after confirming the disposable target)

Do not run this section until the intended PostgreSQL server/database and
credentials are established. Replace placeholders locally. Keep credentials
out of tracked appsettings and out of shared test output. User secrets below
belong to the existing host's Development configuration.

```powershell
Set-Location 'D:\Repos\NexMed360 Old\NexMed360-API'
$apiProject = '.\src\NexMed360.API.Old\NexMed360.API.Old.csproj'
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' '<confirmed disposable PostgreSQL connection string>' --project $apiProject
dotnet user-secrets set 'Auth:Jwt:Issuer' 'NexMed360.Local' --project $apiProject
dotnet user-secrets set 'Auth:Jwt:Audience' 'NexMed360.Local.Portal' --project $apiProject
dotnet user-secrets set 'Auth:Jwt:Signing-Key' '<local secret of at least 32 ASCII characters>' --project $apiProject
dotnet user-secrets set 'Auth:Jwt:Validity' '1' --project $apiProject

# 1. No startup migration or seed. Stop with Ctrl+C after inspection.
dotnet run --no-build --project $apiProject --launch-profile http -- --AzureAppConfiguration:Enabled=false --DatabaseInitialization:MigrateOnStartup=false --DatabaseInitialization:SeedOnStartup=false
```

Expected: Swagger at `http://localhost:5098/swagger`, the message
`Startup database migration and seeding are disabled.`, and no migration or
seed activity. Starting successfully does not establish database connectivity
or readiness of database-backed routes. Startup should also succeed with a
syntactically valid connection string pointing to a closed local port when
both switches are off; this checks that initialization did not connect.
For example, append the following single CLI argument to check 1:
`'--ConnectionStrings:DefaultConnection=Host=127.0.0.1;Port=1;Database=nexmed360_startup_check;Username=unused;Password=unused;Timeout=1'`.
Do not send database-backed requests during that closed-port check. Remove
that override to restore the confirmed target before continuing.

To check missing-configuration failure without changing secrets, repeat check
1 with `--Auth:Jwt:Issuer=` appended. Expected: exit before listening with
`Required configuration is missing: Auth:Jwt:Issuer`. To check the seed guard,
repeat it with `--DatabaseInitialization:SeedOnStartup=true` replacing false
and `--Auth:SuperUserPassword=` appended. Expected: exit naming
`Auth:SuperUserPassword`, before migration or seed activity.

For the **confirmed disposable target only**, initialization can then be
requested explicitly. Set a local seed password first. This command mutates
that target; stop the host after initialization and restart using check 1.

```powershell
dotnet user-secrets set 'Auth:SuperUserPassword' '<local password meeting existing Identity rules>' --project $apiProject
dotnet run --no-build --project $apiProject --launch-profile http -- --AzureAppConfiguration:Enabled=false --DatabaseInitialization:MigrateOnStartup=true --DatabaseInitialization:SeedOnStartup=true
```

Verify migrations and seeding complete without errors, then restart with both
switches false and confirm neither operation repeats. The existing initializer
can log user-creation failure without stopping the host; verify login succeeds
rather than relying on a listening message. Seed-only against an initialized
database and migrate-only are independent checks: only the enabled operation
should log/run. Keep all checks on the confirmed disposable target.

For the sibling UI, create its ignored `.env.local` containing:

```dotenv
NEXT_PUBLIC_API_ENDPOINT=http://localhost:5098/api/v1
```

Then run `npm run dev` from `D:\Repos\NexMed360 Old\NexMed-360-UI` and
open `http://localhost:3000`. Confirm login, account view and one existing
role-appropriate journey. `NEXT_PUBLIC_API_ENDPOINT` is needed at UI build
time too; rerun `npm run build` after setting it for a runtime-capable build.
Record results as observed, including failures. Access-control, booking,
architecture migration and FHIR work remain subsequent bounded items.


## Patient-record ownership regression checks

Both clinic-record read routes now pass the signed-in user ID and the Doctor
role from the authenticated principal to `PatientService`. Patients can read
only records whose patient profile belongs to that user ID. Patient IDs and
record IDs in the URL do not grant access. String user IDs remain unchanged.
Unauthorized targets return the same existing 404 error category as missing
targets, with no record/prescription data. Missing caller IDs return 401.

Doctor reads retain their existing access. Doctor hospital/relationship/sharing
rules and durable originating-hospital ownership are unresolved subsequent
work; this change establishes patient self-access only. Patient demographic
reads, account updates, record writes and booking integrity are separate items.

The approved test-only packages are xUnit v3 3.2.2, its Visual Studio adapter
3.1.5, Microsoft.NET.Test.Sdk 18.0.1 and EF Core InMemory 10.0.1 (matching the
application's EF version). The tests use isolated synthetic in-memory data and
the existing mapper; they do not start the API or connect to PostgreSQL.
They cover service ownership filters, prescriptions, empty lists, missing IDs,
invalid IDs and controller forwarding of caller identity and roles. They do
not establish PostgreSQL query translation or HTTP middleware behavior.

Run from the API repository, after stopping the running API if its output
files are locked:

```powershell
dotnet build .\src\NexMed360.sln
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj
```

Patient-record build and automated regression results: **Passed, reported by
the user on 5 November 2026**. No test count, coverage percentage or HTTP
verification is inferred from that report. For the runtime checks below, restart normally with initialization disabled.
Use two different synthetic patient accounts A/B with clinic records, plus a
doctor account. Use each account's own token and verify the HTTP response body
as well as status. Do not paste tokens into shared evidence.

| Check | Expected |
| --- | --- |
| Patient A: `GET /api/v1/Patient/GetClinicRecords/{A-patient-id}` | 200; only A's records |
| Patient A: `GET /api/v1/Patient/GetClinicRecord/{A-record-id}` | 200; A's record/prescriptions |
| Patient A repeats both calls with B's IDs | 404; no clinical data |
| Repeat A/B ownership checks with patient B's token | B succeeds for B and receives 404 for A |
| Patient reads own empty record list; another patient reads that list | 200 empty list for owner; 404 for other patient |
| Unknown valid GUIDs; malformed GUIDs | Existing 404; existing 400 |
| No token; token for a role other than Patient/Doctor | 401; 403 |
| Doctor views records through the existing portal | Existing read behavior retained |

Patient-record HTTP authorization regression results: **Not run**. These
checks remain pending for end-to-end security evidence.


## Account-update authorization regression checks

`POST /api/v1/Users/UpdateUser` remains the portal's self-service account-edit
route. Its DTO and response shape are unchanged. The controller passes the
authenticated user ID separately from the DTO; `UserService` rejects a missing
caller ID with 401 and a different submitted target ID with 403 before any
lookup, duplicate-email check or mutation. The account query uses the caller
ID. String IDs are compared exactly, without parsing or changing them.

This route permits every signed-in role to update its own account and grants
no cross-account administrator override. Administrative management would need
an explicitly scoped operation. This bounded fix does not change the existing
email validation, login/email normalization or account-registration behavior.

The existing approved test dependencies are reused. Account tests cover caller
forwarding, missing identity, changing another user's ID, case-sensitive string
identity, unchanged target data, successful self-update, and existing not-found
and duplicate-email responses. Synthetic test users and roles exist only in
isolated EF Core InMemory stores. Guard tests also use an unconfigured context
to ensure rejection happens before accessing storage or other dependencies.

Account-update build and automated regression results: **Passed, reported by
the user on 5 November 2026**. The HTTP checks below remain unrun. For manual
checks, use synthetic users A/B:

| Check | Expected |
| --- | --- |
| A edits their own name/phone through the portal, then reloads the account page | 200; changes persist |
| A calls `POST /api/v1/Users/UpdateUser` with B's `id` and a changed `firstName` | 403; neither account changes |
| Superuser sends another account's `id` to this self-service route | 403; target unchanged |
| Call the update route without a token | 401; no account changes |

Account-update HTTP regression results: **Not run**. Run the pending
patient-record ownership checks as part of the same manual security review.


## Booking-eligibility regression checks

`POST /api/v1/Appointments/Create` now checks the existing session/slot flags
before constructing an appointment or changing slot availability. It rejects
inactive sessions, unavailable slots and slots with `BookingAllowed=false`
using the existing `APPT_DoctorUnavailable` error category (409). The route,
DTO, successful response and missing-slot/session responses are unchanged.
An eligible booking still saves the appointment and marks its slot unavailable
in the same `SaveChangesAsync` call.

The new tests reuse the approved EF Core InMemory/xUnit dependencies. They
cover all seven ineligible combinations of the three flags, unchanged data
when rejected, successful booking with the existing mapper, a sequential
repeat booking and the existing missing-slot response. These tests do not
establish relational transaction behavior or concurrent-request protection.
Booking-eligibility build and automated results: **Passed, reported by the
user on 5 November 2026**. Re-run after subsequent changes:

```powershell
dotnet build .\src\NexMed360.sln
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj
```

Manual checks on synthetic appointments: an eligible slot returns 201; a
second sequential request for that slot returns 409 with no extra appointment.
An inactive session or a slot with booking disabled returns 409 and leaves
both appointment count and slot flags unchanged. HTTP results: **Not run**.

Eligibility checks alone do not prevent concurrent booking; the database
constraint below provides that protection once its migration is applied.
Patient/doctor schedule-overlap rules, duplicate slot generation, booking
cutoffs/time zones and further booking authorization remain separate work.


## Concurrent-booking protection

Migration `20261105000100_EnforceSingleNonCancelledAppointmentPerSlot` adds
`UX_Appointments_NonCancelledSlot`, a unique index over `SessionSlotId` where
`Status <> 5` (`Cancelled`). The existing non-unique index remains. Once the
migration is applied, at most one non-cancelled appointment can occupy a slot.
`CancelRequested`, `Completed` and all other existing non-cancelled statuses
hold it. Cancelled history remains and does not prevent rebooking. IDs, routes,
DTOs, columns and package dependencies are unchanged.

Only a PostgreSQL unique violation for this named index becomes the existing
409 booking-conflict response. After a failed save, the service detaches the
failed appointment and reloads its slot. Other database failures keep their
existing handling. Appointment insertion and slot unavailability remain in the
same EF `SaveChangesAsync` transaction, so a failed insert rolls back that save.
See [EF transaction behavior](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
and [PostgreSQL partial indexes](https://www.postgresql.org/docs/current/indexes-partial.html).

Verification before the appointment-read change, reported by the user on
5 November 2026: build passed; default suite 51 passed, 0 failed, 8 skipped;
separate PostgreSQL run 8 passed, 0 failed, 0 skipped. The user also reported
the application migration applied successfully. HTTP checks remain **Not run**.
Re-run the current build/suite after the appointment-read changes below.
Existing duplicates would block migration; no records are automatically removed
or rewritten.

### Build and database-independent checks

```powershell
Set-Location 'D:\Repos\NexMed360 Old\NexMed360-API'
dotnet build .\src\NexMed360.sln
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj
```

Without PostgreSQL test configuration, integration cases are reported as
**skipped**. The remaining tests compare the EF snapshot/model, inspect both
indexes, exercise the specific conflict handler, and verify unrelated database
errors are not mapped to a booking conflict. A simulated exception is not
proof of PostgreSQL locking or rollback.

### Opt-in PostgreSQL checks

Manually create a separate empty database named `nexmed360_booking_tests` on
your intended disposable test server. Set its connection string locally:

```powershell
$env:NEXMED360_BOOKING_TEST_POSTGRES = '<connection string with Database=nexmed360_booking_tests>'
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj --filter 'Category=PostgreSQL'
Remove-Item Env:\NEXMED360_BOOKING_TEST_POSTGRES
```

These tests refuse any other database name. They never read the application's
connection string. They require that database to exist, apply the migrations
there, and add isolated synthetic users/sessions/slots/appointments with unique
IDs. Test data is retained between runs. They do not delete or recreate a
database. Invalid configuration or a connection/migration failure fails the
configured tests; only absent configuration causes a skip.

The PostgreSQL cases synchronize two independent service contexts immediately
before saving, so both have already read the available slot. Expected: one
success, one 409, exactly one saved appointment and an unavailable slot. Other
cases cover all six non-cancelled statuses blocking another booking, rollback
of the attempted slot mutation, and retained cancelled history allowing a new
booking. PostgreSQL results: **8 passed, 0 failed, 0 skipped**, reported by
the user on 5 November 2026. Record passed/failed/skipped counts separately
on subsequent runs; skipped cases do not establish concurrency safety.

### Apply to the configured local application target

After reviewing the build/tests and confirming the application's disposable
database target, stop the API and explicitly request migration, without seeding:

```powershell
dotnet run --no-build --project .\src\NexMed360.API.Old --launch-profile http -- --DatabaseInitialization:MigrateOnStartup=true --DatabaseInitialization:SeedOnStartup=false
```

If the index cannot be created because duplicates exist, stop and inspect the
reported conflict; no automatic cleanup is included. A read-only diagnostic is:

```sql
SELECT "SessionSlotId", COUNT(*)
FROM "Appointments"
WHERE "Status" <> 5
GROUP BY "SessionSlotId"
HAVING COUNT(*) > 1;
```

After migration succeeds, stop and restart normally with both initialization
flags disabled. The invariant is **not installed** on an application database
until this migration succeeds. Confirm normal booking, two competing requests
for the same slot (201/409 with one appointment), and cancellation/rebooking
through the existing portal. Manual HTTP results: **Not run**.


## Appointment-by-ID read authorization

`GET /api/v1/Appointments/View/{appointmentId}` now applies the existing list
scopes to individual reads. The controller passes the authenticated user ID and
supported token roles separately from the requested appointment ID. The service
filters the query before loading or mapping the appointment:

| Caller role | Permitted relationship |
| --- | --- |
| Patient | Appointment's patient profile belongs to the caller |
| Doctor | Appointment's assigned doctor profile belongs to the caller |
| Receptionist | Caller's receptionist profile belongs to the appointment's hospital |

Both a token role and the matching stored relationship are required. Multiple
roles allow their combined scopes. A stored profile without its matching token
role grants no extra access, and the booking creator alone has no access grant.
Unrelated/missing targets return the existing appointment-not-found 404 without
appointment data. Missing caller IDs return 401; malformed appointment IDs keep
the existing 400. The route, DTO, response shape and string identities remain
unchanged. No dependencies, schema or migrations change for this item.

The existing test dependencies are reused. Added cases cover own patient and
assigned-doctor reads, same/other-hospital receptionists, an unassigned doctor
at the same hospital, an unrelated patient who created the booking, missing
identity, missing/incorrect token roles, multiple roles, exact string IDs,
missing/malformed targets and controller forwarding from token claims.
Appointment-read build and automated results: **Passed, reported by the user
on 5 November 2026**. Re-run the build/test commands above after the status
changes below. No PostgreSQL translation or HTTP verification is inferred from
the in-memory/controller tests.

Manual checks: patient A reads their own appointment (200), then requests B's
ID (404). The assigned doctor and same-hospital receptionist get 200; another
doctor or a receptionist from another hospital gets 404. Missing token gets
401. Verify authorized response data matches the existing portal, including
appointment detail and the doctor's encounter-resume view. Results: **Not run**.

This item changes individual reads only. Status-change authorization is
covered below. Cancellation authorization, booking-actor permissions and the
other previously identified clinical/data-ownership gaps remain subsequent
bounded work.


## Appointment status-change authorization

The existing Confirm, Complete, ConfirmArrival and ConfirmInProgress routes
now forward the token's user ID and roles to `ChangeStatus`. They retain their
URLs, HTTP methods, DTO/response shape and action-selected target status.

| Action | Permitted scope |
| --- | --- |
| Confirm, Complete, ConfirmArrival | Assigned doctor or receptionist at the appointment's hospital |
| ConfirmInProgress | Assigned doctor only |

Both the applicable token role and the stored relationship are required.
Patient read access never grants status-change permission. A receptionist
scope cannot grant encounter-start permission even when the same token also
contains a Doctor role. Missing caller ID returns 401; an unsupported role
returns 403; an unrelated or missing appointment returns the existing 404.
Authorized access to a cancelled appointment retains the existing 410.

When resetting `CancelRequested` to another status, removal of an existing
cancellation request and the appointment update are submitted in one
`SaveChangesAsync` call. A missing cancellation row is handled without a null
removal. Other current transition behavior remains; this item does not add a
complete transition state machine or resolve concurrent status/cancel races.

Added tests cover the allowed actions, cross-hospital/unassigned staff denial,
patient and mixed-role boundaries, missing identity, cancelled/missing/invalid
targets, caller forwarding on all four routes, unchanged data on rejection,
one-save cleanup and a simulated failure that must not persist cleanup alone.
The existing approved test dependencies are reused. The save-boundary tests
use InMemory and do not establish PostgreSQL-specific rollback behavior.
Status-change build and automated tests: **Passed, reported by the user on
5 November 2026**. Re-run after subsequent changes:

```powershell
dotnet build .\src\NexMed360.sln
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj
```

Manual checks on synthetic appointments: assigned doctor/front-office actions
succeed in their permitted scope; another doctor or other-hospital receptionist
gets 404 with no status/request changes. Patient tokens get 403 on these staff
routes. Only the assigned doctor can start an encounter. Confirm an appointment
with an existing cancellation request and check that the successful reset
removes the request while keeping the slot booked. Results: **Not run**.

No schema, migration or dependency changes are included. Cancellation endpoint
authorization and booking-actor permissions remain subsequent bounded items.


## Cancellation authorization and requester-ID correction

`POST /api/v1/Appointments/Cancel` now requires the token role and the existing
appointment relationship: its patient, assigned doctor, or a receptionist from
its hospital. The controller forwards the token identity/roles independently
of the request DTO. Unrelated/missing targets return the existing 404 before
any mutation; missing caller IDs return 401. Existing malformed/cancelled target
and duplicate-request responses remain 400/410/409 respectively.

The existing cancellation flow is retained:

- An authorized creator cancels immediately and releases the slot.
- An authorized non-creator creates a cancellation request without releasing it.
- The authorized creator can confirm that request, retaining its original
  requester and reason. A missing request row is recreated for that action.
- A repeated non-creator request returns the existing conflict response.

The request, appointment state and any slot release are saved together. The
route, DTO and response shape are unchanged. Cancellation/status races and a
complete lifecycle state machine remain outside this bounded fix.

`CancellationRequest.RequestedById` is now a required string, matching the
application's string user IDs. The service stores the exact authenticated ID
without `Guid.Parse`. Migration `20261105000200_StoreCancellationRequesterAsString`
changes the existing column from UUID to text using its existing UUID text
representation. It does not change user/account IDs, delete history or add a
foreign key or dependency. The earlier appointment uniqueness index remains.
See [PostgreSQL column-type conversion](https://www.postgresql.org/docs/current/sql-altertable.html).
The reverse migration rejects non-UUID string IDs; it does not delete or replace
them to force a rollback.

Build, unit tests, PostgreSQL tests, the new migration and HTTP checks for this
change: **Not run**. The model-comparison test now also checks the required text
requester property and the updated migration snapshot. Unit cases cover actor
scope, existing cancellation outcomes, non-GUID IDs, caller forwarding, unchanged
state on rejection, one-save mutation and simulated save failure.

Run the build/default suite from the API repository:

```powershell
dotnet build .\src\NexMed360.sln
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj
```

Without PostgreSQL configuration, the expected integration skip count is now
**10**. Reuse the separate `nexmed360_booking_tests` target from the PostgreSQL
instructions above and rerun `--filter 'Category=PostgreSQL'` after rebuilding.
The eight earlier cases remain. Two new cases exercise string requester IDs
through a receptionist request, patient-creator confirmation and rebooking,
and the actual migration's conversion SQL on a session-local temporary table
with an existing UUID reference. The conversion test also checks that an
attempted UUID rollback refuses an opaque string without discarding the two
probe rows. No public application table is downgraded for that probe.

After build/test review, apply the new migration to the confirmed disposable
application target using the explicit migrate-on-startup command above with
seeding disabled, then restart normally. The new cancellation code requires
this column migration before use. Migration application: **Not run**.

Manual checks: unrelated patient/doctor/other-hospital receptionist gets 404
with no request, appointment or slot changes. Authorized non-creator request
keeps the slot occupied; creator confirmation cancels and releases it; a new
booking then succeeds. Check existing requester references and the portal's
response behavior. HTTP results: **Not run**. Booking-actor permissions and the
other previously identified clinical/data-ownership gaps remain separate work.

---

# 1. Architectural Overview & Philosophy

The NexMed 360 backend is built on a Clean Architecture (CA) foundation, enforced by Domain-Driven Design (DDD) tactical patterns. This approach ensures maximum maintainability, testability, and stability for a complex healthcare application.

## 1.1. Core Architectural Principles

<table style="border: 1px solid #007ec2ff">
    <tr style="background-color: #007ec2ff">
        <th>Principle</th>
        <th>Description</th>
        <th>Justification</th>
    </tr>
    <tr>
        <td>Dependency Rule (CA)</td>
        <td>Source code dependencies must only point inward <code>(API → Application → Domain)</code>. No inner layer may know about the outer layers.</td>
        <td>Guarantees the core business logic (Domain) is completely decoupled from external technology (EF Core, Web Frameworks).</td>
    </tr>
    <tr>
        <td>Single Source of Truth (DDD)</td>
        <td>The Domain Model is the exclusive source of business rules (invariants) and object state.</td>
        <td>Prevents scattered logic; ensures all rules are enforced centrally.</td>
    </tr>
    <tr>
        <td>Separation of Concerns (SOLID)</td>
        <td>Application is split into logical and physical projects, each with a single, clear responsibility.</td>
        <td>Maximizes testability and scalability, allowing independent development and deployment considerations.</td>
    </tr>
    <tr>
        <td>Persistence Ignorance</td>
        <td>The Domain layer contains zero references or knowledge of how data is saved.</td>
        <td>The Domain is designed for the business problem, not the database structure.</td>
    </tr>
</table>

## 1.2. Tech Stack and Infrastructure

- **Framework:** `.NET 10 (ASP.NET Core)`
- **ORM:** `Entity Framework Core (EF Core)`
- **Containerization:** `Docker`
- **Database:** `Postgre SQL`
- **CI/CD:** `GitHub Actions`
- **Hosting Infrastructure:** `Azure App Services | Azure Container Apps`
- **Documentation:** `Swagger / OpenAPI`
- **Json Serialization:** `System.Text.Json`

</br>

# 2. Project Structure and Responsibilities

The architecture is implemented using four physical projects. Dependencies must strictly adhere to the inward rule.

<table style="border: 1px solid #007ec2ff">
    <tr style="background-color: #007ec2ff">
        <th>Project</th>
        <th>Name (.csproj)</th>
        <th>Dependencies (Points To)</th>
        <th>Core Responsibilities</th>
    </tr>
    <tr>
        <td>Domain</td>
        <td><code>NexMed360.Domain</code></td>
        <td>None</td>
        <td>Business Rules: Defines Entities, Value Objects, Domain Events, and Repository Interfaces (Contracts).</td>
    </tr>
    <tr>
        <td>Application</td>
        <td><code>NexMed360.Application</code></td>
        <td><code>NexMed360.Domain</code></td>
        <td>Use Cases & Policies: Contains Specifications, Handlers, DTOs, and Application Contracts (e.g., <code>IEmailSender</code>, <code>IDomainEventDispatcher</code>).</td>
    </tr>
    <tr>
        <td>Infrastructure</td>
        <td><code>NexMed360.Infrastructure</code></td>
        <td><code>NexMed360.Application</code></td>
        <td>Implementations: Contains EF Core Data Models, Concrete Repository implementations, UnitOfWork implementation, and external service implementations (e.g., <code>SmtpEmailSender</code>).</td>
    </tr>
    <tr>
        <td>Presentation</td>
        <td><code>NexMed360.API</code></td>
        <td><code>NexMed360.Infrastructure</code></td>
        <td>Entry Point: Controllers, Dependency Injection Setup, HTTP Routing, Authentication/Authorization Filters, and Error Handling Middleware.</td>
    </tr>
</table>

## 2.1. Application Architecture Diagram
![Application Architecture](assets/Application_Architecture.png "Application Architecture")

</br>

# 3. Domain-Driven Design (DDD) Patterns

These patterns ensure the domain is rich, robust, and maintains transactional integrity.

## 3.1. Entities and Value Objects (VOs)

<table style="border: 1px solid #007ec2ff">
    <tr style="background-color: #007ec2ff">
        <th>Pattern</th>
        <th>Purpose</th>
        <th>Implementation Guide</th>
    </tr>
    <tr>
        <td>Entity <code>(Patient, Appointment)</code></td>
        <td>Object with identity <code>(Guid Id)</code>. Inherits from <code>EntityBase</code>. Used for core business objects tracked over time.</td>
        <td>Must use private/protected setters. State changes must occur via public, meaningful methods <code>(patient.UpdateAddress(newAddress))</code>.</td>
    </tr>
    <tr>
        <td>Value Object <code>(Address, Money)</code></td>
        <td>Object defined purely by its attributes. Immutable and lacks identity. Inherits from <code>ValueObjectBase</code>.</td>
        <td>Must be immutable. New state requires creating a new instance. Used for descriptive data that should be treated as a single unit.</td>
    </tr>
</table>

## 3.2. Aggregates and Aggregate Roots (AR)

- **Definition:** An Aggregate is a transactional boundary. The Aggregate Root (AR) is the single Entity that controls access to all objects within that boundary.

- **Example:** Appointment is the AR; Treatment is an internal/child Entity.

- **Rules Enforcement:** The AR is the gatekeeper. All invariants (business rules) that apply across the entire aggregate (e.g., "Cannot modify a confirmed appointment") are checked and enforced by the AR's methods.

- **Implementation:** The AR tracks all Domain Events in a transient list.

## 3.3. Domain Events

- **Definition:** An object representing an immutable fact that occurred in the domain (e.g. `AppointmentCompletedEvent`).

- **Flow & Implementation:**

  - AR method (`appointment.Complete()`) calls `AddDomainEvent(event)`.

  - The Unit of Work (in the Infrastructure layer) intercepts the AR's list of events before `SaveChangesAsync()`.

  - The UoW dispatches the events via the Event Dispatcher (an Application contract) to the relevant Application Handlers.

  - Handlers perform decoupled side effects (e.g., sending emails).

</br>

# 4. Persistence and Transaction Management

## 4.1. Model Separation: Domain vs. Data

<table style="border: 1px solid #007ec2ff">
    <tr style="background-color: #007ec2ff">
        <th><p><strong>Model</strong></p></th>
        <th><p><strong>Project</strong></p></th>
        <th><p><strong>Purpose &amp; Role</strong></p></th>
    </tr>
    <tr>
        <td><p><strong>Domain Model</strong></p></td>
        <td><p><code>NexMed360.Domain</code></p></td>
        <td><p><strong>Rich</strong> object model. Contains collections (<code>List&lt;Prescription&gt;</code>) and private setters.</p></td>
    </tr>
    <tr>
        <td><strong>Data Model  (Entity)</strong></p></td>
        <td><code>NexMed360.Infrastructure</code></p></td>
        <td><p><strong>Anemic</strong> object model. Contains public setters for EF Core mapping. Flat structure for database  columns.</p></td>
    </tr>
</table>

> Translator: The Repository Implementation is responsible for converting the Data Model retrieved from the database into the rich Domain Model expected by the Application layer.

## 4.2. Repository Pattern

- **Interface (`IRepository`):** Defined in `NexMed360.Domain`.

- **Implementation:** Lives in `NexMed360.Infrastructure`. Injects the shared `NexMedDbContext` instance.

## 4.3. Unit of Work (UoW)

- **Interface (`IUnitOfWork`):** Defined in `NexMed360.Domain`. Contains `CommitAsync()`.

- **Implementation:** Lives in `NexMed360.Infrastructure`. Wraps the shared `NexMedDbContext` instance.

- **Critical Role:** Ensures atomicity. `CommitAsync()` is the central point where:

    - Domain Events are collected and dispatched.

    - `context.SaveChangesAsync()` is called, committing all pending changes (across all Repositories) in a single database transaction.

</br>

# 5. Application and Presentation Layer Details

## 5.1. Application Layer (Services and Handlers)

- **Structure:** Uses the repository pattern. Handlers inject Repositories and the UoW.

- **Decoupling Side Effects:** Handlers for Domain Events (e.g., `SendAppointmentConfirmationHandler`) reside here, defining the application's policy in response to a business fact. They depend only on Application Contracts (like `IEmailSender`).

## 5.2. Dependency Inversion Principle (DIP) for External Services

- **Contract:** The interface (e.g., `IEmailSender`, `ISmsSender`) is defined in the Application project (the inner layer).

- **Implementation:** The concrete service (e.g., `SmtpEmailSender`, `TwilioSmsSender`) is implemented in the Infrastructure project (the outer layer).

- **Benefit:** Allows swapping external services (e.g., changing from Twilio to Vonage) without altering the core business policies in the Application or Domain layers.

## 5.3. Error Handling and API

- **Api Response:** All Api endpoints should strictly respond using a standard `Result<T>` class. refer the code below.
```
public class Result<T>
{
    public bool IsSuccess { get; set; }
    public ApiError? Error { get; set; }
    public T? Data { get; set; }

    public static implicit operator Result<T>(T? data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static implicit operator Result<T>(ErrorCode errorCode) => new()
    {
        IsSuccess = false,
        Error = ErrorRegistry.GetError(errorCode)
    };

    public static implicit operator Result<T>(ApiError apiError)
    {
        var errorCode = apiError.Code ?? ErrorCode.UnknownError;
        var baseError = ErrorRegistry.GetError(errorCode);
        var title = apiError.Title ?? baseError.Title;
        var httpStatus = apiError.HttpStatus ?? baseError.HttpStatus;
        var description = apiError.Description ?? baseError.Description;
        
        return new Result<T>
        {
            IsSuccess = false,
            Error = new(errorCode, title, description, httpStatus),
        };
    }
}
```
- **Error Flow:** Custom Application errors are used.

    - **class `ApiError`:** Contains error specific details (e.g., error code, title, descriptions, http status).

    - **class `ErrorRegistry`:** Holds a collection of pre-defined ApiErrors with specific details.

- **API Middleware:** The `NexMed360.API` project must implement global Exception Handling Middleware to intercept all custom exceptions and translate them into appropriate HTTP status codes with a standardized `Result<T>` JSON payload.

</br>

# 6. Deployment Architecture
The system is designed as a cloud native application to be hosted on Microsoft Azure.

!["Deployment Architecture"](assets/Deployment_Architecture.png "Deployment Architecture")

As illustrated in the Deployment architecture diagram above, the deployment pipeline
and runtime environment consist of:
- **CI/CD Pipeline:** Developers push code to GitHub, triggering GitHub Actions.
This pipeline runs unit tests, performs code analysis via SonarCloud, builds
Docker container images, and pushes them to the Azure Container Registry
(ACR).

- **Runtime Environment:** The application runs on Azure Container Apps (ACA)
within a managed Virtual Network. This environment hosts both the API
container and the Auth Service container behind an Ingress Controller with
traffic routing rules.

- **Data Persistence:** A PostgreSQL Flexible Server is deployed in a private
subnet, ensuring that the database is not exposed directly to the public
internet. Connection between the API and Database is secured via private
endpoints.

- **Security & Monitoring:** Azure Key Vault manages secrets and configuration
strings, while Log Analytics workspaces aggregate logs from all containers for
monitoring.


# Additional Notes

> All transactional services (DbContext, Repositories, UoW) must be registered with a Scoped lifetime.

> App configurations have been moved to the Azure App Configuration service. The `appsettings.json` file may be used to configure required settings for local development; however, ensure that these settings are not committed to the repository.

