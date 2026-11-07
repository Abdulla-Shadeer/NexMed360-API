# NexMed360 API

The existing portal API runs from `src/NexMed360.Api` on .NET 10 and PostgreSQL.
The solution has four production projects and the existing regression-test project.

| Project | Responsibility | Project dependencies |
| --- | --- | --- |
| Domain | Clinical entities, appointment/record/session behavior, existing enums and roles | None |
| Application | Feature folders, unchanged DTOs, repository contracts and use-case workflows | Domain |
| Infrastructure | EF configurations/repositories, read queries, Identity/JWT, initialization | Application |
| Api | Existing controllers, HTTP validation/errors, configuration and composition | Application, Infrastructure |

The existing DTO namespaces, HTTP verbs/routes, authorization attributes, JSON
contracts and Identity/JWT settings are retained. HTTP principals become a plain
caller context at the controller boundary. Infrastructure types do not appear in
Domain or Application contracts. Application is organized into Accounts,
Appointments, DoctorSessions, Hospitals, Patients, Search, Overview and Shared
folders. The existing DTO contents and namespaces remain unchanged.

Clinical entities live in Domain and are mapped directly by EF using per-model
Infrastructure configurations. They contain no Identity navigations or database
generation annotations. Navigations remain within these aggregate boundaries:

| Root | Internal child entities |
| --- | --- |
| Appointment | CancellationRequest |
| ClinicRecord | Prescription |
| DoctorSession | SessionSlot, SessionHistory |
| Patient | VisitHistory |
| Hospital, Doctor, HospitalAdmin, Receptionist | None |

Separate roots reference each other by ID. EF configurations preserve the same
navigation-free foreign keys, indexes and delete behaviors. Doctor is independent
of Hospital; its existing `HospitalId` still represents today's assignment.
Multiple-hospital assignments require a later explicit model/schema change.

Domain methods implement the existing booking, cancellation, record creation,
hospital update, slot generation and session-history behavior. Application owns
use-case orchestration, validation and all response DTO conversion. Repositories
accept/return domain models and contain no DTO contracts. Independent roots have
separate write repositories, including Doctor, ClinicRecord and staff profiles. Infrastructure query
interfaces return plain statistics or joined read data, including account display
fields; Application composes the unchanged portal responses. The obsolete
AutoMapper dependency has been removed; no replacement library was added.

`User : IdentityUser` remains in Infrastructure. Clinical models retain the same
string `UserId`, `CreatedByUserId` and `RequestedById` values. The existing account
table and database foreign keys remain; account display data is joined inside
Infrastructure. No new account table, provider-ID mapping system or authentication
migration is introduced. Future Duende work must preserve or explicitly map these
stable IDs and separately verify its login flow, claims and token validation.

Use these two account services from application features:

| Service | Operations |
| --- | --- |
| `IAuthService` | Register accounts/roles and log in |
| `IUserService` | Read/update account demographics and retrieve the existing overview |

Only their implementations use the internal `IIdentityGateway` boundary. Its
Infrastructure implementation contains Identity account/password operations and
JWT creation; the separate account and credential adapters have been removed.
Incoming token validation remains in the existing ASP.NET authentication middleware.
Patient/staff registration calls `IAuthService` inside the existing shared local
transaction, so account, role and profile writes still commit together.
Transaction interfaces live under `Shared/Persistence`; they are persistence
plumbing rather than another authentication service. Booking and cancellation
retain the corrected atomic database writes.

## Configuration and database

Local Development configuration disables Azure App Configuration and startup
migration/seeding. Required keys are:

- `ConnectionStrings:DefaultConnection`
- `Auth:Jwt:Issuer`, `Audience`, `Signing-Key`, `Validity`
- `Auth:SuperUserPassword` when seeding

Development settings retain the existing local JWT configuration and connection
fields, with the database name changed to `nexmed360_migrated`. Existing user
secrets still apply because the host retains its user-secrets identity; environment
and command-line values can override the target. Set an explicit local connection
to the intended new database before initialization. Do not share credentials.

Infrastructure contains one fresh `InitialCreate` migration, generated from the
corrected model. It includes the filtered unique slot index, text cancellation
requester IDs, existing keys/relationships/timestamp mappings and surviving
historical database defaults. **Apply it only to a new empty database.** Existing
application databases and their migration histories are not upgrade targets for
this migration. Create the new databases manually; no database deletion is needed.
The subsequent domain refactor has no relational model differences from that
initial schema and requires no additional migration or database reset.

The EF design-time factory uses an unreachable placeholder so model generation
and comparison do not read application configuration or connect to a database.
Use the explicit startup switches below for application initialization. A plain
`dotnet ef database update` cannot use the Development target through this factory.
The existing EF Package Manager Console tools remain at version 10.0.1 in
Infrastructure; select that project for migration-generation commands.

For Azure configuration, enable `AzureAppConfiguration:Enabled` and supply
`APP_CONFIG_ENDPOINT` plus an authorized Azure identity. Environment/CLI values
retain precedence. Startup migration and seeding remain separate explicit opt-ins.

## Build and database-independent checks

Run from the API repository in PowerShell:

```powershell
dotnet restore .\src\NexMed360.sln
dotnet build .\src\NexMed360.sln --no-restore
Remove-Item Env:NEXMED360_MIGRATED_TEST_POSTGRES -ErrorAction SilentlyContinue
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj --no-build
dotnet ef migrations has-pending-model-changes --project .\src\NexMed360.Infrastructure --startup-project .\src\NexMed360.Api --no-build
```

With the PostgreSQL opt-in unset, 11 PostgreSQL cases are skipped intentionally.
The remaining suite includes the existing authorization/eligibility regressions,
39 endpoint definitions, unchanged DTO source contracts, layer dependencies,
service resolution, login/registration workflows and corrected schema comparison.
It also includes domain state-transition checks, aggregate navigation and pure
repository-contract checks, and frozen JSON for 25 full portal response examples.
The examples were captured and verified against isolated pre-refactor code using
synthetic InMemory data and the API's invariant culture. They cover profile,
record, appointment, hospital/member, session/slot, overview and error responses.
The expected JSON explicitly corrects missing member/admin account fields, roles
and history doctor fields; those old null values are not a completeness target.
Nine additional fresh-scope cases assert actual stored values, multi-role membership,
hospital isolation and the unchanged administrator-visibility gate.
DTO source checks run from this checkout. No PostgreSQL connection is required
for those checks. Build warnings are not test or coverage results.

Known restore warnings include the unavailable private CodeArtifact audit feed
and the previously reported OpenAPI/PKCS vulnerabilities. Dependency versions
have not been upgraded as part of this structural change.

## Initialize and run the migrated application

First build/test, then manually create an empty `nexmed360_migrated` database.
Confirm the server, credentials and target locally. The following mutates that
new application database only; replace placeholders locally:

```powershell
$apiProject = '.\src\NexMed360.Api'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=nexmed360_migrated;Username=<user>;Password=<password>'
# Existing Development JWT settings apply. Supply a seed password if not configured.
dotnet user-secrets set 'Auth:SuperUserPassword' '<local password meeting Identity rules>' --project $apiProject
dotnet run --no-build --project $apiProject --launch-profile http -- --AzureAppConfiguration:Enabled=false --DatabaseInitialization:MigrateOnStartup=true --DatabaseInitialization:SeedOnStartup=true
```

Confirm `InitialCreate` applies and initialization completes. The existing seed
creates roles and `superuser@gmail.com` when the relevant tables are empty; it
is not a complete synthetic dataset or a repair of partially populated data.
A listening message alone does not prove successful account creation. Stop with
Ctrl+C and restart with initialization disabled:

```powershell
dotnet run --no-build --project $apiProject --launch-profile http -- --AzureAppConfiguration:Enabled=false --DatabaseInitialization:MigrateOnStartup=false --DatabaseInitialization:SeedOnStartup=false
```

Swagger remains at `http://localhost:5098/swagger`. With both switches disabled,
startup does not migrate or seed. Database-backed endpoints still need the
configured PostgreSQL database. Missing required settings fail with key names
rather than secret values.

## PostgreSQL regression checks

Manually create a separate empty `nexmed360_migrated_tests` database. The test
connection must name that database exactly; it is independent of appsettings and
the application database. Tests apply `InitialCreate` and insert synthetic data;
they do not delete a database. Subsequent runs can reuse this test database.

```powershell
$env:NEXMED360_MIGRATED_TEST_POSTGRES = 'Host=localhost;Port=5432;Database=nexmed360_migrated_tests;Username=<user>;Password=<password>'
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj --no-build --filter 'Category=PostgreSQL'
Remove-Item Env:NEXMED360_MIGRATED_TEST_POSTGRES
```

Expected: all 11 cases execute. They cover competing bookings, all non-cancelled
statuses, cancelled-history rebooking, string requester cancellation/rebooking,
the corrected initial schema and rollback of Identity writes after profile-save
failure. A default test run with skipped cases is not PostgreSQL evidence.

## Existing portal review

Use the sibling `NexMed-360-UI` unchanged. Its ignored `.env.local` should contain:

```dotenv
NEXT_PUBLIC_API_ENDPOINT=http://localhost:5098/api/v1
```

The endpoint is needed at build time too. From the UI repository run `npm ci`,
`npm run build`, then `npm run dev` for manual review at `http://localhost:3000`.
Use the new host and newly initialized application database.

| Journey | Review checks |
| --- | --- |
| Login/account | Seed login; own profile read/update; changing another account ID returns 403 |
| Hospital/staff | Create hospital, assign admin, add doctor/receptionist; verify returned profiles and overview |
| Patient | Register, log in, retrieve profile; registration links the same Identity ID |
| Sessions/booking | Create/update session, generate slots, book eligible slot; inactive/unavailable slots rejected; second active booking conflicts |
| Appointment access | Own patient, assigned doctor and same-hospital receptionist succeed; unrelated actors and missing identity fail |
| Encounter | Assigned doctor starts encounter; authorized staff confirm arrival/completion; unrelated actors cannot mutate it |
| Cancellation | Authorized non-creator requests; creator confirms and releases slot; rebooking retains history |
| Records | Doctor creates record/prescriptions; owning patient reads; another patient cannot read by guessing patient/record IDs |

These are review checks, not claims that every behavior is already correct or
that all security/data-ownership gaps have been resolved. Record actual observed
results and failures, including the previously deferred manual HTTP checks.

## Evidence and remaining academic work

On 5 November 2026, before this restructuring, the user reported the API/UI
builds, portal smoke checks and focused security tests passing. Earlier
PostgreSQL booking checks passed; the requester-ID migration was reported applied.
Those results do not verify the relocated code or fresh initial migration.

For this restructuring, the production API and test project compiled locally.
The first migration review corrected a missing routing registration in its test
fixture and restored the existing EF console-tools package in Infrastructure.
The domain/repository refactor initially passed 149 database-independent tests.
The subsequent aggregate-boundary and Application-mapping batch passed all 152
database-independent tests. These verify all 39 endpoint definitions,
all 47 unchanged DTO sources, framework boundaries, aggregate navigations,
repository contracts, transaction handling and domain behavior. At that stage,
all 25 full response examples matched the captured pre-refactor JSON, including
some incomplete display fields later corrected below. The model comparison has
zero relational schema differences and no pending migration changes.

The subsequent authentication simplification compiled and passed all 157
database-independent tests. Five added cases exercise the real Identity adapter
with synthetic InMemory storage: registration/profile data, validated JWT claims,
invalid credentials, stable IDs when adding a role, duplicate-role errors and weak
password rejection. Existing login, profile authorization, registration-transaction,
response-contract and model-comparison checks still pass. No endpoint, DTO, schema,
frontend or dependency change was needed for this batch.

The response-completeness correction compiled and passed all 166
database-independent tests. `Hospital/GetMembers` now loads account names/contact
fields, actual roles and each member's hospital name at both response levels.
Administrator demographics are populated in user, hospital-detail and assignment
projections; hospital-admin list roles are loaded too. `DoctorSession/GetHistory`
loads doctor names and specialties, as ongoing/single-history projections do.
The snapshot expected values for these fields were updated explicitly from seeded
data, alongside independent assertions that would reject the previous nulls.
Patient, appointment, prescription, session and doctor-search shared projections
were also checked with fresh scopes. Routes, DTO sources, authorization gates,
Identity behavior and schema are unchanged; no migration is needed.

To review, rebuild and restart with initialization disabled using the commands
above, then repeat the reported `Hospital/GetMembers/{hospitalId}` request as its
admin. Also check `Users/GetUser` as an admin, `Hospital/FindById/{id}` as superuser,
and `DoctorSession/GetHistory?SessionId={sessionId}` as doctor. Names and contacts
must match their stored accounts; irrelevant role profiles can remain null.

`ViewClinicRecordDto.HospitalName` still has no durable originating-hospital key
in the current model. It remains null rather than being inferred from a doctor's
current hospital. Existing prescription hospital display still uses that current
assignment and does not establish historical provenance. This requires the
separately agreed originating-hospital ownership work.

The user reported the PostgreSQL suite passing before these later refactors.
The latest supplied log for all 11 cases failed during setup because
`nexmed360_migrated_tests` did not exist on the configured server. Create that
dedicated database manually (or correct its host/port) and rerun those cases;
that log did not reach the regression assertions. No PostgreSQL connection, API startup or frontend change was made
during these refactors. Manual HTTP/portal compatibility and the container build
remain unverified. No deployment or coverage/performance result is claimed.

The submitted System Development report (PDF pp. 3-4) describes inward project
dependencies and Infrastructure EF data models. The current JWT authentication
differs from its Duende design. The Methodology and Design report (PDF p. 7)
also commits to pharmacy fulfilment, labs, referrals, record/prescription
modification and quality targets including 80%+ unit coverage per module and
normal API responses below three seconds. These remain completion/evidence gaps;
recording a limitation does not establish assessor approval.

The next review batch is the agreed bounded FHIR R4 demonstration: retrieve
patient records from an independent synthetic hospital with separate storage and
display them in the existing portal. FHIR, durable originating-hospital ownership,
remaining academic workflows, coverage, performance, deployment and evaluation
work are not completed by this restructuring. The paused newer repository has
not been modified or used as an implementation foundation.
