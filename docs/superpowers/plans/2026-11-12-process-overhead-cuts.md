# Process Overhead Cuts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut the per-call and per-module overhead the risk tiers leave behind. Eight cuts, numbered as in the review: (1) no agent review in `/git-accept`, (2) slimmer always-loaded context, (3) one Opus review for tier 2, (4) token numbers in the metrics, (5) one docs PR per module, (6) dark boards only for new patterns, (7) no CodeGraph prompt hook here, (8) cheaper main session while executing a plan.

**Architecture:** Process docs and agent prompts change in place. One new Bun script, `scripts/check-prd-approval.ts`, replaces the `code-reviewer` call in `/git-accept`. `docs/process/decisions.md` takes over `lifecycle.md` §9. A project opt-out file switches off the user-level CodeGraph prompt hook in this repo only.

**Tech Stack:** Bun 1.3.14 + `bun:test`, Markdown, `~/.claude/settings.json` hooks.

**Spec:** the eight items the user approved in the session of 2026-11-12, restated in Goal. This plan continues `2026-11-12-risk-tiered-process.md` on the same branch, `chore/risk-tiered-process`.

## Global Constraints

- Same as the previous plan: Vietnamese process prose with diacritics, every rule in one owner file, no `.github/` change, the user merges.
- The decision log is append-only and newest first. Moving it to a new file keeps every entry byte-for-byte.
- The main session's model stays the user's choice. Item 8 only documents a recommendation; nothing switches the model.

## Review Focus

1. A PRD whose last user-approval row has no earlier `BA review` row for the same version must fail `check-prd-approval` (test `approvedWithoutBaReview_fails`).
2. A content row after the approval row must fail, and a row marked `metadata` must pass (tests `contentAfterApproval_fails`, `metadataAfterApproval_passes`).
3. A PRD that is not *Approved* is skipped, not failed (test `notApproved_isSkipped`).
4. Both PRDs on `dev` (PRD-00 v1.4, PRD-01 v1.1) must pass, or the gate blocks every future docs PR (Task 1 step 5).
5. The hook opt-out must not break the hook in other repos: no opt-out file means the hook runs unchanged (Task 6 step 3).

---

### Task 1: `/git-accept` without an agent review (item 1)

**Files:**
- Create: `scripts/check-prd-approval.ts`, `scripts/check-prd-approval.test.ts`
- Modify: `.claude/commands/git-accept.md` (§4 Review gate, description), `.agents/skills/git-accept/SKILL.md` (description), `docs/prd/README.md` (§ Quy trình step 4)

**Interfaces:**
- Produces:
  - `export function checkPrdApproval(markdown: string): string[]`. Returns the findings, or `[]`. A PRD that is not *Approved* returns `[]`.
  - CLI `bun scripts/check-prd-approval.ts <file>...` prints `path: finding` lines and exits 1 if there is any finding, else 0.

**Rule:**
- The status is the `Trạng thái` row of the header table, and the version is its `Phiên bản` row.
- The rows considered are the §14 rows of that version.
- The **approval row** is the last of those rows whose `Thay đổi` starts with `Người dùng duyệt`, and whose `Nguồn` contains `Quyết định của người dùng`.
- Findings:
  - `no user-approval row for v<V>`.
  - `no BA review before the approval of v<V>`, when no earlier row of that version has `BA review` in `Nguồn`.
  - `content change after the approval of v<V>: <first 60 chars>`, for each later row of that version whose `Nguồn` is not exactly `metadata`.

- [ ] **Step 1: Write failing tests:**
  - `approvedWithBaReviewThenApproval_passes`
  - `approvedWithoutBaReview_fails`
  - `contentAfterApproval_fails`
  - `metadataAfterApproval_passes`
  - `notApproved_isSkipped`
  - `approvedWithoutApprovalRow_fails`
  - `onlyRowsOfTheCurrentVersionCount`: a BA row of an older version does not satisfy the current one.
- [ ] **Step 2: Run** `bun test scripts/check-prd-approval.test.ts`. Expected: FAIL, the module is not found.
- [ ] **Step 3: Implement.** Parse the table rows as `| date | version | change | source |`.
- [ ] **Step 4: Run** `bun test scripts/`. Expected: PASS.
- [ ] **Step 5: Run** `bun scripts/check-prd-approval.ts docs/prd/00-tong-quan-va-nen-tang.md docs/prd/01-quan-ly-san-pham.md`. Expected: exit 0.
- [ ] **Step 6: Edit `git-accept.md` §4.** Run the script on every `docs/prd/*.md` file in the PR (other than `README.md` and `_template.md`). Any finding is `blocking`. The main session reads the diff for stale file, command or `§` references, per tier 0 in `lifecycle.md` § Hạng kiểm tra. No `code-reviewer`. The PR comment records the script output and "đã đọc diff".
- [ ] **Step 7:** Edit both descriptions: "agent review" becomes "PRD approval check". In `docs/prd/README.md` step 4, add the `metadata` convention for rows after approval.
- [ ] **Step 8: Commit.**

### Task 2: Smaller always-loaded context (item 2)

**Files:**
- Create: `docs/process/decisions.md`
- Modify: `docs/process/lifecycle.md` (§8 freeze line, §9), `.claude/rules/serena.md` (frontmatter), `.claude/agents/solution-architect.md:28`, `AGENTS.md` § Decision Log, memory `agent-model-tiering.md`, `sdd-speed-group-a.md`

- [ ] **Step 1:** Move every `### ` entry under `lifecycle.md` §9, unchanged, into `docs/process/decisions.md`. The new file gets a one-paragraph header: append-only, newest first, what belongs here. §9 keeps only its title and one line pointing to the new file.
- [ ] **Step 2:** Repoint `lifecycle.md` §8 ("một mục ở §9"), `solution-architect.md` ("`lifecycle.md` § 9"), `AGENTS.md` ("Working process … `docs/process/lifecycle.md`") and both memory files to `docs/process/decisions.md`.
- [ ] **Step 3:** Add `paths: ["backend/**", "frontend/**"]` frontmatter to `serena.md`, in the same format as `comments.md`.
- [ ] **Step 4: Verify.** `git diff --stat` shows `lifecycle.md` shrinking by about as many lines as `decisions.md` gains. Check that every entry heading moved: `grep -c '^### 20' docs/process/decisions.md` equals the count taken before the move.
- [ ] **Step 5: Commit.**

### Task 3: One Opus review for tier 2 (item 3)

**Files:**
- Modify: `docs/process/lifecycle.md` § Hạng kiểm tra, `.claude/agents/pr-reviewer.md` (frontmatter), `docs/process/decisions.md` (new entry, Task 8)

- [ ] **Step 1:** In the tier-2 row, replace "review Opus cuối phase" with "`pr-reviewer` chạy Opus là review cuối phase; không có lượt review cuối riêng". The tier-1 row stays: `pr-reviewer` on Sonnet.
- [ ] **Step 2:** Add `model: sonnet` to the `pr-reviewer.md` frontmatter. The main session passes `model: opus` for a tier-2 PR. State this in the same tier-2 cell.
- [ ] **Step 3: Commit** (with Task 4).

### Task 4: Token numbers in the metrics (item 4)

**Files:**
- Modify: `docs/process/lifecycle.md` (§8 step 4, §10), `docs/process/progress.md` (§ Số đo)

- [ ] **Step 1:** §10 and §8 step 4. Each agent's completion notice carries `subagent_tokens`. At the end of each phase, the main session writes the totals per agent type and tier under `## Verification` of the phase PR. The retro sums them into a new `progress.md` column `Token agent (nghìn)`. The main session's own tokens are not measured.
- [ ] **Step 2:** In `progress.md` § Số đo, add the column. Module 00's cell is `—`.
- [ ] **Step 3: Commit** (Tasks 3–4 together: `docs(process): one Opus review for tier 2 and count agent tokens`).

### Task 5: One docs PR per module (item 5)

**Files:**
- Modify: `docs/process/lifecycle.md` (§5 first bullet), `docs/prd/README.md` (§ Quy trình steps 2 and 4), `docs/tech/README.md` (line 3 if it names a PR)

- [ ] **Step 1: §5.**
  - One branch `chore/<module>-docs` and one draft PR per module carry the PRD, then the technical design.
  - Design links are recorded in the PRD's `Thiết kế` row.
  - Each approval is committed on that branch: PRD *Approved* first, then tech *Approved*.
  - The PR is merged once, through `/git-accept`, when the module's documents are all *Approved*.
  - Downstream agents read the documents from the branch.
  - A module whose PRD is approved but whose design is still blocked may merge the PRD alone, if the user asks.
- [ ] **Step 2:** In `docs/prd/README.md`, step 4 changes from "merge qua `/git-accept`" to "commit bản duyệt lên nhánh tài liệu của module (`lifecycle.md` § Commit tài liệu)".
- [ ] **Step 3: Commit** (with Task 6).

### Task 6: Dark boards only for new patterns (item 6)

**Files:**
- Modify: `.claude/agents/ui-designer.md` (rule 1), `docs/process/lifecycle.md` §1 row 2

- [ ] **Step 1: `ui-designer.md` rule 1.**
  - System, Components and any **new** screen pattern or interaction get a light and a dark board, laid out as today.
  - A screen built only from existing patterns gets a light board. The tokens give its dark version, and its board names the dark pattern boards it relies on.
  - The rest of rule 1 (layout, no inversion) stays.
- [ ] **Step 2:** In §1 row 2 "Xong khi", add "bảng tối chỉ cho mẫu mới".
- [ ] **Step 3: Commit** `docs(process): one docs PR per module and dark boards only for new patterns`.

### Task 7: No CodeGraph prompt hook in this repo (item 7)

**Files:**
- Modify: `~/.claude/settings.json` (`hooks.UserPromptSubmit`, the `codegraph prompt-hook` entry). This is outside the repo.
- Create: `.claude/no-codegraph-prompt-hook`, an empty marker file, tracked.

- [ ] **Step 1:** Load `update-config`. Change the command to `[ -e "$CLAUDE_PROJECT_DIR/.claude/no-codegraph-prompt-hook" ] || codegraph prompt-hook`. stdin still reaches `codegraph` when the marker is absent.
- [ ] **Step 2:** Create the marker file.
- [ ] **Step 3: Verify.**
  - Run `echo '{"prompt":"how does TokenIssuer work","cwd":"'$PWD'"}' | CLAUDE_PROJECT_DIR=$PWD sh -c '<command>'`. Expected: no output.
  - Run the same with `CLAUDE_PROJECT_DIR=/tmp`. Expected: CodeGraph output, so the hook is unchanged elsewhere.
- [ ] **Step 4: Commit** the marker with Task 8.

### Task 8: Cheaper main session and the decision entry (items 7–8)

**Files:**
- Modify: `docs/process/lifecycle.md` § Hạng kiểm tra (one paragraph), `docs/process/decisions.md` (new entry), memory `sdd-speed-group-a.md`

- [ ] **Step 1: Add the paragraph "Phiên chính khi chạy kế hoạch".**
  - Once a plan is approved, the main session suggests `/model sonnet` to the user for execution.
  - Opus stays for brainstorming, planning and tier-2 reviews.
  - Each phase starts in a new session, resuming from the plan's ledger.
  - The user decides; the main session never switches models itself.
- [ ] **Step 2: Prepend the decision entry** `### 2026-11-12 — Bớt chi phí nền và thủ tục tài liệu` to `decisions.md`.
  - It lists items 1–8 with one reason each.
  - It notes that tier 1 skipping Opus departs from `~/.claude/CLAUDE.md`'s "final review: opus, once" for this repo, by the user's order.
  - Rejected options:
    - A script that checks stale paths in docs: no repeated breakage yet (§8 threshold).
    - Disabling all hooks: it would drop the bash guard.
- [ ] **Step 3: Gate.** Run `bun test scripts/`, `bun scripts/check-comments.ts` and `bun scripts/risk-tier.ts`. Expected: tier 2, all green.
- [ ] **Step 4: Commit** `docs(process): record the overhead cuts`.

## Not in this plan

- Items 9–11 (shorter `testing.md` and `api-contract.md`, `qa-frontend` for e2e only, a shorter Pre-Task Protocol) wait for the module 01 retro.
