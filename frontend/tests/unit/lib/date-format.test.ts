import { describe, expect, it } from 'vitest'
import { formatDateTime } from '@/lib/date-format'

function expectedLocal(iso: string): string {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    })
      .formatToParts(new Date(iso))
      .map((part) => [part.type, part.value]),
  )
  return `${parts.hour}:${parts.minute}, ${parts.day}/${parts.month}/${parts.year}`
}

describe('formatDateTime', () => {
  it('formats an instant as time, then date in the browser time zone', () => {
    const iso = '2026-10-30T07:30:00Z'
    expect(formatDateTime(iso, 'vi')).toBe(expectedLocal(iso))
  })

  it('formats in vi locale with HH:mm, dd/MM/yyyy', () => {
    const iso = '2026-11-01T14:30:00Z'
    expect(formatDateTime(iso, 'vi')).toBe(expectedLocal(iso))
    expect(formatDateTime(iso, 'vi')).toMatch(/^\d{2}:\d{2}, \d{2}\/\d{2}\/\d{4}$/)
  })
})
