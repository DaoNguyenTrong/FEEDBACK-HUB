// Computes a change's risk tier; the rule each tier triggers is docs/process/lifecycle.md § Hạng kiểm tra.
// Usage: bun scripts/risk-tier.ts [--base <ref>] [--head <ref>]   (default origin/dev...HEAD)

import { spawnSync } from 'node:child_process'

export type Tier = 0 | 1 | 2
export type ChangedFile = { path: string; changedLines: string[] }

const SENSITIVE_WORDS =
  'Auth|Token|Cookie|Session|Permission|Tenant|ApiKey|Lockout|Role|Organization|Invitation|Member'

export const TIER2_PATHS: RegExp[] = [
  /^\.github\//i,
  /^lefthook\.ya?ml$/i,
  /^scripts\/risk-tier(\.test)?\.ts$/i,
  /\/Migrations\//i,
  /^backend\/.*\/(Auth|Authorization|ApiKeys|Organizations|Roles|Invitations|Members)\//i,
  new RegExp(`^backend/.*(${SENSITIVE_WORDS})[^/]*\\.cs$`, 'i'),
  /^backend\/src\/FeedbackHub\.API\/Program\.cs$/i,
  /^backend\/src\/FeedbackHub\.Infrastructure\/Persistence\/AppDbContext\.cs$/i,
  /^frontend\/src\/api\/(client|mutator)\.ts$/i,
  /^frontend\/src\/router\//i,
  /^frontend\/src\/stores\/(auth|organizations|roles)\.ts$/i,
]

export const TIER2_CONTENT: RegExp[] = [
  /\[(AllowAnonymous|Authorize)\b/,
  /HasQueryFilter|IgnoreQueryFilters|AddAuthentication|AddAuthorization|ExecuteSqlRaw|FromSqlRaw|withCredentials/,
]

export const TIER0_PATHS: RegExp[] = [
  /^docs\//i,
  /\.md$/i,
  /^shared\/openapi\//i,
  /^frontend\/src\/api\/generated\//i,
  /^frontend\/src\/locales\//i,
  /^\.claude\//i,
  /^\.codex\//i,
  /^\.agents\//i,
]

export const TEST_PATHS: RegExp[] = [/^backend\/tests\//i, /^frontend\/tests\//i, /\.test\.ts$/i]

const CONTENT_CHECKED = /\.(cs|ts|vue)$/i

const matches = (patterns: RegExp[], text: string) => patterns.some((p) => p.test(text))

// A test under a sensitive path stays tier 1: deleting its assertions weakens a tier-2 guard.
const isSensitiveTestPath = (path: string) =>
  matches(TIER2_PATHS, path) || new RegExp(`(${SENSITIVE_WORDS})`, 'i').test(path)

export function tierOf(path: string): Tier {
  if (matches(TEST_PATHS, path)) return isSensitiveTestPath(path) ? 1 : 0
  if (matches(TIER2_PATHS, path)) return 2
  if (matches(TIER0_PATHS, path)) return 0
  return 1
}

function fileTier({ path, changedLines }: ChangedFile): { tier: Tier; why: string } {
  const tier = tierOf(path)
  if (tier === 2) return { tier, why: 'sensitive path' }
  if (tier === 1 && !matches(TEST_PATHS, path) && CONTENT_CHECKED.test(path)) {
    const line = changedLines.find((l) => matches(TIER2_CONTENT, l))
    if (line !== undefined) return { tier: 2, why: `changes "${line.trim()}"` }
  }
  return { tier, why: tier === 0 ? 'docs, generated or test' : 'ordinary code' }
}

export function tierOfChange(files: ChangedFile[]): { tier: Tier; reasons: string[] } {
  const rated = files.map((f) => ({ path: f.path, ...fileTier(f) }))
  const tier = rated.reduce<Tier>((max, r) => (r.tier > max ? r.tier : max), 0)
  const reasons = rated
    .filter((r) => r.tier === tier)
    .slice(0, 5)
    .map((r) => `${r.path}: ${r.why}`)
  return { tier, reasons }
}

// Fails closed: an unreadable diff must never print a tier, because tier 0 would drop every check.
function git(args: string[]): string {
  const r = spawnSync('git', args, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 })
  if (r.status !== 0) {
    process.stderr.write(`risk-tier: cannot run git ${args.join(' ')}\n${r.stderr ?? ''}`)
    process.exit(2)
  }
  return r.stdout
}

export function changedFiles(range: string): ChangedFile[] {
  const paths = git(['diff', '--no-renames', '--name-only', range]).split('\n').filter(Boolean)
  return paths.map((path) => ({
    path,
    changedLines: git(['diff', '--no-renames', '-U0', range, '--', path])
      .split('\n')
      .filter((l) => /^[+-](?![+-]{2} )/.test(l))
      .map((l) => l.slice(1)),
  }))
}

function argValue(args: string[], flag: string): string | undefined {
  const i = args.indexOf(flag)
  return i >= 0 ? args[i + 1] : undefined
}

if (import.meta.main) {
  const args = process.argv.slice(2)
  const base = argValue(args, '--base') ?? 'origin/dev'
  const head = argValue(args, '--head') ?? 'HEAD'
  const { tier, reasons } = tierOfChange(changedFiles(`${base}...${head}`))
  console.log(`Risk tier: ${tier}`)
  for (const reason of reasons) console.log(`- ${reason}`)
}
