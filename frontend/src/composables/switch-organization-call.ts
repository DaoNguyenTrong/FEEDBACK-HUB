import axios from 'axios'
import { getAuth } from '@/api/generated/auth/auth'
import { reloadTo, reloadToLogin } from '@/lib/session-exit'
import { clearSuspendedNotice } from '@/lib/suspended-notice'

function isUnauthorized(error: unknown) {
  if (error instanceof ApiError) return error.status === 401
  return axios.isAxiosError(error) && error.response?.status === 401
}

/**
 * Switches the working organization, then reloads to `homePath`. Shared by `useOrganizationSwitch`
 * and the organization-context store, which has no router. A 401 means sign-out; other errors propagate.
 */
export async function switchOrganizationCall(
  organizationId: string | null,
  homePath: string,
): Promise<'switched' | 'signed-out'> {
  try {
    await getAuth().authSwitchOrganization({ organizationId })
  } catch (error) {
    // The interceptor's clearAuth() usually lets App.vue's session-lapse exit win; this is the fallback.
    if (isUnauthorized(error)) {
      clearSuspendedNotice()
      clearKeepLoginPreference()
      useAuthStore().clearAuth()
      reloadToLogin()
      return 'signed-out'
    }
    throw error
  }
  reloadTo(homePath)
  return 'switched'
}
