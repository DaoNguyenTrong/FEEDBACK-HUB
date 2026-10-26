/**
 * Full reload to home, so no store keeps the old tenant's data; the auth store is skipped on purpose.
 * Errors propagate without a reload, except 401: the interceptor already tried refresh-and-retry.
 */
export function useOrganizationSwitch() {
  const router = useRouter()

  function switchOrganization(organizationId: string | null): Promise<'switched' | 'signed-out'> {
    return switchOrganizationCall(organizationId, router.resolve(useHomeRoute()).fullPath)
  }

  return { switchOrganization }
}
