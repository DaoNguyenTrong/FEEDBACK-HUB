import { describe, expect, test } from 'bun:test'
import { join } from 'node:path'
import { check } from './claude-bash-guard'

const HEREDOC_COMMIT = `git commit -m "$(cat <<'EOF'
chore: guard git push --force and git add .

git add -A, git commit --no-verify and gh pr merge --admin are now denied.
EOF
)"`

const DENY: Record<string, string[]> = {
  'force push': [
    'git push --force',
    'git push -f',
    'git push origin feature/x --force',
    'git push origin feature/x -f',
    'git push -fu origin feature/x',
    'git push -uf origin feature/x',
    'git push origin +feature/x',
    'git push origin +HEAD:feature/x',
    'git push origin "+"feature/x',
    'git push --forc origin feature/x',
    'git push --mirror origin',
    'git push --force-with-lease --force origin feature/x',
    'git -C /tmp/repo push --force',
    'git -c color.ui=never push origin x -f',
    '/usr/bin/git push -f',
    'command git push -f',
    'env FOO=1 git push -f',
    'timeout 30 git push -f',
    'bash -c "git push -f origin x"',
    "sh -lc 'git push origin +x'",
    'eval git push -f',
  ],
  'push to main or delete a long-lived branch': [
    'git push origin main',
    'git push origin HEAD:main',
    'git push origin feature/x:refs/heads/main',
    'git push origin refs/heads/main',
    'git push -u origin main',
    'git push origin :main',
    'git push origin :dev',
    'git push origin --delete dev',
    'git push origin -d main',
    'git push --delete origin feature/x dev',
  ],
  'commit hook bypass': [
    'git commit --no-verify -m "x"',
    'git commit -m "x" --no-verify',
    'git commit -n -m "x"',
    'git commit -nm "x"',
    'git commit --no-verif -m "x"',
    'git -c core.hooksPath=/dev/null commit -m "x"',
    'git -c core.hookspath=/dev/null commit -m "x"',
    'git -ccore.hooksPath=/dev/null commit -m "x"',
    'git --config-env=core.hooksPath=HOOKS commit -m "x"',
    'LEFTHOOK=0 git commit -m "x"',
    'LEFTHOOK_EXCLUDE=prettier git commit -m "x"',
    'LEFTHOOK_SKIP_comments=1 git commit -m "x"',
    'LEFTHOOK_CONFIG=/tmp/empty.yml git commit -m "x"',
    'env LEFTHOOK=0 git commit -m "x"',
    'export LEFTHOOK=0 && git commit -m "x"',
    'export LEFTHOOK=0; git commit -m "x"',
    'GIT_CONFIG_PARAMETERS="\'core.hooksPath\'=\'/dev/null\'" git commit -m "x"',
    'git config core.hooksPath /dev/null',
    'git config --local core.hooksPath .nohooks',
    'git merge --no-verify feature/x',
  ],
  'commit everything': [
    'git commit -a -m "x"',
    'git commit -am "x"',
    'git commit --all -m "x"',
    'git commit -m "x" -a',
    'git commit --amend -a --no-edit',
    'git commit -m "x" .',
    'git commit -m "x" -- :/',
  ],
  'add everything': [
    'git add -A',
    'git add -A backend/',
    'git add --all',
    'git add --all --dry-run',
    'git add --al',
    'git add --no-ignore-removal',
    'git add .',
    'git add ./',
    'git add -- .',
    'git add :/',
    "git add ':(top)'",
    'git add -fA',
    'git stage -A',
  ],
  'force-delete a branch': [
    'git branch -D feature/x',
    'git branch -df feature/x',
    'git branch -fd feature/x',
    'git branch -d -f feature/x',
    'git branch --delete --force feature/x',
    'git branch -d --force feature/x',
    'git branch --force --delete feature/x',
  ],
  'gh merge bypass': [
    'gh pr merge 13 --merge --admin',
    'gh pr merge --admin 13',
    'gh pr merge 13 --merge --auto',
    'gh pr merge 13 --merge --delete-branch',
    'gh pr merge 13 --merge -d',
    'gh pr merge 13 -md',
    'gh pr merge 13 --admin=true',
    'gh repo delete',
    'gh repo delete DaoNguyenTrong/FEEDBACK-HUB --yes',
  ],
  'compound commands (denied part wins)': [
    'git status && git push -f',
    'git fetch origin; git push origin +x',
    'git log --oneline | head -1 || git branch -D x',
    'git add a.ts\ngit commit -nm "x"',
    'git status & git push -f',
    'echo "$(git push -f)"',
    'echo `git push -f`',
    '(cd /tmp && git push -f)',
    '{ git add -A; }',
    'if true; then git push -f; fi',
  ],
}

const ALLOW = [
  'git status',
  'git push --force-with-lease origin feature/x',
  'git push --force-with-lease=feature/x:0123abc origin feature/x',
  'git push --force-if-includes --force-with-lease origin feature/x',
  'git push -u origin chore/x',
  'git push -u origin release/v1.2.0',
  'git push origin v1.2.0',
  'git push origin --tags',
  'git push origin dev',
  'git push origin --delete hotfix/old-name',
  'git push origin main:feature/x',
  'git push -o ci.skip origin feature/x',
  'git commit -m "--force and -n in text"',
  'git commit -m "x" -m "git push -f, git add ., --no-verify"',
  'git commit -mn',
  'git commit -m "-a"',
  'git commit --no-edit',
  'git commit --amend --no-edit',
  'git commit -m "x" -- backend/a.cs frontend/b.ts',
  HEREDOC_COMMIT,
  'git add path/a path/b',
  'git add backend/',
  'git add -u backend/',
  'git add -- "file with spaces.ts"',
  'git branch -d feature/x',
  'git branch --delete feature/x',
  'git branch -f dev origin/dev',
  'git branch -m hotfix/v1.2.1',
  'git branch --format "%(refname) -D" --list',
  'git merge --no-ff origin/main -m "Merge main into dev after v1.2.0"',
  'git reset --hard origin/dev',
  'git clean -fdx',
  'git tag -a v1.2.0 -m "Release v1.2.0" origin/main',
  'git config --get core.hooksPath',
  'git config --unset core.hooksPath',
  'git -c core.editor=true rebase --continue',
  'gh pr merge 13 --merge',
  'gh pr merge 13 --merge --subject "Merge release/v1.2.0 --admin --auto"',
  'gh pr merge 13 --merge -t "-d"',
  'gh pr merge 13 --merge --auto=false',
  'gh pr create --base dev --title "x" --body "never gh pr merge --admin"',
  'gh pr view 13 --json state',
  'gh repo view',
  'echo "git push -f"',
  "printf '%s' 'git add -A'",
  'grep -n "git push --force" AGENTS.md',
  'LEFTHOOK_VERBOSE=1 git commit -m "x"',
  'FOO=1 git push -u origin feature/x',
  'git status && git push --force-with-lease origin feature/x',
  'ls -la # git push -f',
  'bun test scripts/',
]

describe('claude-bash-guard check', () => {
  for (const [rule, commands] of Object.entries(DENY)) {
    test.each(commands)(`denies ${rule}: %s`, (command) => {
      expect(check(command)).not.toBeNull()
    })
  }

  test.each(ALLOW)('allows %s', (command) => {
    expect(check(command)).toBeNull()
  })

  test('the reason names the allowed alternative', () => {
    expect(check('git push origin +x')).toContain('--force-with-lease')
    expect(check('git add -A')).toContain('explicit path')
    expect(check('git branch -D x')).toContain('git branch -d')
  })
})

describe('claude-bash-guard hook', () => {
  const run = (stdin: string) => {
    const proc = Bun.spawnSync(['bun', join(import.meta.dir, 'claude-bash-guard.ts')], {
      stdin: Buffer.from(stdin),
    })
    return { code: proc.exitCode, stdout: proc.stdout.toString() }
  }
  const payload = (command: string) =>
    JSON.stringify({ hook_event_name: 'PreToolUse', tool_name: 'Bash', tool_input: { command } })

  test('prints a PreToolUse deny decision for a denied command', () => {
    const result = run(payload('git push -f'))
    expect(result.code).toBe(0)
    const output = JSON.parse(result.stdout).hookSpecificOutput
    expect(output.hookEventName).toBe('PreToolUse')
    expect(output.permissionDecision).toBe('deny')
    expect(output.permissionDecisionReason).toContain('--force-with-lease')
  })

  test('prints nothing for an allowed command', () => {
    expect(run(payload('git push --force-with-lease origin x'))).toEqual({ code: 0, stdout: '' })
  })

  test('denies a git command it cannot read instead of letting it through', () => {
    const result = run(payload('git commit -m "unterminated'))
    expect(JSON.parse(result.stdout).hookSpecificOutput.permissionDecision).toBe('deny')
  })

  test('stays silent on unreadable input that is not about git or gh', () => {
    expect(run('not json')).toEqual({ code: 0, stdout: '' })
  })
})
