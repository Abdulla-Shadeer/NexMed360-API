# NexMed360 API

The API runs from `src/NexMed360.Api` on .NET 10 and PostgreSQL.

| Project | Responsibility |
| --- | --- |
| Domain | People, organizations, practitioner assignments, clinical models, and permission names |
| Application | Use cases, DTOs, and repository contracts |
| Infrastructure | EF mappings, repositories and their response reads, Identity/JWT, and initialization |
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

Phase 2 adds organization workspaces and permission checks to the Phase 1 model. The existing Identity/JWT login remains in place. User responses contain basic account details. Access decisions use current database grants and the selected workspace; JWT job-role claims do not grant application access.

After login, call `GET /api/v1/Workspace` for the small list of available workspace keys, names, and modes. `GET /api/v1/Workspace/Current`, with the chosen key, returns that workspace's permissions and IDs. `GET /api/v1/Patient/Me` returns own-patient details when a patient screen needs them. Send the selected `key` in the `X-Workspace` header on business requests. The server resolves that key against the authenticated account on every request, including active membership, current permissions, assignment ownership/dates, and location scope. A missing, revoked, or unauthorized workspace returns 403.

- `patient`: only the linked patient's data; no organization membership is needed.
- `work:<organizationId>`: organization work within the member's grants and locations.
- `work:<organizationId>:<practitionerRoleId>`: the member's active practitioner assignment. Clinical writing and session execution require this assignment.
- `platform`: platform administration for accounts with the stored platform administrator flag. This mode does not grant clinical access.

`GET /api/v1/Patient/MyClinicalRecords` returns only the signed-in patient's visible records across local organizations. It takes no patient ID. Staff endpoints use the selected organization, and patient-mode booking accepts only the linked patient.

`Workspace/Permissions` lists application permissions. `Workspace/Roles` supports listing, creating, and updating custom roles with `roles.manage`. `Workspace/Members`, `MemberRoles`, and `Locations` support membership editing with `members.manage`; `PUT Workspace/Members/{id}` saves active status, location, and access-role IDs. References must belong to the current organization. Members with `roles.manage` can define groups using any application permission. Members with `members.manage` can assign organization roles and manage staff access. They do not need the clinical permissions they administer. These administration permissions do not themselves allow reading records, starting encounters, or issuing prescriptions. A location-restricted workspace cannot edit shared roles or add staff templates outside its scope.

When assigning an initial administrator in platform mode, choose any extra grants explicitly. The basic administrator grants cover organization details, members, roles, and schedules. They do not include clinical records. The optional `permissions` list grants additional work permissions explicitly; it is not a limit on which staff permissions the administrator can manage.

Email delivery, self-registration, onboarding verification, and password recovery remain later phases. Recovery columns are present, but no recovery endpoints or email packages have been added. Keep healthcare code behind the existing account-service boundary when adding a future identity provider.

Application database reads and writes go through repository contracts. `Infrastructure/Repositories` contains feature reads, projections, and writes, including search, overview, user, workspace, and access management. There is no separate query-store or portal graph loader. Identity keeps its existing credential/store operations; startup and seeding remain infrastructure tasks. Clinical response fields stay the same, while account, member-list, organization, and workspace responses are intentionally smaller. Organization administrators use their dedicated endpoint.

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

The default suite uses synthetic in-memory data, PostgreSQL query translation checks, and model checks. Eleven PostgreSQL tests are skipped unless explicitly configured. The suite covers account identity, record/appointment access, booking constraints, the renamed HTTP/DTO contracts, source-organization projections, both profiles on one person, multiple assignments, required organization IDs, and repeatable seeds.

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


## Phase 2 verification

Verified on 8 November 2026: 194 passing API tests, including workspace authorization, fresh grant/membership checks, delegation limits, location scope, patient self-history, scoped clinical writing, and PostgreSQL query translation. The EF model still matches the existing initial migration. Eleven database tests were skipped because a dedicated PostgreSQL test connection was not configured.

The frontend's seven access regressions and full production build, including TypeScript and lint, passed. No application database or browser journey was run. Existing dependency and code-analysis warnings remain.


## Service results

Services can return data, an error code, or an `ApiError` directly. `Result<T>` converts data to success and errors to failure:

```csharp
if (patient == null) return ErrorCode.USR_NotFound;
if (!canRead) return ApiError.From(ErrorCode.SYS_Unauthorized, "You cannot read this patient.", 403);
return patient;
```

Propagate a failure with `return result.Error!;`. `ApiError.From` keeps the registered error title and status unless a custom status is supplied. It does not change the registered error.

Keep explicit result factories when writing an HTTP response through `Ok`, `ObjectResult`, or JSON serialization, because those calls otherwise accept raw data or errors. Workspace read endpoints have response-body tests to protect this wrapper, including the workspace list used after login. Use `Result<T>.Success(null)` or `Success(default)` when a successful result deliberately has no data; a bare `return null` would return a null result. The JSON fields and HTTP status behavior remain unchanged.

Verified after this cleanup: 205 API tests passed, including 11 result conversion checks and the existing response contracts. Eleven PostgreSQL tests were skipped because no dedicated test connection was configured. No database or frontend change was needed.


## Organization administration

Platform mode provides these endpoints under `/api/v1/OrganizationAdministration`:

- `GET {organizationId}/Administrators`: view administrators, active status, and effective organization permissions.
- `PUT {organizationId}/Administrators/{membershipId}`: update organization access and permissions. Keep at least one administration permission. Editing one administrator does not change shared roles used by other staff.
- `DELETE {organizationId}/Administrators/{membershipId}`: remove that administrator's staff membership and role links for this organization. Shared login accounts, patient/practitioner identities, clinical authorship, and other organization memberships are kept.
- `DELETE {organizationId}`: delete an unused organization and its staff/scheduling setup. Patient registrations or care history return 409 instead of deleting historical records. A database foreign-key conflict also leaves the deletion unapplied.

Administrators are identified by organization, staff, or access-role management permissions, not a job title. Work and patient modes cannot call platform management endpoints. Organization role definitions and staff assignments remain scoped to their organization and location rules.

The frontend labels the membership editor **Manage Staff**. It manages sign-in access, role assignments, and location scope; it is separate from patient registration and practitioner assignment creation. Platform controls appear under **Manage Organization Admins** and on organization details/list actions. Removal and deletion use the existing confirmation dialog.


Administration follow-up verified on 8 November 2026: 219 API tests passed, including independent administration/clinical grants, practitioner creation by an administrator without clinical grants, platform-only access, shared-role isolation, shared-account preservation, and organization deletion/history protection. Seventeen frontend regressions passed. Eleven PostgreSQL tests were skipped; no application database or live browser journey was run. No schema migration is needed.


Latest combined Phase 2 cleanup verification: 219 API tests and 24 UI regressions passed; the full UI production build passed TypeScript and lint. Eleven PostgreSQL tests were skipped. No schema or package change was made. The application database and live browser workflows were not exercised. Account/member-list and workspace payloads are deliberately smaller; clinical response checks remain in place. The combined code review path and example request flow are in the workspace's `refactor-plan.md`.
