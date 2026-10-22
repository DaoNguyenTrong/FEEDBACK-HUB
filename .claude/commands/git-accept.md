---
description: Merge the current branch's docs-only PR into dev when the gate passes (required CI green, agent review without blocking findings, docs-only scope)
---

Merge a docs-only pull request into `dev` through a gate. This is the merge path for docs-only PRs; other PRs into `dev` go through the `pr-reviewer` agent, and any merge outside `dev` (hotfix, release) needs the user's explicit order (`AGENTS.md` § Commits). The gate runs on the client, so it is not a hard control: it only enforces the process.

## Steps

### 1. Find the PR

```bash
gh pr view --json number,state,baseRefName,headRefName,url,title
```

Stop unless `state` is `OPEN`, `baseRefName` is `dev`, and `headRefName` starts with `feature/`, `fix/` or `chore/`. Report which condition failed.

Then check the branch is rebased onto the latest `dev` (one PR at a time, `docs/process/lifecycle.md`): `git fetch origin dev && git merge-base --is-ancestor origin/dev origin/<headRefName>` must exit 0; otherwise stop and ask for a rebase. Also check the description has a `## Changelog` section (`gh pr view <number> --json body`) — usually just `none` for docs; missing → stop and ask for it. Exception: a `chore/release-vX.Y.Z-changelog` PR opened by `/git-release` (quick release, `devMergeCommand`) carries the release's own `## [vX.Y.Z]` section instead and needs none; its only changed file must be the CHANGELOG (and any `versionFiles`).

### 2. Scope gate

```bash
gh pr view <number> --json files --jq '.files[].path'
```

Check each path in turn, in this order:

1. Under `.claude/` or `.github/`, or `AGENTS.md`, or under `docs/process/` other than `docs/process/progress.md` → fail, even when it ends in `.md` (rules, agent prompts and the process itself are reviewed by the user; the progress table is status the main session updates in the same PR as an approved document, `docs/process/lifecycle.md` § Tiến độ module).
2. Under `docs/`, or ends in `.md` → pass.
3. Anything else → fail.

Any failing path → stop and report the first offending file. Code goes through `pr-reviewer`; workflows, rules, settings, process documents and anything touching auth need the user.

### 3. CI gate

```bash
gh pr view <number> --json statusCheckRollup --jq '.statusCheckRollup[] | "\(.name)\t\(.conclusion // .status)"'
```

- `changes` and `test / comments` must be `SUCCESS` (`comments` never skips), and `test / backend`, `test / frontend` and `test / guide` must each be `SUCCESS` or `SKIPPED`. CI skips the parts a change does not touch and counts a skip as passing (see the header of `.github/workflows/ci.yml`), so a docs-only PR usually shows them skipped.
- Any of those checks missing, or any check `FAILURE`, `CANCELLED`, `PENDING`, `QUEUED` or `IN_PROGRESS` → stop. Do not wait in a loop; report which check is not finished.

### 4. Review gate

Dispatch the `code-reviewer` subagent (Agent tool, `subagent_type: code-reviewer`) and give it the PR number, so it reviews `gh pr diff <number>`. It checks the docs against the rules they describe (for example, a doc that names a file or command that no longer exists) and labels each finding `blocking` or `non-blocking`.

Post its output as a PR comment so the decision is on record:

```bash
gh pr comment <number> --body-file <file>
```

Write the body to the scratchpad directory, not `/tmp`. Any finding labelled `blocking` → stop and report it. Non-blocking findings do not stop the merge.

### 5. Merge

Only when steps 1–4 pass:

```bash
gh pr merge <number> --merge
```

Use `--merge` to match the repo's existing merge commits. Never pass `--admin`, `--auto` or `--delete-branch` here. Then delete the merged branch, remote first and only once it is inside `dev`, then local:

```bash
git fetch --prune origin && git merge-base --is-ancestor origin/<headRefName> origin/dev && git push origin --delete <headRefName>
git switch dev && git pull --ff-only origin dev && git branch -d <headRefName>
```

### 6. Finish

Print the merge commit and the PR URL. Print the deleted branch name. Remind the user to run `/git-sync` on any other branch that was based on `dev`.

## Rules

- Never merge a PR with a path that fails the scope gate in step 2
- Never bypass a failed gate with `--admin` or by editing the checks
- Never force push (rebasing the branch is the author's step, before this command)
- If any step fails, stop and report which gate failed and why
