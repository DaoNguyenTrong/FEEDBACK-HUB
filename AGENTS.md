# AGENTS.md

This file provides guidance to Claude Code when working with this repository.

## Monorepo Layout

| Path        | Stack                                                                     | Relevant rules                                                                              |
| ----------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `backend/`  | .NET 10, Clean Architecture (Domain → Application → Infrastructure → API) | `architecture.md`, `code-conventions.md`, `authentication.md`, `commands.md`, `comments.md` |
| `frontend/` | Vue 3 + Vite + TypeScript, Pinia, naive-ui, vue-i18n, Tailwind            | `frontend-conventions.md`, `commands.md`, `comments.md`                                     |
| `shared/`   | `openapi/` (contract exported from `backend`, consumed by `frontend`)     | `api-contract.md`                                                                           |

The backend's OpenAPI export (`shared/openapi/openapi.json`, committed) is the single source of truth for the API; `orval` regenerates `frontend/src/api/generated/**` (gitignored) from it on every `bun install` — workflow in `api-contract.md`.

## Software Lifecycle

Work runs from idea to release, not only from code. Full detail (work sizes S/M/L, gates, change management, traceability): `docs/process/lifecycle.md`; per-module progress: `docs/process/progress.md`.

| Stage              | Output                         | Owner                                                              |
| ------------------ | ------------------------------ | ------------------------------------------------------------------ |
| 0 Idea and vision  | module list, priorities        | main session (`brainstorming`) with the user                       |
| 1 Requirements     | PRD in `docs/prd/`             | `product-manager`, reviewed by `business-analyst`; user approves   |
| 2 UI design        | boards on the design canvas    | `ui-designer`; user approves                                       |
| 3 Technical design | `docs/tech/`                   | `tech-lead`; `appsec` reviews it before approval when security-critical |
| 4 Plan             | `docs/superpowers/plans/`      | main session (`writing-plans`); user approves, may authorize PR    |
| 5 Implementation   | code, tests, feature-map entry | `implementer`, one plan task per call                              |
| 6 Review and merge | PR merged into `dev`           | `pr-reviewer` (docs-only PR: `/git-accept`)                        |
| 7 Release          | tag, changelog                 | `release-kit:git-release`; user approves                           |

The main session orchestrates: it asks the user, relays answers, and calls agents in order. Subagents cannot ask the user or call each other. A business question found at any later stage goes back to the PRD; nothing downstream proceeds on an unapproved upstream document.

## Common Commands

```bash
backend/scripts/test.sh                          # backend tests
bun run --cwd frontend test:run                  # frontend unit tests
bun run --cwd frontend build-only                # writes src/typings/ — required before type-check
bun run --cwd frontend type-check
scripts/test-gate.sh                             # full local gate before push (no coverage)
```

Full list (build, dev, codegen, migrations, OpenAPI export): `.claude/rules/commands.md`.
CI workflows and git hooks (Lefthook): `.claude/reference/ci-and-git-hooks.md`.

## Agent Workflow

### Pre-Task Protocol

The step-by-step implementation protocol (memory → live state → CodeGraph/Serena orientation → impact check → feature map → reproduce-first → execute → verify gates → decision log) lives in `.claude/agents/implementer.md` § Pre-Task Protocol, the single source. Code tasks — plan tasks and S-size fixes alike — go to the `implementer` agent, which runs it; the main session does not edit `backend/`, `frontend/` or `shared/` itself. Its orientation steps 1–4 (memory → live state → CodeGraph → Serena) also apply to any agent exploring code.

**Recurring feedback:** if the same correction comes up twice (from a reviewer or from your own earlier work), stop and turn it into a lint rule, architecture test, or generator instead of adding another prose rule.

**Worktrees** lack every gitignored file (config, generated client, typings); read `.claude/reference/worktrees.md` before creating or working in one.

### Commits

- A question is not an order to commit — "is this ready?" or "what changed?" gets an answer, not a commit.
- Run `git branch --show-current` before every commit; never commit on `main` (the Lefthook branch guard blocks it) or on a branch you didn't expect.
- Stage only the files the task changed, by explicit path — never `git add -A`/`git add .`/`git commit -a`; leave changes that predate the task unstaged. Subagents working in parallel each commit their own paths, so one never sweeps up another's half-finished files.
- A commit rejected by Lefthook (commitlint, prettier, secretlint) means fix the message or re-stage — not `--no-verify`.
- Push or open a PR only on the user's explicit order. The user may give that order once for a whole plan when approving it (recorded in the plan and in the brief to `implementer`): after every task is done and `scripts/test-gate.sh` passes, one PR into `dev`. Never for `main`. The user's approval of a PRD or technical design is also that order, for one docs-only PR carrying it.
- Every PR description has a `## Changelog` section in the `release-kit:git-mr` format (`- Added|Changed|Deprecated|Removed|Fixed|Security: <one reader-facing sentence>`, or just `none` for internal changes), followed by another `## ` section — `git-release` builds the release CHANGELOG from it and reads it up to the next `## ` heading, so trailing free text (the attribution line) would make it invalid.
- One PR into `dev` at a time: rebase it onto the latest `origin/dev` (then `git push --force-with-lease`), merge with `--merge`, delete the merged branch on the remote and locally (`git branch -d`, never `-D`), then rebase the next one — near-linear history with one merge commit per PR (`docs/process/lifecycle.md` § Quyền push và mở PR).
- Merge into `dev` only through a gate: `/git-accept` for a docs-only PR (see `.claude/commands/git-accept.md`), or the `pr-reviewer` agent for any other PR. `pr-reviewer` allows at most 3 revision rounds, then escalates for a design review; the areas it never merges are listed once, in `pr-reviewer.md` § 4 — those need the user's explicit order, as does any merge outside `dev` (hotfix, release).

### Tool Selection

Orient with CodeGraph (`codegraph_explore`): how modules connect, what calls what across layers, and what breaks if you change something (its blast radius is the impact check for all frontend symbols). Then drill in with Serena, then `Read`. Never grep first; never open a file to orient.

| When you already know which symbol to inspect… | Use                                           | Scope                                                  |
| ---------------------------------------------- | --------------------------------------------- | ------------------------------------------------------ |
| Read a class or method body                    | Serena `find_symbol` with `include_body=true` | backend + frontend                                     |
| Confirm what a specific usage resolves to      | Serena `find_declaration`                     | backend + frontend                                     |
| Scan a file's public surface                   | Serena `get_symbols_overview`                 | backend + frontend                                     |
| Find every caller / usage of a symbol          | Serena `find_referencing_symbols`             | **backend C# only** — frontend: CodeGraph blast radius |
| Rename a symbol across the codebase            | Serena `rename_symbol`                        | **backend C# only** — frontend: see `serena.md`        |
| Broad keyword / file search                    | `grep` (quick), Explore agent (broad)         | —                                                      |

Why the frontend limits exist: `.claude/rules/serena.md`.

### Superpowers Integration

If the superpowers plugin is installed, its skills yield to this file. Where a skill's default disagrees, these win:

- **Where it fits** — brainstorming / systematic-debugging / writing-plans run after the protocol's orientation steps 1–4: their "explore the context" _is_ memory → live state → CodeGraph → Serena.
- **Documents** — a module's requirements go in `docs/prd/` (PRD, written by `product-manager`) and its technical design in `docs/tech/` (written by `tech-lead`) instead of the plugin's default `docs/superpowers/specs/`; brainstorming interviews feed those, they don't create a spec of their own. Implementation plans go in `docs/superpowers/plans/`. Root `plans/` holds the earlier numbered plans — don't add new ones there. Commit timing follows `docs/process/lifecycle.md` § Commit tài liệu (a PRD or technical design is committed when the user approves it; a plan travels on the feature branch), whatever the skill's own "commit the design doc" step says.
- **Per-module flow** — the stages in § Software Lifecycle, sized S/M/L per `docs/process/lifecycle.md`. Plan execution: the main session uses `subagent-driven-development` to hand `implementer` one task at a time and runs `code-reviewer` between tasks; `implementer` itself uses `executing-plans`, TDD, debugging, verification and `receiving-code-review`.
- **Requirement gaps stop the run** — subagent-driven development's "rulings, not stalls" covers technical ambiguity only. A missing or conflicting business rule or acceptance criterion means stop and ask; never write a ruling that invents business behavior.
- **Branch & worktree** — work in the current checkout on one `feature/*` branch cut from `dev`. Don't create a worktree for a skill run: the gitignored config and generated client are missing there. Subagents that build the backend run one at a time (`-m:1`, shared `obj/`).
- **Finishing** — base is `dev` (`main` only for a hotfix); never merge locally or delete an unmerged branch (merged branches are deleted per § Commits), and don't use `superpowers:finishing-a-development-branch` (its local-merge option conflicts with this rule; merging is `pr-reviewer`'s gate). Without an explicit order to push or open a PR, leave the branch as it is.

## Critical Invariants

**Entity Pattern** — entities are built only through their `static Create(XxxParams p)` factory (validation in `Update`), never via EF, Mapperly or by hand. Details: `code-conventions.md` § Entity Pattern; enforced by `ArchitectureTests.cs`.

## Decision Log

Decisions live where their scope lives; none needs a separate approval:

- **Module** (its API, data model, authorization) — only in §12 of that module's `docs/tech/` document, approved with it. A code change with a major deviation from it updates the document in the same PR; a minor one is only declared in the PR (`docs/process/lifecycle.md` § Lệch thiết kế khi triển khai).
- **Platform / cross-cutting** (infrastructure, deployment, shared auth mechanics, build/test tooling, codegen) — `.claude/decisions.md`, written by whoever makes the change, in the same PR. Append-only; rules in that file's header.
- **Working process** (agents, gates, review) — `docs/process/lifecycle.md`.
