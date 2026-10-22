// Claude Code PreToolUse hook for Bash (.claude/settings.json): denies the git/gh "never" rules of
// AGENTS.md § Commits in any spelling, which prefix-matched permission rules miss.
// Usage: the hook payload JSON on stdin; prints a deny decision or nothing, always exits 0.

const SUBSTITUTION = '$()'
const MAX_DEPTH = 8

const REASON = {
  force:
    'Force push is never allowed (AGENTS.md § Commits). After a rebase, push your own branch with ' +
    '`git push --force-with-lease`.',
  main: 'Never push to main directly: it changes only through a release PR (release-kit:git-release). Push your branch and open a PR.',
  longLived:
    'main and dev are long-lived branches and are never deleted. Delete only merged branches: ' +
    '`git push origin --delete <branch>`.',
  hooks:
    'Never bypass the Lefthook hooks (AGENTS.md § Commits): no --no-verify/-n, core.hooksPath ' +
    'override or LEFTHOOK variables. Fix what the hook rejected and commit again.',
  commitAll:
    'Commit only explicitly staged paths (AGENTS.md § Commits): no `git commit -a`/`--all` or a ' +
    'whole-tree pathspec. Run `git add <path>...`, then `git commit`.',
  addAll:
    'Stage explicit paths only (AGENTS.md § Commits): no `git add -A`/`--all`/`.`/`:/`. Use ' +
    '`git add <path>...`.',
  branchDelete:
    'Delete local branches with `git branch -d`, which refuses unmerged work; never `-D` or ' +
    '`--delete --force` (AGENTS.md § Commits).',
  ghMerge:
    'Merge with `gh pr merge <n> --merge` only, never --admin, --auto or --delete-branch ' +
    '(pr-reviewer.md, git-accept.md). Then delete the branch with `git push origin --delete <branch>` ' +
    'and `git branch -d <branch>`.',
  repoDelete: 'Deleting the GitHub repository is never done by an agent; ask the user.',
  unreadable:
    'The Bash guard could not parse this git/gh command (unbalanced quote or substitution). ' +
    'Rewrite it in plain shell syntax.',
}

type Redirect = 'target' | 'heredoc' | 'heredoc-strip' | null

// Splits a shell string into simple commands of quote-removed words. Command substitutions become
// commands of their own; heredoc bodies and comments are data. Throws on an unterminated construct.
class ShellParser {
  private pos = 0
  private readonly heredocs: { delimiter: string; stripTabs: boolean }[] = []
  private readonly commands: string[][] = []

  constructor(private readonly src: string) {}

  parse(): string[][] {
    this.list(false)
    return this.commands
  }

  private list(inSubstitution: boolean): void {
    const src = this.src
    let words: string[] = []
    let word: string | null = null
    let redirect: Redirect = null
    let depth = 0
    const endWord = () => {
      if (word === null) return
      if (redirect === 'heredoc' || redirect === 'heredoc-strip')
        this.heredocs.push({ delimiter: word, stripTabs: redirect === 'heredoc-strip' })
      else if (redirect === null) words.push(word)
      redirect = null
      word = null
    }
    const endCommand = () => {
      endWord()
      redirect = null
      if (words.length) this.commands.push(words)
      words = []
    }

    while (this.pos < src.length) {
      const c = src[this.pos]
      const next = src[this.pos + 1]
      if (c === ')' && depth === 0 && inSubstitution) {
        this.pos++
        endCommand()
        return
      }
      if (c === ' ' || c === '\t') {
        endWord()
        this.pos++
      } else if (c === '\\' && next === '\n') {
        this.pos += 2
      } else if (c === '\n') {
        endCommand()
        this.pos++
        this.skipHeredocs()
      } else if (c === '#' && word === null) {
        while (this.pos < src.length && src[this.pos] !== '\n') this.pos++
      } else if ((c === '<' || c === '>') && next === '(') {
        this.pos += 2
        this.list(true)
        word = (word ?? '') + SUBSTITUTION
      } else if (c === '<' || c === '>' || (c === '&' && next === '>')) {
        if (word !== null && /^\d+$/.test(word)) word = null
        endWord()
        redirect = this.redirection()
      } else if (c === ';' || c === '&' || c === '|') {
        endCommand()
        this.pos++
      } else if (c === '(' || c === ')') {
        endCommand()
        if (c === '(') depth++
        else if (depth > 0) depth--
        this.pos++
      } else {
        word = (word ?? '') + this.wordPart()
      }
    }
    if (inSubstitution) throw new Error('unterminated $(')
    endCommand()
  }

  private wordPart(): string {
    const src = this.src
    const c = src[this.pos]
    if (c === "'") {
      const end = src.indexOf("'", this.pos + 1)
      if (end < 0) throw new Error("unterminated '")
      const text = src.slice(this.pos + 1, end)
      this.pos = end + 1
      return text
    }
    if (c === '"') return this.doubleQuoted()
    if (c === '$') return this.dollar()
    if (c === '`') return this.backtick()
    if (c === '\\') {
      this.pos += 2
      return src[this.pos - 1] ?? ''
    }
    this.pos++
    return c
  }

  private doubleQuoted(): string {
    const src = this.src
    let text = ''
    this.pos++
    while (this.pos < src.length) {
      const c = src[this.pos]
      const next = src[this.pos + 1]
      if (c === '"') {
        this.pos++
        return text
      }
      if (c === '\\' && next !== undefined && '$`"\\\n'.includes(next)) {
        text += next === '\n' ? '' : next
        this.pos += 2
      } else if (c === '$' && (next === '(' || next === '{')) {
        text += this.dollar()
      } else if (c === '`') {
        text += this.backtick()
      } else {
        text += c
        this.pos++
      }
    }
    throw new Error('unterminated "')
  }

  private dollar(): string {
    const src = this.src
    const next = src[this.pos + 1]
    if (next === "'") {
      let text = ''
      this.pos += 2
      while (this.pos < src.length && src[this.pos] !== "'") {
        if (src[this.pos] === '\\') this.pos++
        text += src[this.pos] ?? ''
        this.pos++
      }
      if (this.pos >= src.length) throw new Error("unterminated $'")
      this.pos++
      return text
    }
    if (next === '(' && src[this.pos + 2] === '(') return this.skipBalanced('(', ')')
    if (next === '(') {
      this.pos += 2
      this.list(true)
      return SUBSTITUTION
    }
    if (next === '{') return this.skipBalanced('{', '}')
    this.pos++
    return '$'
  }

  // Returns `$((...))` or `${...}` verbatim: arithmetic and parameter expansion run no command.
  private skipBalanced(open: string, close: string): string {
    const start = this.pos
    let depth = 0
    for (this.pos++; this.pos < this.src.length; this.pos++) {
      const c = this.src[this.pos]
      if (c === open) depth++
      else if (c === close && --depth === 0) {
        this.pos++
        return this.src.slice(start, this.pos)
      }
    }
    throw new Error(`unterminated $${open}`)
  }

  private backtick(): string {
    const src = this.src
    let body = ''
    let end = this.pos + 1
    while (end < src.length && src[end] !== '`') {
      if (src[end] === '\\' && '\\`$'.includes(src[end + 1] ?? '')) end++
      body += src[end++]
    }
    if (end >= src.length) throw new Error('unterminated `')
    this.pos = end + 1
    this.commands.push(...new ShellParser(body).parse())
    return SUBSTITUTION
  }

  private redirection(): Redirect {
    const src = this.src
    if (src.startsWith('<<<', this.pos)) {
      this.pos += 3
      return 'target'
    }
    if (src.startsWith('<<-', this.pos)) {
      this.pos += 3
      return 'heredoc-strip'
    }
    if (src.startsWith('<<', this.pos)) {
      this.pos += 2
      return 'heredoc'
    }
    if (src[this.pos] === '&') this.pos++
    this.pos++
    if ('>&|'.includes(src[this.pos] ?? '')) this.pos++
    return 'target'
  }

  private skipHeredocs(): void {
    const src = this.src
    for (const { delimiter, stripTabs } of this.heredocs.splice(0)) {
      while (this.pos < src.length) {
        const eol = src.indexOf('\n', this.pos)
        const end = eol < 0 ? src.length : eol
        const line = src.slice(this.pos, end)
        this.pos = end + 1
        if ((stripTabs ? line.replace(/^\t+/, '') : line) === delimiter) break
      }
    }
  }
}

type Option = { name: string; long: boolean; value?: string }
type Spec = {
  shortValue?: string
  shortAttachedValue?: string
  longValue?: string[]
  abbreviations?: boolean
}

function parseArgs(args: string[], spec: Spec) {
  const options: Option[] = []
  const positionals: string[] = []
  const takesValue = (name: string) =>
    (spec.longValue ?? []).some(
      (long) => long === name || (spec.abbreviations && long.startsWith(name)),
    )
  let endOfOptions = false
  for (let i = 0; i < args.length; i++) {
    const arg = args[i]
    if (endOfOptions || arg === '-' || !arg.startsWith('-')) {
      positionals.push(arg)
    } else if (arg === '--') {
      endOfOptions = true
    } else if (arg.startsWith('--')) {
      const eq = arg.indexOf('=')
      const name = eq < 0 ? arg.slice(2) : arg.slice(2, eq)
      const value = eq >= 0 ? arg.slice(eq + 1) : takesValue(name) ? args[++i] : undefined
      options.push({ name, long: true, value })
    } else {
      for (let j = 1; j < arg.length; j++) {
        const name = arg[j]
        const rest = arg.slice(j + 1)
        if (spec.shortValue?.includes(name)) {
          options.push({ name, long: false, value: rest || args[++i] })
          break
        }
        if (spec.shortAttachedValue?.includes(name)) {
          options.push({ name, long: false, value: rest })
          break
        }
        options.push({ name, long: false })
      }
    }
  }
  return { options, positionals }
}

// git accepts any unambiguous prefix of a long option; an ambiguous one fails, so denying it is safe.
const isLong = (option: Option, name: string, abbreviations = true) =>
  option.long && (option.name === name || (abbreviations && name.startsWith(option.name)))
const isShort = (option: Option, name: string) => !option.long && option.name === name
const isTrue = (option: Option) =>
  option.value === undefined || !/^(false|f|0)$/i.test(option.value)

const isHooksPath = (config: string) => config.split('=')[0].toLowerCase() === 'core.hookspath'

function bypassesHooks(name: string, value: string): boolean {
  if (name === 'LEFTHOOK') return !/^(1|true)$/i.test(value)
  if (/^LEFTHOOK_(EXCLUDE|SKIP|CONFIG|BIN)/.test(name)) return true
  return (
    (name === 'GIT_CONFIG_PARAMETERS' || /^GIT_CONFIG_KEY_\d+$/.test(name)) &&
    /core\.hookspath/i.test(value)
  )
}

const branchName = (ref: string) => ref.replace(/^\+/, '').replace(/^refs\/heads\//, '')

// A pathspec that names the whole tree from the repo root or the working directory.
function isWholeTree(pathspec: string): boolean {
  const magic = /^:(\/|\([^)]*\)|[!^]+)/.exec(pathspec)
  const rest = magic ? pathspec.slice(magic[0].length) : pathspec
  if (!magic && rest === '') return false
  const segments = rest.split('/').filter((segment) => segment !== '' && segment !== '.')
  return (
    segments.length === 0 ||
    (segments.length === 1 && segments[0] === '*') ||
    segments.every((segment) => segment === '..')
  )
}

function checkPush(args: string[]): string | null {
  const { options, positionals } = parseArgs(args, {
    shortValue: 'o',
    longValue: ['repo', 'receive-pack', 'exec', 'push-option'],
    abbreviations: true,
  })
  const force = options.some((o) => isLong(o, 'force') || isLong(o, 'mirror') || isShort(o, 'f'))
  if (force || positionals.some((p) => p.startsWith('+'))) return REASON.force
  const deleting = options.some((o) => isLong(o, 'delete') || isShort(o, 'd'))
  const refspecs = options.some((o) => isLong(o, 'repo')) ? positionals : positionals.slice(1)
  for (const refspec of refspecs) {
    const target = branchName(refspec.slice(refspec.lastIndexOf(':') + 1))
    const deletes = deleting || refspec.startsWith(':')
    if (deletes && (target === 'main' || target === 'dev')) return REASON.longLived
    if (!deletes && target === 'main') return REASON.main
  }
  return null
}

function checkCommit(args: string[]): string | null {
  const { options, positionals } = parseArgs(args, {
    shortValue: 'mFcCt',
    shortAttachedValue: 'Su',
    longValue: [
      'message',
      'file',
      'author',
      'date',
      'template',
      'reuse-message',
      'reedit-message',
      'fixup',
      'squash',
      'cleanup',
      'trailer',
      'pathspec-from-file',
    ],
    abbreviations: true,
  })
  if (options.some((o) => isLong(o, 'no-verify') || isShort(o, 'n'))) return REASON.hooks
  if (options.some((o) => isLong(o, 'all') || isShort(o, 'a'))) return REASON.commitAll
  if (positionals.some(isWholeTree)) return REASON.commitAll
  return null
}

function checkAdd(args: string[]): string | null {
  const { options, positionals } = parseArgs(args, {
    longValue: ['chmod', 'pathspec-from-file'],
    abbreviations: true,
  })
  const all = options.some(
    (o) => isLong(o, 'all') || isLong(o, 'no-ignore-removal') || isShort(o, 'A'),
  )
  return all || positionals.some(isWholeTree) ? REASON.addAll : null
}

function checkBranch(args: string[]): string | null {
  const { options } = parseArgs(args, {
    shortValue: 'u',
    longValue: [
      'set-upstream-to',
      'contains',
      'no-contains',
      'merged',
      'no-merged',
      'points-at',
      'sort',
      'format',
    ],
    abbreviations: true,
  })
  const deletes = options.some((o) => isLong(o, 'delete') || isShort(o, 'd'))
  const force = options.some((o) => isLong(o, 'force') || isShort(o, 'f'))
  return options.some((o) => isShort(o, 'D')) || (deletes && force) ? REASON.branchDelete : null
}

function checkConfig(args: string[]): string | null {
  const { options, positionals } = parseArgs(args, {
    shortValue: 'f',
    longValue: ['file', 'blob', 'type', 'default', 'comment', 'value'],
  })
  const key = positionals.findIndex(isHooksPath)
  if (key < 0) return null
  const reads = options.some((o) => (o.long && /^(get|unset|list)/.test(o.name)) || isShort(o, 'l'))
  if (reads || ['get', 'unset', 'list'].includes(positionals[0])) return null
  return positionals.length > key + 1 ? REASON.hooks : null
}

function checkGit(args: string[]): string | null {
  let i = 0
  for (; i < args.length && args[i].startsWith('-'); i++) {
    const arg = args[i]
    let config: string | undefined
    if (arg === '-c' || arg === '--config-env') config = args[++i]
    else if (arg.startsWith('-c')) config = arg.slice(2)
    else if (arg.startsWith('--config-env=')) config = arg.slice('--config-env='.length)
    else if (
      ['-C', '--git-dir', '--work-tree', '--namespace', '--super-prefix', '--attr-source'].includes(
        arg,
      )
    )
      i++
    if (config !== undefined && isHooksPath(config)) return REASON.hooks
  }
  const rest = args.slice(i + 1)
  switch (args[i]) {
    case 'push':
      return checkPush(rest)
    case 'commit':
      return checkCommit(rest)
    case 'add':
    case 'stage':
      return checkAdd(rest)
    case 'branch':
      return checkBranch(rest)
    case 'config':
      return checkConfig(rest)
    case 'merge':
      return parseArgs(rest, { shortValue: 'mFsX', shortAttachedValue: 'S' }).options.some((o) =>
        isLong(o, 'no-verify'),
      )
        ? REASON.hooks
        : null
    default:
      return null
  }
}

function checkGh(args: string[]): string | null {
  if (args[0] === 'repo' && args[1] === 'delete') return REASON.repoDelete
  if (args[0] !== 'pr' || args[1] !== 'merge') return null
  const { options } = parseArgs(args.slice(2), {
    shortValue: 'bFtAR',
    longValue: ['body', 'body-file', 'subject', 'author-email', 'match-head-commit', 'repo'],
  })
  const bypass = options.some(
    (o) =>
      isTrue(o) &&
      (isLong(o, 'admin', false) ||
        isLong(o, 'auto', false) ||
        isLong(o, 'delete-branch', false) ||
        isShort(o, 'd')),
  )
  return bypass ? REASON.ghMerge : null
}

const ASSIGNMENT = /^([A-Za-z_][A-Za-z0-9_]*)\+?=(.*)$/s
const KEYWORDS = new Set(['!', '{', '}', 'if', 'then', 'else', 'elif', 'while', 'until', 'do'])
const SHELLS = new Set(['bash', 'sh', 'zsh', 'dash', 'ksh'])
const DECLARE = new Set(['export', 'declare', 'typeset', 'local', 'readonly'])

// Skips options of a wrapper command; `withValue` lists the options that take the next word.
function skipOptions(words: string[], i: number, withValue: string[]): number {
  while (i < words.length && words[i].startsWith('-')) {
    if (words[i] === '--') return i + 1
    i += withValue.includes(words[i]) ? 2 : 1
  }
  return i
}

function checkSimple(words: string[], depth: number): string | null {
  let i = 0
  while (i < words.length) {
    const word = words[i]
    const assignment = ASSIGNMENT.exec(word)
    if (assignment) {
      if (bypassesHooks(assignment[1], assignment[2])) return REASON.hooks
      i++
      continue
    }
    if (KEYWORDS.has(word)) {
      i++
      continue
    }
    const name = word.slice(word.lastIndexOf('/') + 1)
    if (DECLARE.has(name)) {
      for (const arg of words.slice(i + 1)) {
        const declared = ASSIGNMENT.exec(arg)
        if (declared && bypassesHooks(declared[1], declared[2])) return REASON.hooks
      }
      return null
    }
    if (name === 'eval') return check(words.slice(i + 1).join(' '), depth + 1)
    if (SHELLS.has(name)) {
      const start = i + 1
      i = skipOptions(words, start, ['-o', '+o'])
      const inline = words.slice(start, i).some((o) => /^-[a-z]*c/.test(o))
      return inline && i < words.length ? check(words[i], depth + 1) : null
    }
    if (name === 'env') {
      const split = words.findIndex((w, k) => k > i && (w === '-S' || w === '--split-string'))
      if (split > 0) return check(words.slice(split + 1).join(' '), depth + 1)
      i = skipOptions(words, i + 1, ['-u', '--unset', '-C', '--chdir'])
    } else if (name === 'timeout') {
      i = skipOptions(words, i + 1, ['-s', '--signal', '-k', '--kill-after']) + 1
    } else if (name === 'nice') {
      i = skipOptions(words, i + 1, ['-n'])
    } else if (name === 'sudo') {
      i = skipOptions(words, i + 1, ['-u', '-g', '-C', '-D', '-h', '-p', '-r', '-t', '-U'])
    } else if (name === 'xargs') {
      i = skipOptions(words, i + 1, ['-I', '-L', '-n', '-P', '-s', '-d', '-E', '-a'])
    } else if (['command', 'exec', 'nohup', 'time', 'builtin'].includes(name)) {
      i = skipOptions(words, i + 1, ['-a'])
    } else if (name === 'git') {
      return checkGit(words.slice(i + 1))
    } else if (name === 'gh') {
      return checkGh(words.slice(i + 1))
    } else {
      return null
    }
  }
  return null
}

// The deny reason for a shell command string, or null when it may run. Throws when it can't parse.
export function check(command: string, depth = 0): string | null {
  if (depth > MAX_DEPTH) throw new Error('nested too deeply')
  for (const words of new ShellParser(command).parse()) {
    const reason = checkSimple(words, depth)
    if (reason) return reason
  }
  return null
}

function deny(reason: string) {
  const output = {
    hookEventName: 'PreToolUse',
    permissionDecision: 'deny',
    permissionDecisionReason: reason,
  }
  console.log(JSON.stringify({ hookSpecificOutput: output }))
}

// A crashed hook lets the command run, so every failure on a git/gh command becomes a deny.
async function main() {
  let text = ''
  try {
    text = await Bun.stdin.text()
    const command = JSON.parse(text)?.tool_input?.command
    if (typeof command !== 'string') return
    text = command
    const reason = check(command)
    if (reason) deny(reason)
  } catch {
    if (/\b(git|gh)\b/.test(text)) deny(REASON.unreadable)
  }
}

if (import.meta.main) await main()
