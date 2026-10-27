/**
 * formatDateTime formats an ISO 8601 string as "HH:mm, dd/MM/yyyy" in the browser's
 * local time zone. Uses Intl.DateTimeFormat for locale-aware hour/date formatting.
 */
export function formatDateTime(iso: string, _locale: string): string {
  const date = new Date(iso)
  const timeStr = new Intl.DateTimeFormat('vi', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date)
  const dateStr = new Intl.DateTimeFormat('vi', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
  return `${timeStr}, ${dateStr}`
}
