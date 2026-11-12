// Checks the approval order of an Approved PRD (docs/prd/README.md § Quy trình, step 4): a BA review row before the
// last user-approval row of the current version, and nothing but `metadata` rows after it.
// Usage: bun scripts/check-prd-approval.ts <prd.md>...   (exit 1 on any finding)

import { readFileSync } from 'node:fs'

type Row = { version: string; change: string; source: string }

const cells = (line: string) => line.split('|').slice(1, -1).map((c) => c.trim())

function headerValue(markdown: string, key: string): string | undefined {
  for (const line of markdown.split('\n')) {
    const c = cells(line)
    if (c.length === 2 && c[0] === key) return c[1]
  }
  return undefined
}

function historyRows(markdown: string): Row[] {
  return markdown
    .split('\n')
    .filter((l) => /^\|\s*\d{4}-\d{2}-\d{2}\s*\|/.test(l))
    .map(cells)
    .filter((c) => c.length >= 4)
    .map(([, version, change, source]) => ({ version, change, source }))
}

const isApproval = (r: Row) => r.change.startsWith('Người dùng duyệt') && r.source.includes('Quyết định của người dùng')

export function checkPrdApproval(markdown: string): string[] {
  if (headerValue(markdown, 'Trạng thái') !== 'Approved') return []
  const version = headerValue(markdown, 'Phiên bản') ?? ''
  const rows = historyRows(markdown).filter((r) => r.version === version)
  const approvalAt = rows.findLastIndex(isApproval)
  if (approvalAt < 0) return [`no user-approval row for v${version}`]

  const findings: string[] = []
  if (!rows.slice(0, approvalAt).some((r) => r.source.includes('BA review'))) {
    findings.push(`no BA review before the approval of v${version}`)
  }
  for (const r of rows.slice(approvalAt + 1)) {
    if (r.source !== 'metadata') {
      findings.push(`content change after the approval of v${version}: ${r.change.slice(0, 60)}`)
    }
  }
  return findings
}

if (import.meta.main) {
  let failed = false
  for (const path of process.argv.slice(2)) {
    for (const finding of checkPrdApproval(readFileSync(path, 'utf8'))) {
      console.log(`${path}: ${finding}`)
      failed = true
    }
  }
  process.exit(failed ? 1 : 0)
}
