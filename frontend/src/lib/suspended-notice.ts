const SUSPENDED_NOTICE_KEY = 'organization-suspended-notice'

// Survives the full reload that follows the exit; sessionStorage may be unavailable (blocked storage).
export function writeSuspendedNotice(organizationName: string) {
  try {
    window.sessionStorage.setItem(SUSPENDED_NOTICE_KEY, organizationName)
  } catch {
    // The notice is a courtesy; the exit goes ahead without it.
  }
}

export function clearSuspendedNotice() {
  try {
    window.sessionStorage.removeItem(SUSPENDED_NOTICE_KEY)
  } catch {
    // Nothing stored if storage is blocked.
  }
}

export function readSuspendedNotice(): string | null {
  try {
    const name = window.sessionStorage.getItem(SUSPENDED_NOTICE_KEY)
    window.sessionStorage.removeItem(SUSPENDED_NOTICE_KEY)
    return name
  } catch {
    return null
  }
}
