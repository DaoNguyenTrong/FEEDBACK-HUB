# Risk-Tiered Process (cost reduction) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spend review effort by risk instead of by work size: tier 0 gets no agent review, tier 1 one light review per PR, tier 2 the full security flow, with the tier computed by a script so it is never a judgment call.

**Architecture:** One Bun script, `scripts/risk-tier.ts`, owns the path and content patterns and computes a change's tier (max over its files) and the per-tier `fix` ratio for the retro. `docs/process/lifecycle.md` §2 owns the rule (what each tier gets) and points to the script for the patterns. The agents (`pr-reviewer`, `code-reviewer`, `implementer`) and the PR template (`release-kit.json`) read the tier from the script.

**Tech Stack:** Bun 1.3.14 + `bun:test` (same as `scripts/check-comments.ts`), git CLI, Markdown process docs.

**Spec:** the proposal approved by the user in the session of 2026-11-12, restated in § Spec below. There is no separate spec file.

## Spec

| Tier | Which changes | Checks |
| --- | --- | --- |
| 0 | Docs, process text, locales, OpenAPI export and generated client, test-only changes | CI only. The main session reads the diff. No agent review. |
| 1 | Everything else: ordinary behavior in non-sensitive areas (CRUD, screens, lists, filters) | Sonnet `implementer`. Tasks batched per screen. No per-task `code-reviewer`. One Sonnet review per phase PR (`pr-reviewer`). No mutation checks, no Opus. |
| 2 | Sign-in, tokens, cookies, sessions, permissions, organization switching and tenant mechanism, API keys, locks/concurrency, EF migrations, CI and hook config, the tier script itself | Today's full flow: `appsec` on the technical design before the plan, Opus reviewer after each task, about 5 mutants, Opus final phase review, user merges. |

- The tier of a change is the highest tier of any file in it. The author may raise it, never lower it.
- Lighter documents for modules marked `không cần` appsec in `docs/process/progress.md`: the technical design uses the short form; UI boards only for new screens or new interaction patterns; one BA pass.
- `solution-architect` gets one batched call per stage. The main session decides purely technical questions that have a sensible default itself and records the decision.
- The retro reports the `fix` ratio per tier. If tier 1 leaks more than tier 2, the leaking paths move to tier 2.

## Global Constraints

- Process prose in `docs/process/` and `.claude/` stays Vietnamese, with full diacritics. Script code and comments are in English and follow `.claude/rules/comments.md` (at most 3 lines × 120 chars, checked by `bun scripts/check-comments.ts`).
- Each rule has one owner file (`lifecycle.md` §8 "Mỗi quy tắc một nơi"). Other files point to it with `file § section` and never copy it.
- Do not change `.github/workflows/`. `bun test scripts/` already runs in CI (`test / comments` job), so the script's tests are gated without a CI change.
- The process freeze (`lifecycle.md` §8) is lifted for this change by the user's order. The §9 entry says so.
- The PR touches `.claude/` and `docs/process/`, so neither `/git-accept` nor `pr-reviewer` may merge it. The user merges.

## Review Focus

1. A renamed auth file whose new name has no keyword (for example `Services/Auth/LoginFlow.cs`) must stay tier 2. Directory patterns cover it (Task 1 test `tierOf_authDirectoryWithoutKeywordInName_isTier2`).
2. A tier-1 controller that adds `[AllowAnonymous]` or drops `[Authorize]` must become tier 2. The content markers cover it (Task 1 test `tierOfChange_allowAnonymousAddedInTier1File_isTier2`).
3. A test-only change that deletes assertions in auth tests must not be tier 0 (Task 1 test `tierOf_testUnderSensitivePath_isTier1`).
4. When the base ref is missing (shallow clone, typo), the script must fail loudly. Printing tier 0 would remove every check (Task 1 step "CLI fails closed").
5. A rename shows both old and new paths, so a move out of a sensitive directory is still tier 2 (Task 1 CLI uses `--no-renames`).

---

## Phase 1 — one PR `chore/risk-tiered-process` into `dev` (user merges)

### Task 1: `scripts/risk-tier.ts`: tier of a change

**Files:**
- Create: `scripts/risk-tier.ts`
- Test: `scripts/risk-tier.test.ts`

**Interfaces:**
- Produces:
  - `export type Tier = 0 | 1 | 2`
  - `export type ChangedFile = { path: string; changedLines: string[] }`, where `changedLines` holds the `+`/`-` lines of the diff without the marker
  - `export function tierOf(path: string): Tier`, path only
  - `export function tierOfChange(files: ChangedFile[]): { tier: Tier; reasons: string[] }`. `reasons` lists up to 5 `path: why` strings for the files that set the tier. An empty list returns tier 0.
  - CLI `bun scripts/risk-tier.ts [--base <ref>] [--head <ref>]`. Defaults: `origin/dev`, `HEAD`. Prints `Risk tier: <n>` on line 1, then one reason per line. Exit 0.

**Patterns.** All are case-insensitive. Order per path: test paths first (tier 0, or tier 1 if the path also matches a tier-2 path pattern), then tier-2 paths, then tier-0 paths, otherwise tier 1. Content markers apply only to non-test `.cs`, `.ts` and `.vue` files, so a doc that mentions `[Authorize]` stays tier 0.
- Tier 2, path:
  - `^\.github/`, `^lefthook\.ya?ml$`, `^scripts/risk-tier(\.test)?\.ts$`
  - `/Migrations/`
  - backend directories `/(Auth|Authorization|ApiKeys|Organizations|Roles|Invitations|Members)/`
  - backend file names containing `Auth|Token|Cookie|Session|Permission|Tenant|ApiKey|Lockout|Role|Organization|Invitation|Member`
  - `^backend/src/FeedbackHub\.API/Program\.cs$`, `^backend/src/FeedbackHub\.Infrastructure/Persistence/AppDbContext\.cs$`
  - `^frontend/src/api/(client|mutator)\.ts$`, `^frontend/src/router/`, `^frontend/src/stores/(auth|organizations|roles)\.ts$`
- Tier 2, content: a changed line matching `\[(AllowAnonymous|Authorize)\b|HasQueryFilter|AddAuthentication|AddAuthorization|IgnoreQueryFilters|ExecuteSqlRaw|FromSqlRaw|withCredentials`.
- Tier 0, path: `^docs/`, `\.md$`, `^shared/openapi/`, `^frontend/src/api/generated/`, `^frontend/src/locales/`, `^\.claude/`, `^\.codex/`, `^\.agents/`.
- Tier 0, tests: `^backend/tests/`, `^frontend/tests/`, `\.test\.ts$`. A test path that also matches a tier-2 path keyword is tier 1, not 0.

- [ ] **Step 1: Write the failing tests** (a `test.each` table for `tierOf`, plus named cases)

```ts
test.each([
  ['docs/process/lifecycle.md', 0],
  ['frontend/src/locales/vi.ts', 0],
  ['shared/openapi/openapi.json', 0],
  ['frontend/tests/unit/views/products.test.ts', 0],
  ['frontend/src/views/products/ProductList.vue', 1],
  ['backend/src/FeedbackHub.Application/Services/Products/ProductService.cs', 1],
  ['backend/src/FeedbackHub.Application/Services/Auth/TokenIssuer.cs', 2],
  ['backend/src/FeedbackHub.API/Common/AuthCookies.cs', 2],
  ['backend/src/FeedbackHub.Infrastructure/Persistence/Migrations/20261112_X.cs', 2],
  ['frontend/src/api/client.ts', 2],
  ['.github/workflows/ci.yml', 2],
  ['scripts/risk-tier.ts', 2],
])('tierOf(%s) = %d', (path, tier) => expect(tierOf(path)).toBe(tier))

test('tierOf_authDirectoryWithoutKeywordInName_isTier2', () =>
  expect(tierOf('backend/src/FeedbackHub.Application/Services/Auth/LoginFlow.cs')).toBe(2))
test('tierOf_testUnderSensitivePath_isTier1', () =>
  expect(tierOf('backend/tests/FeedbackHub.Application.Tests/Services/Auth/AuthServiceTests.cs')).toBe(1))
test('tierOf_lowercaseKeyword_isTier2', () => expect(tierOf('frontend/src/stores/auth.ts')).toBe(2))
test('tierOfChange_takesTheMaxAndNamesTheReason', () => {
  const r = tierOfChange([
    { path: 'docs/a.md', changedLines: [] },
    { path: 'frontend/src/api/client.ts', changedLines: [] },
  ])
  expect(r.tier).toBe(2)
  expect(r.reasons[0]).toStartWith('frontend/src/api/client.ts')
})
test('tierOfChange_allowAnonymousAddedInTier1File_isTier2', () =>
  expect(tierOfChange([{ path: 'backend/src/FeedbackHub.API/Controllers/ProductsController.cs',
    changedLines: ['    [AllowAnonymous]'] }]).tier).toBe(2))
test('tierOfChange_empty_isTier0', () => expect(tierOfChange([]).tier).toBe(0))
test('tierOfChange_markerInDoc_staysTier0', () =>
  expect(tierOfChange([{ path: 'docs/tech/01.md', changedLines: ['Dùng [Authorize] cho mọi action'] }]).tier).toBe(0))
test('tierOf_tierScriptTest_isTier1', () => expect(tierOf('scripts/risk-tier.test.ts')).toBe(1))
```

- [ ] **Step 2: Run** `bun test scripts/risk-tier.test.ts`. Expected: FAIL, the module is not found.
- [ ] **Step 3: Implement** `tierOf` and `tierOfChange` as pure functions over the pattern lists above, exported as `TIER2_PATHS`, `TIER2_CONTENT`, `TIER0_PATHS` and `TEST_PATHS` (`RegExp[]`).
- [ ] **Step 4: Implement the CLI** (`if (import.meta.main)`). Read files with `git diff --no-renames --name-only <base>...<head>` and lines with `git diff --no-renames -U0 <base>...<head> -- <path>`. **CLI fails closed:** if either git call exits non-zero, print `risk-tier: cannot diff <base>...<head>` to stderr and exit 2. Never print a tier in that case.
- [ ] **Step 5: Run** `bun test scripts/` (expected PASS), then `bun scripts/risk-tier.ts --base origin/dev --head HEAD`. Expected: `Risk tier: 2` with reason `scripts/risk-tier.ts: …`. Then run `bun scripts/risk-tier.ts --base no-such-ref`. Expected: exit 2 and stderr message.
- [ ] **Step 6: Run** `bun scripts/check-comments.ts scripts/risk-tier.ts scripts/risk-tier.test.ts`. Expected: no findings.
- [ ] **Step 7: Commit** `git add scripts/risk-tier.ts scripts/risk-tier.test.ts && git commit -m "feat(scripts): compute a change's risk tier from its paths and diff"`

### Task 2: `fix` ratio per tier for the retro

**Files:**
- Modify: `scripts/risk-tier.ts`
- Test: `scripts/risk-tier.test.ts`

**Interfaces:**
- Consumes: `tierOfChange` (Task 1).
- Produces:
  - `export type CommitInfo = { subject: string; files: ChangedFile[] }`
  - `export function fixRatioByTier(commits: CommitInfo[]): Record<Tier, { fix: number; total: number }>`
  - CLI `bun scripts/risk-tier.ts --fix-ratio <range>` prints a Markdown table `| Hạng | fix | Tổng | Tỉ lệ |`, one row per tier, ratio as a whole percent or `—` when the total is 0.

- [ ] **Step 1: Write the failing tests:**
  - `fixRatioByTier_countsFixSubjectsPerTier`: two tier-2 commits (one `fix(auth): …`) and one tier-1 `feat` give `{2: {fix: 1, total: 2}, 1: {fix: 0, total: 1}, 0: {fix: 0, total: 0}}`.
  - `fixRatioByTier_fixPatternIsStrict`: subjects `fix: a`, `fix(x): a` and `fix!: a` count; `fixture: a` and `Revert "fix: a"` do not.
  - `fixRatioByTier_skipsCommitsWithNoFiles`.
- [ ] **Step 2: Run** `bun test scripts/risk-tier.test.ts`. Expected: FAIL.
- [ ] **Step 3: Implement.** The fix regex is `^fix(\(|!|:)`. The CLI uses `git log --no-merges --format=%H%x09%s <range>`, then the Task 1 diff calls per commit (`<sha>^..<sha>`). It fails closed the same way as Task 1.
- [ ] **Step 4: Run** `bun test scripts/` (PASS), then `bun scripts/risk-tier.ts --fix-ratio origin/main..origin/dev`. Expected: a 3-row table.
- [ ] **Step 5: Commit** `git commit -m "feat(scripts): report the fix-commit ratio per risk tier" -- scripts/risk-tier.ts scripts/risk-tier.test.ts`

### Task 3: Process rule in `lifecycle.md` and `progress.md`

**Files:**
- Modify: `docs/process/lifecycle.md` (§1, §2, §5b, §8, §9)
- Modify: `docs/process/progress.md` (§ Số đo)

**Interfaces:**
- Consumes: the CLI commands from Tasks 1–2.
- Produces: the anchor `lifecycle.md § Hạng kiểm tra`, which Tasks 4–5 point to.

- [ ] **Step 1: §2.** Add the subsection `### Hạng kiểm tra` after the S/M/L table. It holds the Spec table, the max/raise-only rule, and the command `bun scripts/risk-tier.ts`. It says that patterns live only in that script, and that a module's technical design names any new tier-2 path, which the same PR adds to the script. Add one sentence: S/M/L decides which documents are needed, the tier decides how much checking.
- [ ] **Step 2: §1 table, row 5** ("Cổng duyệt"): replace "`code-reviewer` giữa các task" with "theo hạng (§2 Hạng kiểm tra)". **Row 2** ("Xong khi"): "mọi FR có màn hình mới hoặc mẫu tương tác mới đều có bảng; màn hình theo mẫu có sẵn ghi tên mẫu". **Paragraph under the table:** the main session sends `solution-architect` one batched call per stage, and decides itself a purely technical question that has a sensible default, recording it in the plan.
- [ ] **Step 3: §5b.** Add the short-form technical design for modules marked `không cần` appsec in `progress.md`. Keep §1, §3, §4, §5, §11, §12, §13. The other sections may hold a single line "Không áp dụng: <lý do>". Aim for about 200 lines. The BA reviews the PRD once; a second pass only for a `blocking` finding.
- [ ] **Step 4: §8 retro.** Step 4 also pastes the output of `bun scripts/risk-tier.ts --fix-ratio <module range>`. Add step 6: if the tier-1 ratio is higher than the tier-2 ratio, add the paths behind the tier-1 `fix` commits to the script's tier-2 list in the retro's process PR.
- [ ] **Step 5: §9.** Prepend `### 2026-11-12 — Kiểm tra theo hạng rủi ro, không theo cỡ việc`. Reason: module 00's 22 repeated `fix` commits clustered in auth, token, concurrency and email verification, while every change paid the same per-task review. Mention that it is applied outside a retro by the user's order. Rejected options:
  - Merging the PRD and technical design into one spec. It changes three agents and the traceability chain; the short form gets most of the saving.
  - A CI job that labels the PR. It needs write permission in CI, and the tests already run under `test / comments`.
  - Self-assessed tiers. One misjudgment removes every check.
- [ ] **Step 6: `progress.md` § Số đo.** Add the column `fix theo hạng (0/1/2)`. Module 00's cell is `—`, because its branch is gone.
- [ ] **Step 7: Verify.** `grep -n "Hạng kiểm tra" docs/process/lifecycle.md` finds the §2 heading and the §1 reference. Every `§` reference you added resolves to an existing heading.
- [ ] **Step 8: Commit** `git commit -m "docs(process): spend review effort by risk tier" -- docs/process/lifecycle.md docs/process/progress.md`

### Task 4: Agents read the tier

**Files:**
- Modify: `.claude/agents/pr-reviewer.md` (§3, §4)
- Modify: `.claude/agents/code-reviewer.md`
- Modify: `.claude/agents/implementer.md` (§ Input)
- Modify: `.claude/release-kit.json` (`prExtraSections`)

**Interfaces:**
- Consumes: `bun scripts/risk-tier.ts`, `lifecycle.md § Hạng kiểm tra`.
- Produces: a `## Risk tier` section in every PR body, filled by `release-kit:git-mr`.

- [ ] **Step 1: `pr-reviewer.md` §3.** New step 0: run `bun scripts/risk-tier.ts --base origin/dev --head <pr head>`. If the PR body's `## Risk tier` is lower than the script's tier, that is `blocking`. For tier 0, check only items 6, 7 and 9 plus stale references; skip PRD, design and feature-map. Tier 1 keeps the full checklist. Tier 2 → §4 `NEEDS_HUMAN`, as today.
- [ ] **Step 2: `code-reviewer.md`.** One line at the top: the main session calls you only for tier 2 tasks (on Opus) or when a tier-1 PR needs a second look; state the tier from the script in your report.
- [ ] **Step 3: `implementer.md` § Input.** The brief states the task's tier. Mutation checks only for tier 2. Tier 0 tasks need no `feature-map` entry unless they change a flow.
- [ ] **Step 4: `release-kit.json`.** Append `"Risk tier"` to `prExtraSections`. Its content is the script's first line plus reasons.
- [ ] **Step 5: Verify.** `python3 -c "import json;json.load(open('.claude/release-kit.json'))"` exits 0, and every new `§` reference resolves.
- [ ] **Step 6: Commit** `git commit -m "chore(agents): read the risk tier before reviewing" -- .claude/agents/pr-reviewer.md .claude/agents/code-reviewer.md .claude/agents/implementer.md .claude/release-kit.json`

### Task 5: Index and memory

**Files:**
- Modify: `AGENTS.md` (§ Software Lifecycle, row 5)
- Modify: memory `agent-model-tiering.md` (outside the repo)

- [ ] **Step 1: `AGENTS.md` row 5, owner column:** "`implementer`; review theo hạng (`lifecycle.md` § Hạng kiểm tra)".
- [ ] **Step 2: Memory.** "security-critical" in the model rule now equals tier 2 from the script.
- [ ] **Step 3: Gate.** Run `bun test scripts/`, `bun scripts/check-comments.ts`, and `bun scripts/risk-tier.ts`. Expected: tier 2, because the PR contains `scripts/risk-tier.ts`.
- [ ] **Step 4: Commit** `git commit -m "docs(agents): point the lifecycle index at the risk tiers" -- AGENTS.md`

## Not in this plan

- Automated tests for the retro groups A–F of module 00. They target code that is partly gone with `feature/module-00`. Plan them when module 00 is redone.
- A CI job that comments the tier on each PR. Revisit at the module 01 retro if authors forget the `## Risk tier` section.
- A global query-filter check (an org-scoped entity without `HasQueryFilter`). Tier 1 services that forget the org filter remain a residual risk until an architecture test covers it.
