import { getAuth } from '@/api/generated/auth/auth'
import axios from 'axios'
import { reloadTo, reloadToLogin } from '@/lib/session-exit'

function isUnauthorized(error: unknown) {
  if (error instanceof ApiError) return error.status === 401
  return axios.isAxiosError(error) && error.response?.status === 401
}

/**
 * Full reload to home, so no store keeps the old tenant's data; the auth store is skipped on purpose.
 * Errors propagate without a reload, except 401: the shared interceptor already tried refresh-and-retry,
 * so a 401 that reaches here means the session is over and it exits to login.
 */
export function useOrganizationSwitch() {
  const client = getAuth()
  const router = useRouter()
  const auth = useAuthStore()

  async function switchOrganization(
    organizationId: string | null,
  ): Promise<'switched' | 'signed-out'> {
    try {
      await client.authSwitchOrganization({ organizationId })
    } catch (error) {
      // A failed refresh rejects with a bodyless AxiosError (no ProblemDetails to wrap), not an ApiError.
      if (isUnauthorized(error)) {
        clearKeepLoginPreference()
        auth.clearAuth()
        reloadToLogin()
        return 'signed-out'
      }
      throw error
    }
    reloadTo(router.resolve(useHomeRoute()).fullPath)
    return 'switched'
  }

  return { switchOrganization }
}
