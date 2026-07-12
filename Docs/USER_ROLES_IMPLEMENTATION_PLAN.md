# User Roles Implementation Plan

## Objective

Add role-based user creation and authorization with exactly 3 roles:
- `Admin`
- `User`
- `Support`

The plan is designed for the current architecture (`Controllers` + `Services` + `Repositories` + EF Core + JWT + MFA).

## Current State Summary

- `Entities/User.cs` does not include a role field yet.
- `DTOs/Auth/CreateUserRequest.cs` does not include role input.
- `Services/Implementatons/TokenService.cs` does not issue role claims.
- `Controllers/UsersController.cs` allows open user creation (`POST /api/users`) without role enforcement.
- No authorization policies are defined for role-based route protection.

## Role Model Decision

Use a string-based role in persistence and a constants class in code:
- Store canonical role values in DB as `admin`, `user`, `support`.
- Expose normalized display values in responses when needed (`Admin`, `User`, `Support`).

Why this approach:
- Easy to query and seed.
- Backward-friendly for future role additions.
- Avoids enum migration friction in SQL Server over time.

## Phase 1: Domain and Contract Changes

1. Add role constants.
- Create `Constants/UserRoles.cs`.
- Values: `Admin`, `User`, `Support` and normalized lowercase equivalents if desired.

2. Add role to user entity.
- Update `Entities/User.cs` with `Role` (required, max length 20).
- Default role should be `user`.

3. Update DTOs.
- Add optional `Role` in `DTOs/Auth/CreateUserRequest.cs`.
- Add `Role` in `DTOs/Auth/CreateUserResponse.cs`.
- Keep role optional for public registration path and force default `user` when omitted.

4. Validation rules.
- Accept only `admin|user|support` (case-insensitive input).
- Normalize and persist lowercase.
- Reject unknown role with `400`.

## Phase 2: Persistence (EF Core)

1. Update model configuration.
- In `Data/Configurations/UserConfiguration.cs`, enforce:
  - `Role` required.
  - max length (20).
  - default value `user`.

2. Add migration.
- Create migration to add `Role` column to `Users`.
- Backfill existing rows as `user`.
- Add index for role filtering if future admin APIs require list-by-role (`IX_Users_Role`).

3. Repository adjustments.
- Extend `IUserRepository` and implementation if needed with role-aware queries:
  - `GetByIdAsync` (if missing)
  - optional `ListByRoleAsync(string role)` for admin tooling.

## Phase 3: Service Layer and Token Claims

1. User registration behavior.
- In `UserRegistrationService`:
  - Parse requested role.
  - If no role requested, assign `user`.
  - If role requested and caller is not authorized to assign privileged roles, reject.

2. Issue role claim in JWT.
- In `TokenService.CreateAccessToken`, add:
  - `ClaimTypes.Role` with canonical role value.
- Keep MFA token minimal; role claim is optional there unless needed by MFA-protected role endpoints.

3. Audit enhancements.
- Include role in audit metadata on user creation and login.
- Add event semantics for privileged role assignment attempts (accepted/rejected).

## Phase 4: Authorization and API Policies

1. Add policies in authentication extension.
- In `Extensions/AuthenticationExtensions.cs` add policies:
  - `AdminOnly`
  - `SupportOrAdmin`
  - `UserSelfOrAdmin` (when resource ownership applies)

2. Protect relevant endpoints.
- Restrict role-assignment/create-admin capabilities to `Admin`.
- Keep open registration endpoint only for `user` role.
- Protect future support/admin management routes with `[Authorize(Policy = ...)]`.

3. Optional endpoint split (recommended).
- Keep `POST /api/users` as public/self-registration => always creates `user`.
- Add privileged endpoint `POST /api/admin/users` for admins to create `admin|support|user`.

## Phase 5: Bootstrap and Operations

1. Seed initial admin account safely.
- Add one-time seed/migration or startup script to ensure at least one `admin` exists.
- Use environment variables/secrets for bootstrap credentials.

2. Prevent admin lockout.
- Block deletion/deactivation of the last active admin account.

3. Logging and monitoring.
- Track role-escalation attempts and admin actions.

## Phase 6: Tests

1. Unit tests.
- Role normalization/validation.
- Default role behavior.
- Token contains role claim.

2. Controller tests.
- `UsersController` role assignment permissions.
- 401/403 coverage for protected policies.

3. Integration tests.
- Create `user`, `support`, `admin` flows.
- Verify policy behavior across real JWT tokens.

## Phase 7: Documentation Updates

Update:
- `Docs/API_ENDPOINT_FLOW_GUIDE.md`: role-aware endpoints and policy expectations.
- `Docs/ARCHITECTURE.md`: RBAC model and claim usage.
- `Docs/README.md`: how to create users by role and who can assign roles.

## Suggested Delivery Slices

### Slice A (Safe Foundation)
- Entity + migration + constants + DTO updates + default role = `user`.

### Slice B (Security Core)
- Token role claim + authorization policies + protected admin endpoint.

### Slice C (Hardening)
- Last-admin protections + full tests + docs.

## Acceptance Criteria

- System supports only three valid roles: `Admin`, `User`, `Support`.
- New users created from public endpoint default to `User`.
- Only admins can create users with `Admin` or `Support` role.
- Access token includes role claim and policies enforce route access.
- Existing users are migrated safely to `user` with no downtime.
- Tests and docs are updated.
