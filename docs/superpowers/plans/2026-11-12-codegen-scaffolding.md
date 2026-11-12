# Codegen and Scaffolding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace hand-repeated code with generators and automatic checks, so each new endpoint, message key, locale key and entity costs less to write and cannot drift from the conventions.

**Architecture:** Six independent cuts:
- (1) a reflection test that sends an anonymous request to every authorized controller action;
- (2) an OpenAPI operation transformer that documents 401/403, replacing the per-action attributes;
- (3) `LocaleSchema` derived from `en.ts`;
- (4) a test that pins message keys to both `.resx` files;
- (5) one script for the contract sync;
- (6) a `dotnet new` template for an entity slice, plus a skill for the steps the template cannot do.

**Tech Stack:** .NET 10, xUnit + NSubstitute, Microsoft.AspNetCore.OpenApi, `dotnet new` templating, Vue 3 + vue-i18n + TypeScript, Bun.

**Approval:** `solution-architect` (on the user's behalf) approved with 12 changes, applied in this version.

**Spec:** items 1–6 of the codegen review in the session of 2026-11-12. The user approved them ("Làm theo đề xuất của bạn"). The measurements:
- 49 controller actions, but only 29 `Unauthorized` and 9 `Forbidden` asserts;
- 152 `ProducesResponseType` lines, 46 of them 401 and 18 of them 403;
- `en.ts` is 597 lines against 306 in `vi.ts`, because it repeats every key in a hand-written interface;
- no test pins the message keys to the `.resx` files;
- the ApiKey slice is about 600 lines, mostly fixed shape.

**Branch:** `chore/codegen-scaffolding`, stacked on `chore/risk-tiered-process`, which is not merged yet. It is rebased onto `origin/dev` once that PR merges, and opens no PR before then.

## Global Constraints

- Every rule in `AGENTS.md`, `.claude/rules/*` and `docs/process/lifecycle.md` § Hạng kiểm tra applies.
  - Tiers come from `bun scripts/risk-tier.ts`.
  - Only `implementer` edits `backend/` and `frontend/`; the main session edits `scripts/`, `.claude/` and `docs/`.
  - Backend builds run one at a time (`-m:1`).
- `shared/openapi/openapi.json` after Task 2 may differ from before only by added 401/403 response entries, or an existing 401/403 gaining `ProblemDetails` content (compared with `jq -S`). Any removal or other diff is a bug.
- No existing test is deleted in this plan. Deciding whether the reflection test makes the hand-written 401 tests redundant waits for the module 01 retro.
- The template output must pass the existing `ArchitectureTests`, the entity pattern and `.claude/rules/code-conventions.md` (no `var`, explicit types, primary constructors).

## Review Focus

1. **Route templates.** A route with a constraint (`{id:guid}`, `{id:long}`) or a catch-all must be called with a valid placeholder. Otherwise the route does not match and routing answers before authorization, so the anonymous test proves nothing about that action. The signed-in route-match test is the proof that each URL reaches its action; an unknown constraint throws in the data source (step 1). Owner: Task 1.
2. **The wrong status first.** An endpoint whose first middleware is not authorization would answer 400 or 415 before 401. Examples: rate limiting, a body-size limit, the `X-TimeZone` header check. The test sends `X-TimeZone` and an empty JSON body with `Content-Type: application/json`. Owner: Task 1.
3. **Anonymous endpoints that legitimately return 401.** `login` and `refresh` declare 401 for bad credentials. The transformer must leave anonymous operations alone, and their own attributes stay. Owner: Task 2.
4. **Endpoints authorized only by policy.** `SignedInUser` and `OrganizationMember` are not permission policies. 403 is documented only where the action or controller carries a permission policy (`Permissions.*`) or `OrganizationMember`/`ActiveOrganizationMember`, matching today's attributes. Task 2 is verified by the `openapi.json` diff. Owner: Task 2.
5. **Template file names.** A template instantiated with a name that is also a C# keyword or an existing entity (`Role`) must not overwrite files. `dotnet new` refuses to overwrite without `--force`, and the skill says never to pass `--force`. Owner: Task 6.

---

## Phase 1 — one PR (after rebase onto `dev`)

### Task 1: Every authorized action rejects an anonymous caller (item 1) — tier 2

**Files:**
- Create: `backend/tests/FeedbackHub.API.Tests/Architecture/AnonymousAccessTests.cs`

**Interfaces:**
- Consumes: `ApiFactoryFixture.CreateTestClient()` (sets `X-TimeZone`) and `AuthTestHelper`, to get a signed-in client for the route-match check.
- Produces: nothing used by later tasks.

- [ ] **Step 1: Write the `[Theory]`.** Name it `AuthorizedAction_AnonymousCaller_Returns401`, with `[MemberData]` drawn from `EndpointDataSource` in the test host.
  - Include every `RouteEndpoint` whose metadata has a `ControllerActionDescriptor`, no `IAllowAnonymous`, and either some `IAuthorizeData` or none (then the fallback policy, which requires authentication, applies). Write this as one predicate and say in its comment which actions today rely on the fallback.
  - Each row carries the HTTP method and a URL built from `RoutePattern`. A parameter gets `Guid.Empty` for `guid` and `1` for `int` or `long`; an unconstrained parameter gets `x`. Any other constraint (`alpha`, `regex`, `min`, …), an optional or a catch-all parameter **throws** in the data source with the route named, never falls back to `x`.
  - Send the request without credentials, with `Content-Type: application/json` and the body `{}` for POST, PUT and PATCH.
  - Assert `HttpStatusCode.Unauthorized`.
- [ ] **Step 2: Write the guard `[Fact]`** `EndpointSource_FindsAuthorizedActions`. It asserts the row count is at least 40, so an empty data source cannot pass vacuously.
- [ ] **Step 3: Write the route-match `[Theory]`** over the same rows: `AuthorizedAction_SignedInCaller_RouteMatches`. A signed-in member of one organization, with no permissions, calls each URL, using only the `Guid.Empty`/`1` ids above so no row is changed, in its own test class with its own database reset. Assert the status is not 404 **from routing** and not 429: any 404 must carry a `ProblemDetails` body with a domain error code (a bare routing 404 has no body). Confirm in the test host that rate limiting cannot trip on this many calls (pin its limits in `ApiFactoryFixture` if needed).
- [ ] **Step 4: Run.**
  - `dotnet test backend/tests/FeedbackHub.API.Tests --no-restore -m:1 --filter AnonymousAccessTests`
  - Expected: all PASS. A failure is a real finding, either an endpoint that answers before authorizing or a test bug. Report it; do not weaken the assert.
- [ ] **Step 5: Mutation check (tier 2).** Apply each mutant alone, run step 4, confirm the named test fails, then revert. Report the list to the reviewer.
  1. Set `FallbackPolicy` to allow anonymous callers (`AuthExtensions.cs`) and remove the class-level `[Authorize]` from `ApiKeysController`. Expected: `AuthorizedAction_AnonymousCaller_Returns401` fails for the ApiKeys rows.
  2. Add a middleware before `UseAuthorization` in `Program.cs` that answers 200 for `/api/admin/api-keys`. Expected: the same test fails.
  3. Change the parameter placeholder for `guid` to `x`. Expected: `AuthorizedAction_SignedInCaller_RouteMatches` fails.
  4. Make the endpoint filter exclude every row. Expected: `EndpointSource_FindsAuthorizedActions` fails.
  5. Make `ApiKeyAuthenticationHandler` succeed when the header is missing. Expected: `AuthorizedAction_AnonymousCaller_Returns401` fails.
- [ ] **Step 6: Commit** `test(api): reject an anonymous caller on every authorized action`.

### Task 2: Document 401/403 by transformer (item 2)

**Files:**
- Create: `backend/src/FeedbackHub.API/OpenApi/AuthorizationResponsesOperationTransformer.cs`
- Modify: `backend/src/FeedbackHub.API/Extensions/OpenApiExtensions.cs:50` (register it after `TimeZoneHeaderOperationTransformer`)
- Modify: every controller in `backend/src/FeedbackHub.API/Controllers/`. Remove `ProducesResponseType` for 401 and 403 **only** on actions the transformer now covers.
- Modify: `shared/openapi/openapi.json` (re-export)
- Test: `backend/tests/FeedbackHub.API.Tests/OpenApi/AuthorizationResponsesOperationTransformerTests.cs`

**Interfaces:**
- Produces: `public sealed class AuthorizationResponsesOperationTransformer : IOpenApiOperationTransformer`
  - It reads `context.Description.ActionDescriptor.EndpointMetadata`.
  - No `IAllowAnonymous`: it adds `401` with a `ProblemDetails` schema, unless the operation already has one.
  - An `IAuthorizeData` whose `Policy` is a value of `Permissions.All`, or is `AuthorizationPolicies.OrganizationMember` or `ActiveOrganizationMember`: it adds `403` the same way.
  - The schema comes from `context.GetOrCreateSchemaAsync(typeof(ProblemDetails))` (Microsoft.OpenApi 2.x), not built by hand.

- [ ] **Step 1: Export the spec before any change.** Copy `shared/openapi/openapi.json` to the scratchpad as `openapi.before.json`.
- [ ] **Step 2: Failing tests.** One for each case:
  - `Transform_AnonymousAction_AddsNothing`
  - `Transform_AuthorizedAction_Adds401WithProblemDetails`
  - `Transform_PermissionPolicy_Adds403`
  - `Transform_SignedInUserPolicy_DoesNotAdd403`
  - `Transform_Existing401_IsKept`
  - `Transform_ClassAuthorizeWithActionAllowAnonymous_AddsNothing`

  Build the `OpenApiOperationTransformerContext` from a hand-made `ApiDescription`.
- [ ] **Step 3: Implement and register.** Run the tests. Expected: PASS.
- [ ] **Step 4: List before removing.** List the actions that declare 403 (or 401) today but that the transformer would not cover, such as `SignedInUser`-only actions; their attributes stay. Put the list in the report.
- [ ] **Step 5: Remove the covered attributes,** then export (`commands.md` § OpenAPI Spec Export) and compare with `jq -S` against `openapi.before.json`. Allowed: added 401/403 entries, and an existing 401/403 gaining `ProblemDetails` content. List each case in the report. Any removal or other change is a bug.
- [ ] **Step 6: Run the gates.** `backend/scripts/test.sh` must pass, and `bun run --cwd frontend codegen && bun run --cwd frontend type-check` must pass.
- [ ] **Step 7: Commit** `refactor(api): document 401 and 403 by an OpenAPI transformer`.

### Task 3: `LocaleSchema` from `en.ts` (item 3) — tier 0 (locales)

**Files:**
- Modify: `frontend/src/locales/en.ts`

- [ ] **Step 1: Restructure `en.ts`.** Delete the hand-written `export interface LocaleSchema { … }`. Declare `const en = { … }` with the existing values, then `export type LocaleSchema = typeof en` and keep the existing default/named export shape.
  - `vi.ts` stays `const vi: LocaleSchema = { … }`.
  - Leaf types become `string`. Check that no key relied on a narrower type in the interface: `grep` the removed interface for anything other than `string` and nested objects. If one exists, keep it with a `satisfies` annotation on that key and note it in the report.
- [ ] **Step 2: Verify.**
  - `bun run --cwd frontend build-only && bun run --cwd frontend type-check` passes.
  - `bun run --cwd frontend test:run` passes.
  - Proof that the type still bites: delete one key from `vi.ts`, see `type-check` fail, restore.
- [ ] **Step 3: Commit** `refactor(locales): derive LocaleSchema from the English locale`.

### Task 4: Message keys pinned to both `.resx` files (item 4)

**Files:**
- Create: `backend/tests/FeedbackHub.Application.Tests/Resources/MessageKeysTests.cs`

- [ ] **Step 1: Write the tests.**
  - `Resx_BothCulturesHaveTheSameKeys`: parse both files with `XDocument` from the source tree. Walk up from `AppContext.BaseDirectory` until a directory containing `FeedbackHub.sln` is found; fail the test if it or either `.resx` file is not found, so it cannot pass vacuously.
  - `DomainMessages_EveryKeyHasText` and `ApplicationMessages_EveryKeyHasText`: reflect over `public const string` fields; each value must exist in both files.
- [ ] **Step 2: Run.** `dotnet test backend/tests/FeedbackHub.Application.Tests --no-restore -m:1 --filter MessageKeysTests`. Expected: PASS today, since both files have 60 entries. Prove it bites: add a fake `const string` to `ApplicationMessages`, watch the test fail, remove it.
- [ ] **Step 3: Commit** `test(resources): pin every message key to both cultures`.

### Task 5: One command for the contract sync (item 5) — main session

**Files:**
- Create: `scripts/sync-contract.sh` (executable)
- Modify: `.claude/rules/api-contract.md` § Regenerating, and `.claude/rules/commands.md` § OpenAPI Spec Export. Both point to the script and keep the two raw commands as what it runs.

- [ ] **Step 1: Write the script.** It starts with `cd "$(dirname "$0")/.."`, then runs, with `set -euo pipefail`:
  - `dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`
  - `bun run --cwd frontend codegen`
  - `bun run --cwd frontend build-only`
  - `bun run --cwd frontend type-check`

  Then it prints `git diff --stat -- shared/openapi/openapi.json`.
- [ ] **Step 2: Verify.** Run it. Expected: exit 0, and no `openapi.json` diff on this branch before Task 2 lands. Run it after Task 2 if Task 2 is done first.
- [ ] **Step 3: Commit** `chore(scripts): sync the API contract in one command`.

### Task 6: Entity-slice template and skill (item 6)

**Files:**
- Create: `backend/templates/feedbackhub-entity/.template.config/template.json`
- Create, under `backend/templates/feedbackhub-entity/`, mirroring the repo layout, with `Sample` as `sourceName`:
  - `src/FeedbackHub.Domain/Entities/Sample.cs`: `SampleParams(string Name)`, private constructor, `Create`/`Update` with the name validation, and `IHasOrganization` when org-scoped.
  - `src/FeedbackHub.Application/Services/Samples/`: `ISampleService.cs`, `SampleService.cs` (create, list, update, delete; tenant-filtered through the repository), `SampleDto.cs`, `SampleRequest.cs`, `SampleMapper.cs` (Mapperly `ToDto`, `ToParams`).
  - `src/FeedbackHub.Infrastructure/Persistence/Configurations/SampleConfiguration.cs`
  - `src/FeedbackHub.API/Controllers/SamplesController.cs`: lowercase route `api/samples`, class-level `[Authorize(Policy = Permissions.SamplesManage)]`, and no 401/403 attributes (Task 2 documents those).
  - `tests/…/SampleTests.cs` (Domain), `SampleServiceTests.cs` (Application, `Fixture` + `CreateFixture()`), `SamplesControllerTests.cs` (API: happy path, 400, 403).
- Create: `.claude/skills/new-backend-slice/SKILL.md` and `.agents/skills/new-backend-slice/SKILL.md` (a thin pointer, like `git-accept`)

**Interfaces:**
- Template parameters: `--name` (`sourceName` `Sample`), `--plural` (replaces `Samples`), `--route` (replaces `samples`), and `--org-scoped` (bool, default `true`; strips `IHasOrganization` and the tenant filter when `false`, through `//#if (OrgScoped)` blocks in the C# files).
- Substitution order matters: `sourceName` `Sample` also matches inside `Samples`, and it is case-sensitive. Declare the `--plural` and `--route` symbols so that `Samples` and `samples` are replaced before `sourceName`, and verify it (step 2).
- Short name: `feedbackhub-entity`. Install with `dotnet new install backend/templates/feedbackhub-entity`.

- [ ] **Step 1: Write the template.** Model it on the ApiKey slice and the conventions in `.claude/rules/code-conventions.md` and `testing.md`. Keep only the shape; business fields beyond `Name` are left out.
- [ ] **Step 2: Verify in a scratch copy, never in the checkout.**
  - Run `rsync -a --exclude bin --exclude obj backend/ $SCRATCH/backend/`, then `dotnet new install backend/templates/feedbackhub-entity` and `dotnet new feedbackhub-entity -n Widget --plural Widgets --route widgets -o $SCRATCH/backend`.
  - Add, in the scratch copy only, what the skill lists: the `Permissions.WidgetsManage` constant and its `All` entry, the DI line, the message keys in both `.resx` files and the `DbSet` if one is needed.
  - Then run `dotnet build $SCRATCH/backend/FeedbackHub.sln -m:1` and `dotnet test` the four test projects in the scratch copy.
  - Expected: build and tests PASS, including `ArchitectureTests` and the generated tenant-isolation test (a read from another organization returns 404) in `WidgetServiceTests`.
  - `grep -ri sample` over the generated files returns nothing.
  - Repeat with `--org-scoped false`.
  - Finally `dotnet new uninstall`.
- [ ] **Step 3: Write the skill.** `new-backend-slice` lists, in order, what the template cannot do:
  1. Permission constant and `Permissions.All`.
  2. DI registration.
  3. Message keys in `DomainMessages` and both `.resx` files.
  4. EF migration (`commands.md`).
  5. `scripts/sync-contract.sh`.
  6. Fill in the business fields and their tests.
  7. Add tier-2 paths to `scripts/risk-tier.ts` when the slice is sensitive.
  8. Never pass `--force`.

  It is under 60 lines and points to rule files instead of copying them.
- [ ] **Step 4:** The template's `.cs` files must pass `bun scripts/check-comments.ts backend/templates` (check that `//#if` lines and `.template.config/` do not trip it, prettier or `dotnet format`), and no `Directory.Build.props` glob may pick up `backend/templates/**`. Prove the sln compiles none of them: put a deliberate syntax error in one template file, run `dotnet build backend/FeedbackHub.sln --no-restore -m:1`, expect success, then revert.
- [ ] **Step 5: Commit** `feat(templates): scaffold an entity slice with dotnet new`. The main session commits the skill files separately: `docs(skills): add the new-backend-slice skill`.

## Execution

| Task | Who | Tier (expected) | Review |
|---|---|---|---|
| 1 | `implementer` (Sonnet) | 2: tests under `Architecture/` touching auth | `code-reviewer` on Opus, with the mutant list |
| 2 | `implementer` (Sonnet) | 2: `[Authorize]` lines change in controllers | `code-reviewer` on Opus |
| 3 | `implementer` (Sonnet) | 0 | main session reads the diff |
| 4 | `implementer` (Sonnet), batched with Task 3 only if both are idle; otherwise alone | 0 (tests) | main session reads the diff |
| 5 | main session | 0 | none |
| 6 | `implementer` (Sonnet) | 1 (the scratch run must include the tenant-isolation test) | covered by `pr-reviewer` at the phase |

- Backend tasks 1, 2, 4 and 6 run one after another, because of the shared `obj/`. Task 3 (frontend) may run in parallel with any of them.
- At the end of the phase, the main session runs `scripts/test-gate.sh`.
- `pr-reviewer` runs on Opus, because the phase is tier 2. The user merges, because `.claude/` and `scripts/` are touched.
