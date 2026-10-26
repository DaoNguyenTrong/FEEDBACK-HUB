import { getAuth } from '@/api/generated/auth/auth'
import { reloadTo, reloadToLogin } from '@/lib/session-exit'

/**
 * Full reload to home, so no store keeps the old tenant's data; the auth store is skipped on purpose.
 * Errors propagate without a reload, except 401: /api/auth/ skips refresh-and-retry, so it exits to login.
 */
export function useOrganizationSwitch() {
  const client = getAuth()
  const router = useRouter()
  const auth = useAuthStore()

  async function switchOrganization(organizationId: string | null) {
    try {
      await client.authSwitchOrganization({ organizationId })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        auth.clearAuth()
        reloadToLogin()
        return
      }
      throw error
    }
    reloadTo(router.resolve(useHomeRoute()).fullPath)
  }

  return { switchOrganization }
}
