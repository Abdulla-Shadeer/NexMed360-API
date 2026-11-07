# NexMed360 API

The API runs from `src/NexMed360.Api` on .NET 10 and PostgreSQL.

| Project | Responsibility |
| --- | --- |
| Domain | People, organizations, practitioner assignments, clinical models, and permission names |
| Application | Use cases, DTOs, repository contracts, and response mapping |
| Infrastructure | EF mappings, repositories, Identity/JWT, and initialization |
| Api | HTTP endpoints, validation, errors, and startup |

## Phase 1 models

- `ApplicationUser` links to one `Person`. Names are stored on the person. A person can have both a patient and a practitioner profile.
- A patient can exist without a login. `OrganizationPatient` holds a registration at an organization.
- `Practitioner` is the shared professional identity. Each `PractitionerRole` describes an assignment at one organization and optional location, with professional role, specialty, services, and dates.
- `OrganizationMembership` links an account to an organization. `AccessRole` stores a named permission group; `MembershipAccessRole` assigns it to a membership.
- `Location` represents a site, department, ward, room, bed, or another place. Parent locations belong to the same organization.
- `Encounter` and participants are ready in the schema. Clinical records store their source organization, practitioner assignment, and optional encounter link. The existing appointment/session workflow remains until Phase 5.

Organization-owned rows require `OrganizationId`. Composite foreign keys keep related organization-owned records in the same organization. Saved records cannot change their organization. Independent clinical references use restricted deletion; dependent children are deleted with their parent.

Patient self-history resolves the patient through their account/person link and includes their visible local records across organizations. It does not discover records in external systems.

## Current scope

Phase 1 changes the model, schema, and names in both the API and frontend. Routes now use `Organization`, `PractitionerSchedule`, and `ClinicalRecord` names. Clinical record creation includes `practitionerRoleId`; the server checks that the assignment belongs to the caller and stores its organization.

Account responses include `personId`, `practitionerRoleProfiles`, and `memberships`. The singular profile fields remain as defaults for the current screens. Phase 2 replaces these defaults with explicit workspace selection and permission-based access. Current JWT account roles are temporary; Phase 1 does not complete organization-scoped authorization.

Email delivery, self-registration, onboarding verification, and password recovery remain later phases. Recovery columns are present, but no recovery endpoints or email packages have been added. Keep healthcare code behind the existing account-service boundary when adding a future identity provider.

## Local configuration

Development disables Azure App Configuration and startup migration/seeding. The default development database name is `nexmed360_refactored`. Required settings are:

- `ConnectionStrings:DefaultConnection`
- `Auth:Jwt:Issuer`, `Audience`, `Signing-Key`, and `Validity`
- `Auth:PlatformAdminPassword` when seeding

Environment variables, command-line settings, and existing user secrets can override the database target. Set an explicit local connection before initialization. Do not share credentials.

Infrastructure has one fresh EF-generated `InitialCreate` migration for Phase 1. It is not an upgrade migration for the previous database. Use an empty database, or recreate only the intended local application database. The EF design-time factory uses an unreachable placeholder for model generation and checks.

## Build and check

From the API repository in PowerShell:

```powershell
dotnet restore .\src\NexMed360.sln
dotnet build .\src\NexMed360.sln --no-restore
Remove-Item Env:NEXMED360_REFACTORED_TEST_POSTGRES -ErrorAction SilentlyContinue
dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj --no-build
dotnet ef migrations has-pending-model-changes --project .\src\NexMed360.Infrastructure --startup-project .\src\NexMed360.Api --no-build
```

The default suite uses synthetic in-memory data and model checks. Eleven PostgreSQL tests are skipped unless explicitly configured. The suite covers account identity, record/appointment access, booking constraints, the renamed HTTP/DTO contracts, source-organization projections, both profiles on one person, multiple assignments, required organization IDs, and repeatable seeds.

For PostgreSQL checks, create an empty `nexmed360_refactored_tests` database and set `NEXMED360_REFACTORED_TEST_POSTGRES` to its connection string. The tests require that database name and apply the new initial migration. Do not point them at the previous test database.

## Initialize and run

Create an empty local `nexmed360_refactored` database, then set the target and seed password locally:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=nexmed360_refactored;Username=<user>;Password=<password>'
dotnet user-secrets set 'Auth:PlatformAdminPassword' '<local password meeting Identity rules>' --project .\src\NexMed360.Api
dotnet run --project .\src\NexMed360.Api --launch-profile http -- --AzureAppConfiguration:Enabled=false --DatabaseInitialization:MigrateOnStartup=true --DatabaseInitialization:SeedOnStartup=true
```

The seed creates `platformadmin@example.test`, two demo organizations, their main locations, and three access-role templates per organization. Running it again does not duplicate them. After initialization, restart with both initialization switches set to `false`.

Swagger is at `http://localhost:5098/swagger`. Build the frontend with `npm run build` and run it with `npm run dev`; its `NEXT_PUBLIC_API_ENDPOINT` must point at this API version.

## Phase 1 verification

Verified on 7 November 2026: API solution build; 174 passing database-independent tests and a follow-up run with all 11 PostgreSQL tests passing; EF model matches its migration; frontend TypeScript check and full production build, including lint.

PostgreSQL regressions ran only against `nexmed360_refactored_tests`. The rollback assertion uses stored `PersonId` joins and checks account, person, and patient rollback. The application database, API host, and browser workflows were not exercised. Seed behavior remains verified with in-memory tests only. Existing package/audit and code-analysis warnings remain; no dependency upgrade was made.
