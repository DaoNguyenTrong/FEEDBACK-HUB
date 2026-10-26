import type { ProblemDetails } from '@/api/types'
import {
  clearSuspendedNotice,
  readSuspendedNotice,
  writeSuspendedNotice,
} from '@/lib/suspended-notice'

const WELCOME_PATH = '/welcome'

export const useOrganizationContextStore = defineStore('organization-context', () => {
  const auth = useAuthStore()
  const organizationsStore = useOrganizationsStore()

  // The page reloads after an exit, so only the first problem of a page is acted on.
  let handling = false
  let ownExit = false

  function consumeSuspendedNotice(): string | null {
    return readSuspendedNotice()
  }

  function beginOwnExit() {
    ownExit = true
  }

  function cancelOwnExit() {
    ownExit = false
  }

  /** Personal context, then the welcome page; no notice. Also the exit the user started themselves. */
  async function leaveToWelcome(suspendedName?: string | null): Promise<void> {
    if (suspendedName) writeSuspendedNotice(suspendedName)
    try {
      await switchOrganizationCall(null, WELCOME_PATH)
    } catch (error) {
      clearSuspendedNotice()
      ownExit = false
      throw error
    }
  }

  async function exit(suspendedName?: string | null) {
    try {
      await leaveToWelcome(suspendedName)
    } catch {
      handling = false
    }
  }

  async function handleAccessProblem(problem: ProblemDetails): Promise<void> {
    if (ownExit || handling) return
    const workingId = auth.organizationId

    if (problem.code === 'OrganizationSuspended') {
      handling = true
      await exit(problem.organizationName ?? auth.organizationName)
      return
    }

    if (problem.code !== 'OrganizationAccessDenied' || workingId === null) return

    handling = true
    let mine
    try {
      mine = await organizationsStore.fetchMine()
    } catch {
      handling = false
      return
    }
    if (ownExit) {
      handling = false
      return
    }

    const listed = mine.find((organization) => organization.id === workingId)
    if (listed === undefined) {
      await exit()
    } else if (!listed.status) {
      await exit(listed.name)
    } else {
      // Still listed and active: a missing permission, which the page shows by itself.
      handling = false
    }
  }

  async function handleOrganizationLost(previousOrganizationId: string): Promise<void> {
    if (ownExit || handling) return
    handling = true

    let suspendedName: string | null = null
    try {
      const mine = await organizationsStore.fetchMine()
      const listed = mine.find((organization) => organization.id === previousOrganizationId)
      if (listed && !listed.status) suspendedName = listed.name
    } catch {
      // The organization is lost either way; leave without a notice.
    }
    if (ownExit) {
      handling = false
      return
    }

    await exit(suspendedName)
  }

  return {
    consumeSuspendedNotice,
    beginOwnExit,
    cancelOwnExit,
    leaveToWelcome,
    handleAccessProblem,
    handleOrganizationLost,
  }
})
