---
paths:
  - "backend/**"
---

# Authentication

Two authentication schemes, both accepted by the default `[Authorize]` policy (`AuthExtensions.cs`):

- **JWT Bearer** — from the `Authorization` header or the `access_token` cookie (`OnMessageReceived`).
- **API key** — the `X-Api-Key` header (`ApiKeyAuthenticationHandler`), issued per organization via
  `/api/admin/api-keys`. It is a full, secret credential (accepted by plain `[Authorize]`, exempt from the CSRF
  check). Never ship one to a browser, and never model a public identifier on it: the FeedbackHub widget's
  product key is public, so it authenticates nothing and is checked only on `[AllowAnonymous]` widget endpoints
  (design in `docs/tech/`, TECH02).

**Deny by default.** `FallbackPolicy` is the default policy, so any endpoint — or middleware path after
`UseAuthorization` — that declares no access requires a signed-in caller; an unmapped route answers 401 to an
anonymous caller, 404 to a signed-in one (an `/api` request without `X-TimeZone` gets 400 first). Anonymous
surfaces opt out explicitly:

- Controllers: `[AllowAnonymous]` on `HealthController` and on `AuthController`'s `login`, `register`,
  `verify-email`, `resend-verification`, `confirm-email-change`, `external/{provider}` and `refresh` (pinned by their
  controller tests).
- `Program.cs`: `.AllowAnonymous()` on `/metrics`, `/hangfire` and the dev-only OpenAPI/Scalar routes (pinned by
  `FallbackAuthorizationTests`). A new `Map...` endpoint meant to be public needs the same call. `/metrics` and
  `/hangfire` (full job control, no app-level check) rely on the deployment: nginx answers 404 for both and the
  `api` service publishes no port. A deployment that lets clients reach the API directly exposes them.

Outside deny-by-default — answered before `UseAuthorization`: CORS preflights (`UseCors`) and Local-storage
files (`UseStaticFiles` on `StorageSettings:PublicUrlBase`), which anyone holding a file's URL can read.

`ArchitectureTests.ControllerActionsAndHubs_DeclareAuthorizeOrAllowAnonymous` also fails CI for a controller
action (inherited ones included) or hub with neither attribute, on itself or its class — choose one explicitly.
Controllers declare it at class level except `AuthController`, which authorizes per action.

## Organizations and permissions

Multi-tenant by organization, RBAC per organization. A session is scoped to at most one active
organization, carried as the signed `org_id` JWT claim — never a client-supplied header. Switching
re-issues tokens via `POST /api/auth/switch-organization` (`organizationId: null` returns to the
personal, org-less context).

Each organization has its own roles (`Role`, system kinds `Owner`/`Admin`/`Member` in
`SystemRoleKind`, plus custom ones); a role bundles permissions from the fixed, code-defined catalog
`Permissions`. Endpoints gate with `[Authorize(Policy = Permissions.X)]` (resolved by
`OrganizationPermissionPolicyProvider`) or one of the `AuthorizationPolicies`:

- `OrganizationMember` — active member of the organization in the `{id}` route segment.
- `ActiveOrganizationMember` — active member of the JWT's organization; outside it → 403 `OrganizationAccessDenied`.
- `ActiveOrganizationMemberOrNotFound` — the same, but outside it → 404 `ResourceNotFound` (BR-45).
- `SignedInUser` — a signed-in person's JWT; never an API key.
- `SystemAdministrator` — a system administrator's JWT (API key → 401), whatever `org_id` it carries; anyone
  else → 403 `SystemAdministratorRequired`. Membership in `system_administrators` resolves per request through
  `ISystemAdministratorResolver` (60-second cache that `InvalidateAsync` drops on every instance), never from a JWT
  claim.

The organization gate (`OrganizationContextAuthorizationHandler`, TECH00 §5.1) backs the two
`ActiveOrganizationMember*` policies and the `Permissions.ActiveOrganizationScoped` codes. It checks in this order
and answers the first failure: not signed in (401), outside the organization — personal context, API key or
non-member (403 or 404), organization suspended (403 `OrganizationSuspended` with `organizationName`), missing
permission (403 `OrganizationAccessDenied`). `organizations.manage` is in `Permissions.ActiveOrganizationScoped` too and
is checked against the JWT organization: the `DELETE api/organizations/{id}` route is gone. Only the route-scoped codes
`organizations.members.manage` and `organizations.roles.manage` (and `OrganizationMember`) still check the `{id}`
organization, with no suspended step, until those routes go (phase 4).
Effective permissions resolve per request through a short-TTL cache, not the JWT. The frontend's
`meta.permission` route gate is UI-only (`frontend-conventions.md`).

## Headers

- `X-TimeZone` — required. IANA timezone identifier (e.g. `Asia/Ho_Chi_Minh`). Missing/invalid → 400
  (`UserTimeZoneMiddleware`). Exempt: `/api/health` and `/api/auth/check` (nginx's `auth_request` for the
  user guide forwards only the cookie).
- `X-CSRF-Protection` — required on every non-GET/HEAD/OPTIONS `/api` request that carries the `access_token`
  cookie and no `Authorization`/`X-Api-Key` header; missing → 403 (`CsrfProtectionMiddleware`). Any value works:
  a cross-site form can't set a custom header, so its presence proves the request came from script on an
  allowed origin.

`GET /api/auth/check` (policy `AuthorizationPolicies.SignedInUser`) accepts only a user's JWT — an API key
gets 401 there, unlike every other `[Authorize]` endpoint.

## Temporary lockout

`LoginLockoutSettings` (default: 5 wrong passwords within 15 minutes → locked for 15 minutes). The attempt is reserved in the database *before* the password is checked (`IPasswordAttemptStore`, one column-scoped statement each), so parallel guesses can't exceed the limit; EF never writes the lockout columns on an ordinary account update. The lock is reported (`AccountTemporarilyLocked`) before the password check — reporting it only for a correct password would confirm the guess. Unknown, deactivated and password-less accounts get the generic `InvalidUsernameOrPassword` and count nothing. A successful external (Google/Microsoft) sign-in bypasses the lock and clears the counter.

The current-password check of `PUT /api/profile/password` follows the same scheme and limits on its **own** counter and lock (`PasswordAttemptPurpose.PasswordChange`, reported as `PasswordChangeTemporarilyLocked`): a stolen session can't brute-force the current password, and can't lock the owner out of signing in either — nor do failed sign-ins block a password change. Any new endpoint that verifies a password gets a purpose of its own the same way.
