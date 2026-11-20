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
- `OrganizationMember` links an account to an organization. `AccessRole` stores a named permission group; `MemberRoleAssignment` assigns it to an organization member.
- `Location` represents a site, department, ward, room, bed, or another place. Parent locations belong to the same organization.
- `Encounter` and participants are ready in the schema. Clinical records store their source organization, practitioner assignment, and optional encounter link. The existing appointment/session workflow remains until Phase 5.

Organization-owned rows require `OrganizationId`. Composite foreign keys keep related organization-owned records in the same organization. Saved records cannot change their organization. Independent clinical references use restricted deletion; dependent children are deleted with their parent.

Domain navigation properties stay within an aggregate and its own children, such as a membership's role assignments. Separate aggregate roots reference each other by ID. Repositories use explicit LINQ query-syntax joins and DTO projections for related data; relationship constraints remain in Infrastructure configurations. Identity account types remain outside Domain.

Patient self-history resolves the patient through their account/person link and includes their visible local records across organizations. It does not discover records in external systems.

## Current scope

Phase 2 adds organization workspaces and permission checks to the Phase 1 model. The existing Identity/JWT login remains in place. User responses contain basic account details. Access decisions use current database grants and the selected workspace; JWT job-role claims do not grant application access.

After login, call `GET /api/v1/Workspace` for the small list of available workspace keys, names, and modes. `GET /api/v1/Workspace/Current`, with the chosen key, returns that workspace's permissions and IDs. `GET /api/v1/Patient/Me` returns own-patient details when a patient screen needs them. Send the selected `key` in the `X-Workspace` header on business requests. The server resolves that key against the authenticated account on every request, including active membership, current permissions, assignment ownership/dates, and location scope. A missing, revoked, or unauthorized workspace returns 403.

- `patient`: only the linked patient's data; no organization membership is needed.
- `work:<organizationId>`: one Work mode per active organization membership, with the distinct union of that member's assigned access-role permissions in that organization and the membership's location scope.
- `platform`: platform administration for accounts with the stored platform administrator flag. This mode does not grant clinical access.

`GET /api/v1/Patient/MyClinicalRecords` returns only the signed-in patient's visible records across local organizations. It takes no patient ID. Staff clinical writes use the selected organization; reading another organization's records additionally requires patient approval. Patient discovery is global for staff with patients.read. Patient-mode booking accepts only the linked patient.

`Workspace/Permissions` lists application permissions. `Workspace/Roles` supports listing, creating, and updating custom roles with `roles.manage`. `Workspace/Members`, `MemberRoles`, and `Locations` support membership editing with `members.manage`; `PUT Workspace/Members/{id}` saves active status, location, and access-role IDs. References must belong to the current organization. Members with `roles.manage` can define groups using any application permission. Members with `members.manage` can assign organization roles and manage staff access. They do not need the clinical permissions they administer. These administration permissions do not themselves allow reading records, starting encounters, or issuing prescriptions. A location-restricted workspace can add staff within its allowed locations and optionally assign existing roles; shared role definitions require unrestricted organization access.

When assigning an initial administrator in platform mode, choose any extra grants explicitly. The basic administrator grants cover organization details, members, roles, and schedules. They do not include clinical records. The optional `permissions` list grants additional work permissions explicitly; it is not a limit on which staff permissions the administrator can manage.

Patient self-registration and authenticated patient enablement are implemented. Email delivery/verification, invitations, old-patient activation, and password recovery remain deferred. Recovery columns are present, but no recovery endpoints or email packages have been added. Keep healthcare code behind the existing account-service boundary when adding a future identity provider.

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
- `PUT {organizationId}/Administrators/{organizationMemberId}`: update organization access and permissions. Keep at least one administration permission. Editing one administrator does not change shared roles used by other staff.
- `DELETE {organizationId}/Administrators/{organizationMemberId}`: remove that administrator's staff membership and role links for this organization. Shared login accounts, patient/practitioner identities, clinical authorship, and other organization memberships are kept.
- `DELETE {organizationId}`: delete an unused organization and its staff/scheduling setup. Patient registrations or care history return 409 instead of deleting historical records. A database foreign-key conflict also leaves the deletion unapplied.

Administrators are identified by organization, staff, or access-role management permissions, not a job title. Work and patient modes cannot call platform management endpoints. Organization role definitions and staff assignments remain scoped to their organization and location rules.

The frontend labels the membership editor **Manage Staff**. It manages sign-in access, role assignments, and location scope; it is separate from patient registration and practitioner assignment creation. Platform controls appear under **Manage Organization Admins** and on organization details/list actions. Removal and deletion use the existing confirmation dialog.


Administration follow-up verified on 8 November 2026: 219 API tests passed, including independent administration/clinical grants, practitioner creation by an administrator without clinical grants, platform-only access, shared-role isolation, shared-account preservation, and organization deletion/history protection. Seventeen frontend regressions passed. Eleven PostgreSQL tests were skipped; no application database or live browser journey was run. No schema migration is needed.


Latest combined Phase 2 cleanup verification: 219 API tests and 24 UI regressions passed; the full UI production build passed TypeScript and lint. Eleven PostgreSQL tests were skipped. No schema or package change was made. The application database and live browser workflows were not exercised. Account/member-list and workspace payloads are deliberately smaller; clinical response checks remain in place. The combined code review path and example request flow are in the workspace's `refactor-plan.md`.


Member naming: `OrganizationMember` represents an account's staff access at an organization; `MemberRoleAssignment` links that member to an `AccessRole` permission group. Code, repository, DTO, and workspace fields use these names (`OrganizationMemberId` / `organizationMemberId`). EF explicitly retains the existing database table and column names, so no migration or stored-data change is required.


Staff creation accepts `locationId` for both organization members and practitioner assignments. Choose an existing active location in the organization. Restricted workspaces require a location inside their permitted subtree; unrestricted workspaces may leave it null for all locations. This replaces the old free-text location/department inputs. Existing staff keep their location scope, and new assignments must fit within it. A restricted manager cannot change an existing member outside that scope or create shared access-role definitions. Staff creation does not require any role template.


Staff creation accepts optional `accessRoleIds` for both members and practitioner assignments. Omitting it or sending `[]` adds no permissions. Selected roles must belong to the organization; existing staff keep their roles and selected roles are added without duplicates. The API never assigns Front office or Clinician automatically. Seeded templates remain available for deliberate selection only. Professional titles such as Nurse or Radiologist grant no permissions. Initial administrator assignment remains a separate platform action with administration grants. Existing stored grants are not removed; use Manage Staff to change them.


## Patient signup and patient access

- `POST /api/v1/Users/RegisterPatient` is public. Send `personalInformation` (firstName, lastName, email, password; phoneNumber is optional), `confirmPassword`, `birthDate` (`YYYY-MM-DD`), and `gender` (Male/Female/Other/Unknown). It creates only the account, Person, and Patient in the existing registration transaction. Normalized duplicate emails return 409 without changing the existing account; signup never grants staff access or links another clinical identity. Email is not marked verified. After signup, use normal login.
- `POST /api/v1/Patient/EnableAccess` requires login and no selected workspace. Send `birthDate` and `gender` when creating the patient profile. The server uses the authenticated account's PersonId; the request cannot choose an account/person/patient ID. If a Patient already exists for that Person, the endpoint returns it unchanged, including on an empty repeated request. Staff grants, credentials, and organization registrations stay unchanged. Refresh the workspace list after success.
- New-account registration rejects existing emails. Organization staff creation may reuse an existing account only when that account already has an established membership in the target organization; the existing scope checks still apply. Linking an account to another organization by email and activating an unrelated patient record remain blocked until a confirmation flow is implemented. Staff-assisted creation of fresh accounts and management of established members remain supported.
- The existing normalized username and Patient.PersonId unique indexes protect concurrent requests. No migration or new package is needed. A competing patient enablement reloads the profile that won the insert; the dedicated PostgreSQL test covers this path.

Phase 3 checks on 11 November 2026: **294 non-PostgreSQL API tests and 42 UI tests passed**, and the UI production build passed TypeScript/lint. **12 PostgreSQL tests were excluded**, including the new competing-enablement test; no real database, API host, or browser journey was run. In-memory signup checks do not prove real transaction rollback; the opt-in PostgreSQL suite includes registration rollback coverage. Existing package audit/analyzer warnings remain.

Review: signup/enablement DTOs and controllers → PatientService / OrganizationService → IdentityGateway / PatientRepository → signup/account UI and workspace refresh → tests. Run the API checks with `dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj --no-restore --filter "Category!=PostgreSQL" -p:OutputPath=bin/PatientSignup/ -p:IntermediateOutputPath=obj/PatientSignup/`. Use the existing guarded disposable PostgreSQL setup for integration tests; do not point it at the application database.


## Direct clinical records

The existing `Patient/AddClinicalRecord` endpoint accepts an authorized clinical record with optional prescriptions without requiring a schedule, appointment, arrival, or Encounter. It checks the caller's selected active practitioner assignment, permitted patient scope, clinical write permission at the API boundary, and `prescriptions.issue` when needed. Patient, practitioner assignment, source organization, and server-recorded visit time are preserved; a direct save does not create or advance an appointment/session. No new clinical endpoint, schema, or package was needed for reduced Phase 5.

Verified on 11 November 2026: **298 non-PostgreSQL API tests and 56 UI tests passed**; the full UI production build passed TypeScript/lint. The new API checks exercise notes-only/prescription saves with zero schedules/appointments, unchanged existing appointment/session state, server authorship/time, own-patient history, and rejection of unregistered patients. Twelve PostgreSQL integration tests were excluded; no real database, API host, or browser journey was run. Existing dependency/audit/analyzer warnings remain.

Review: `WorkspaceAccessTests` → Patient Care / PatientSelector → shared record editor/form and history cards → direct-page layout guard. API check: `dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj --no-restore --filter "Category!=PostgreSQL" -p:OutputPath=bin/DirectClinicalCare/ -p:IntermediateOutputPath=obj/DirectClinicalCare/`.


## Organization Work mode and practitioner creation

The workspace list contains only one `Organization name — Work mode` entry per active organization membership, plus Patient mode and Platform Admin mode when available. Access roles and professional assignments never add workspace entries. A person with one organization and a Patient profile sees exactly two choices regardless of access-role count. Assignment-suffixed workspace keys are rejected. Patient mode carries no organization grants; platform access still requires the platform flag.

`Workspace/Current` returns that organization's distinct current permission list and a small `practitionerAssignments` list (`id`, `name`) for clinical context. A sole valid clinical assignment is automatic. Several genuinely different assignments can be selected within Work mode in the UI; selecting one does not change the organization key, permission union, or membership location scope. Every clinical request independently validates that its target assignment belongs to the caller, is active/in date, is in this organization, and fits the membership's location scope. A frontend choice is not authorization.

Exact repeated practitioner creation returns 409 before adding access-role grants. Title/specialty comparisons ignore case and surrounding spaces, while location, effective dates, and active status distinguish assignments. The existing registration transaction uses a [PostgreSQL account-row lock](https://www.postgresql.org/docs/current/explicit-locking.html#LOCKING-ROWS) to serialize competing creation checks. No new schema/index or package is needed. Existing duplicate rows are not deleted: they keep their scheduling/clinical references and are folded into one equivalent clinical choice. Inactive/expired assignments are excluded; organization membership remains independently usable.

Verified on 11 November 2026: **304 non-PostgreSQL API tests and 62 UI tests passed**, and the full UI production build passed TypeScript/lint. Thirteen PostgreSQL integration tests were excluded, including the new simultaneous-practitioner-creation test; no real database, API host, or browser journey was run. Existing local development settings were preserved. Review: WorkspaceRepository / WorkspaceAccess / WorkspaceFilter → OrganizationService / PractitionerRole duplicate rule / repository lock → workspace provider/selector → shared AddUserForm and callers → focused tests. API check: `dotnet test .\tests\NexMed360.API.Old.Tests\NexMed360.API.Old.Tests.csproj --no-restore --filter "Category!=PostgreSQL" -m:1 -nr:false -p:OutputPath=bin/OrganizationWorkspaces/ -p:IntermediateOutputPath=obj/OrganizationWorkspaces/`.


## Shared patient discovery and consent

Implemented on 17 November 2026. The existing Patient/Person identity is shared across organizations. Staff with `patients.read` can search name/email or open a Patient ID, including self-registered and other-organization patients. Search returns at most 30 matches and omits account/person IDs and phone numbers. `OrganizationPatient` is an administrative registration, not patient ownership. Existing names/emails never silently merge accounts; staff reuse the selected Patient ID for care.

`RecordSharingRequests` is the only new table. An approved request is the grant; clinical records/prescriptions remain with their source organization. Request creation needs `clinical-records.read` and an owned active practitioner assignment in the receiving organization. The patient needs an active portal account; staff-assisted activation remains deferred. Receiving staff need clinical-read permission and an active grant in their organization; location restrictions are preserved.

- `GET /api/v1/RecordSharing`: own inbox in Patient mode, or receiving-organization requests in Work mode; Work mode may filter by `patientId`.
- `POST /api/v1/RecordSharing`: patient ID, practitioner assignment, purpose, optional `recordFromDate`/`recordToDate` visit-date filters, optional `accessExpiresOn` date. Receiving organization and verified requester details come from trusted context/profiles.
- `POST /api/v1/RecordSharing/{id}/Decision`: `{ "approve": true }` or false, in the owning patient's mode only.
- `POST /api/v1/RecordSharing/{id}/Revoke`: owning patient revokes an approval. Concurrent/repeated decisions return a conflict rather than replacing the saved decision.

Dates filter the clinical visit date inclusively. The access expiry is separate and defaults to 30 days from request creation, ending on the chosen date in UTC. An expired pending request cannot be approved. Blank record dates include eligible past history; only published records created and visited before approval are shared. Future or backdated newly created records are excluded. No record copies are created. Revoking one approval does not cancel another valid approval. Revocation cannot undo information already viewed.

Existing history/detail APIs and prescription reads use the same consent scope. Source organization/author remain visible; recipients cannot edit source records. `isPatientVisible` defaults to true when saving and may be disabled. Patient visibility does not grant another organization access. Organizations with sharing history cannot be deleted. Request/decision/shared-read events use application logs; a new durable audit subsystem was not introduced. Protected responses disable HTTP caching; clients may still hold data already retrieved.

### Migration and review

Generated `20261117112723_AddRecordSharingRequests`; it adds the table/indexes/restricted foreign keys and does not replace existing patient or record tables. Check the intended development database's applied migrations and apply this migration explicitly if it is missing. The design-time factory deliberately uses an unreachable connection, so supply the checked connection when using the CLI:

```powershell
dotnet ef database update AddRecordSharingRequests --project src/NexMed360.Infrastructure --startup-project src/NexMed360.Api --connection "<checked development database connection>"
```

Review path: `RecordSharingRequest` domain transitions → `RecordSharingService` authorization/validation → `RecordSharingRepository` and `ClinicalRecordRepository.Readable` → `ApplicationDbContext` patient filter and migration → `RecordSharingController` → API regressions. The source-author projection bypasses role filters only for records already authorized for reading.

Verification on 17 November 2026: **323 non-PostgreSQL API tests and 70 UI regressions passed**; UI production build passed TypeScript/lint. PostgreSQL query translation and in-memory decision concurrency were checked. **14 dedicated PostgreSQL tests were excluded** because no test connection was configured, including the new approval/revocation flow. The migration matches the EF model. No application database, running API, or live browser journey was used. Existing package vulnerability/source warnings remain.

## Access, locations, booking, and session history

Workspace/Current includes the selected organization's access-role names and permitted practitioner assignments without loading full user/profile graphs. Authorization uses permissions, organization/location scope, and ownership. Role names and professional titles grant no access.

| Permission | Access |
| --- | --- |
| `appointments.read-own` | Personal staff appointments, across the caller's permitted practitioner assignments. |
| `schedules.read-own` | Personal staff sessions and session history. |
| `appointments.read-organization` | Read organization appointments. |
| `schedules.read-organization` | Read organization sessions and session history. |
| `appointments.manage-organization` | View and manage organization appointments. |
| `schedules.manage-organization` | View and manage organization sessions. |

Personal staff views need both own-read permission and an active, permitted assignment in the selected organization. No assignment needs to be selected for these lists. Management permission alone does not open personal views, and creating a booking for someone else does not make it personal. Patient mode reads its linked appointments without staff grants. Read-only organization permissions do not allow changes. Session history has inclusive date boundaries and shows stored session status, times, and encounter counts; detailed encounter history remains deferred.

Locations use `GET/POST /api/v1/Locations` and `PUT /api/v1/Locations/{id}`. Management requires `organizations.manage` in an unrestricted Work mode. Parents must belong to that organization. Cycles, inactive ancestors for active children, and deactivating a location with active children are rejected. Deletion is outside this scope.

`AcceptsAppointments` belongs to PractitionerRole and defaults to false. Staff managers configure it during staff creation or through `GET /api/v1/PractitionerAssignments?userId=...` and `PUT /api/v1/PractitionerAssignments/{id}/Booking`, using `{ "acceptsAppointments": true }`. This needs `members.manage` and respects organization/location scope without clinical grants. Booking search uses `forBooking: true` and optional `date` to check active/in-date opted-in assignments, active schedules, recurrence, and free/bookable slots. The booking API checks eligibility again. Normal staff/schedule search remains separate. Booking staff may load a specific practitioner's schedule without opening general session lists or history.

### Database setup

`AddPractitionerBookingEligibility` adds the booking flag, defaulting to false. Enable Accept appointments under Manage Staff for intended existing assignments after applying it. Existing appointments and clinical history remain intact.

`RenameOrganizationManagementPermissions` replaces the old `appointments.manage` / `schedules.manage` values in saved AccessRoles permission arrays with `appointments.manage-organization` / `schedules.manage-organization`, preserving other grants. The constants are `OrganizationAppointmentsManage` and `OrganizationSchedulesManage`. The migration is reversible.

Check the intended development database's applied migrations before running the update. This command applies through both migrations; the design-time factory requires an explicit checked connection:

```powershell
dotnet ef database update RenameOrganizationManagementPermissions --project src/NexMed360.Infrastructure --startup-project src/NexMed360.Api --connection "<checked development database connection>"
```

### Recorded verification

On 20 November 2026, **343 non-PostgreSQL API tests, 82 UI tests, TypeScript, and lint passed**. Coverage includes personal/organization boundaries, multiple assignments, location scope/cycles, booking eligibility, inclusive history, and existing consent behavior. EF generated the migrations and permission-rename SQL without connecting to a database. The 15 dedicated PostgreSQL cases, including the permission migration regression, were not run because no test connection was configured. No application database, running API, or live browser journey was used during this work. Existing package/audit warnings remain.
