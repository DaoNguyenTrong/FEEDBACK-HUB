import { getCurrentOrganization } from '@/api/generated/current-organization/current-organization'
import type { OrganizationMemberDto, RoleOptionDto } from '@/api/types'
import { toPagedResult } from '@/lib/paged-result'
import { Permissions } from '@/lib/permissions'

export const useOrganizationPeopleStore = defineStore('organization-people', () => {
  const client = getCurrentOrganization()
  const auth = useAuthStore()

  // ── Members (manual lazy list — useLazyList id-field requirement prevents clean nesting) ──

  const _items = ref<OrganizationMemberDto[]>([])
  const _totalCount = ref(0)
  const _pageNumber = ref(0)
  const _loading = ref(false)
  const _loadingMore = ref(false)

  const _hasMore = computed(() => _items.value.length < _totalCount.value)

  async function _loadPage() {
    if (_loading.value || _loadingMore.value) return
    if (_pageNumber.value > 0 && !_hasMore.value) return

    const isFirst = _pageNumber.value === 0
    if (isFirst) _loading.value = true
    else _loadingMore.value = true

    try {
      const raw = await client.currentOrganizationListMembers({
        pageNumber: _pageNumber.value + 1,
        pageSize: 50,
      })
      const paged = toPagedResult(raw)

      if (isFirst) {
        _items.value = paged.items
      } else {
        const existingIds = new Set(_items.value.map((m) => m.accountId))
        _items.value = [
          ..._items.value,
          ...paged.items.filter((m) => !existingIds.has(m.accountId)),
        ]
      }
      _totalCount.value = paged.totalCount
      _pageNumber.value += 1
    } finally {
      _loading.value = false
      _loadingMore.value = false
    }
  }

  async function _reload() {
    _items.value = []
    _totalCount.value = 0
    _pageNumber.value = 0
    await _loadPage()
  }

  // Public members interface
  const members = {
    get items(): OrganizationMemberDto[] {
      return _items.value
    },
    get totalCount(): number {
      return _totalCount.value
    },
    get loading(): boolean {
      return _loading.value
    },
    get hasMore(): boolean {
      return _hasMore.value
    },
    loadMore: _loadPage,
    reload: _reload,
  }

  // ── Role options ──────────────────────────────────────────────────────────

  const roleOptions = ref<RoleOptionDto[]>([])
  const _roleOptionsLoaded = ref(false)

  async function loadRoleOptions() {
    if (_roleOptionsLoaded.value) return
    if (!auth.hasPermission(Permissions.OrganizationMembersManage)) return
    const options = await client.currentOrganizationListRoleOptions()
    roleOptions.value = options
    _roleOptionsLoaded.value = true
  }

  // ── Mutations ─────────────────────────────────────────────────────────────

  async function changeRoles(accountId: string, roleIds: string[]) {
    await client.currentOrganizationChangeMemberRoles(accountId, { roleIds })

    // API returns 204; rebuild the row from local role-options data.
    const member = _items.value.find((m) => m.accountId === accountId)
    if (member) {
      const newRoles = roleOptions.value
        .filter((o) => roleIds.includes(o.id))
        .map((o) => ({ id: o.id, name: o.name, systemKind: o.systemKind ?? null }))
      const updated: OrganizationMemberDto = {
        ...member,
        roleIds,
        roleNames: newRoles.map((r) => r.name),
        roles: newRoles,
      }
      _items.value = _items.value.map((m) => (m.accountId === accountId ? updated : m))
    }
  }

  async function removeMember(accountId: string) {
    await client.currentOrganizationRemoveMember(accountId)
    _items.value = _items.value.filter((m) => m.accountId !== accountId)
    _totalCount.value = Math.max(0, _totalCount.value - 1)
  }

  async function leave() {
    await client.currentOrganizationLeave()
  }

  return {
    members,
    roleOptions,
    loadRoleOptions,
    changeRoles,
    removeMember,
    leave,
  }
})
