import { getCurrentOrganization } from '@/api/generated/current-organization/current-organization'
import { getPermissions } from '@/api/generated/permissions/permissions'
import type {
  InvitationDto,
  OrganizationMemberDto,
  PermissionDto,
  RoleDto,
  RoleOptionDto,
} from '@/api/types'
import { toPagedResult } from '@/lib/paged-result'
import { Permissions } from '@/lib/permissions'

export const useOrganizationPeopleStore = defineStore('organization-people', () => {
  const client = getCurrentOrganization()
  const permissionsClient = getPermissions()
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
    get loaded(): boolean {
      return _pageNumber.value > 0
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

  // ── Invitations (lazy list) ───────────────────────────────────────────────

  const _invItems = ref<InvitationDto[]>([])
  const _invTotalCount = ref(0)
  const _invPageNumber = ref(0)
  const _invLoading = ref(false)
  const _invLoadingMore = ref(false)

  const _invHasMore = computed(() => _invItems.value.length < _invTotalCount.value)

  async function _loadInvPage() {
    if (_invLoading.value || _invLoadingMore.value) return
    if (_invPageNumber.value > 0 && !_invHasMore.value) return

    const isFirst = _invPageNumber.value === 0
    if (isFirst) _invLoading.value = true
    else _invLoadingMore.value = true

    try {
      const raw = await client.currentOrganizationListInvitations({
        pageNumber: _invPageNumber.value + 1,
        pageSize: 50,
      })
      const paged = toPagedResult(raw)

      if (isFirst) {
        _invItems.value = paged.items
      } else {
        const existingIds = new Set(_invItems.value.map((i) => i.id))
        _invItems.value = [..._invItems.value, ...paged.items.filter((i) => !existingIds.has(i.id))]
      }
      _invTotalCount.value = paged.totalCount
      _invPageNumber.value += 1
    } finally {
      _invLoading.value = false
      _invLoadingMore.value = false
    }
  }

  async function _reloadInv() {
    _invItems.value = []
    _invTotalCount.value = 0
    _invPageNumber.value = 0
    await _loadInvPage()
  }

  const invitations = {
    get items(): InvitationDto[] {
      return _invItems.value
    },
    get totalCount(): number {
      return _invTotalCount.value
    },
    get loading(): boolean {
      return _invLoading.value
    },
    get hasMore(): boolean {
      return _invHasMore.value
    },
    get loaded(): boolean {
      return _invPageNumber.value > 0
    },
    loadMore: _loadInvPage,
    reload: _reloadInv,
  }

  // ── Invitation mutations ─────────────────────────────────────────────────

  const _pendingInvite = ref(false)

  async function invite(email: string, roleId: string) {
    if (_pendingInvite.value) return
    _pendingInvite.value = true
    try {
      const fresh = await client.currentOrganizationSendInvitation({ email, roleId })
      // Remove any existing row with same email, then prepend the new one
      _invItems.value = [fresh, ..._invItems.value.filter((i) => i.email !== email)]
    } finally {
      _pendingInvite.value = false
    }
  }

  const _pendingResend = ref<Set<string>>(new Set())

  async function resend(id: string) {
    if (_pendingResend.value.has(id)) return
    _pendingResend.value = new Set([..._pendingResend.value, id])
    try {
      const updated = await client.currentOrganizationResendInvitation(id)
      _invItems.value = _invItems.value.map((i) => (i.id === id ? updated : i))
    } finally {
      const next = new Set(_pendingResend.value)
      next.delete(id)
      _pendingResend.value = next
    }
  }

  function isResending(id: string): boolean {
    return _pendingResend.value.has(id)
  }

  const _pendingRevoke = ref<Set<string>>(new Set())

  async function revokeOrDelete(id: string) {
    if (_pendingRevoke.value.has(id)) return
    _pendingRevoke.value = new Set([..._pendingRevoke.value, id])
    try {
      await client.currentOrganizationRevokeOrDeleteInvitation(id)
      _invItems.value = _invItems.value.filter((i) => i.id !== id)
      _invTotalCount.value = Math.max(0, _invTotalCount.value - 1)
    } finally {
      const next = new Set(_pendingRevoke.value)
      next.delete(id)
      _pendingRevoke.value = next
    }
  }

  // ── Roles ─────────────────────────────────────────────────────────────────

  const roles = ref<RoleDto[]>([])
  const permissionCatalog = ref<PermissionDto[]>([])
  const _catalogLoaded = ref(false)

  async function loadRoles() {
    roles.value = await client.currentOrganizationListRoles()
  }

  async function loadPermissionCatalog() {
    if (_catalogLoaded.value) return
    permissionCatalog.value = await permissionsClient.permissionsGetAll()
    _catalogLoaded.value = true
  }

  async function createRole(name: string, permissionCodes: string[]) {
    const created = await client.currentOrganizationCreateRole({ name, permissionCodes })
    roles.value = [...roles.value, created]
    return created
  }

  async function updateRole(roleId: string, name: string, permissionCodes: string[]) {
    const updated = await client.currentOrganizationUpdateRole(roleId, { name, permissionCodes })
    roles.value = roles.value.map((r) => (r.id === roleId ? updated : r))
    return updated
  }

  async function deleteRole(roleId: string) {
    await client.currentOrganizationDeleteRole(roleId)
    roles.value = roles.value.filter((r) => r.id !== roleId)
  }

  return {
    members,
    roleOptions,
    loadRoleOptions,
    changeRoles,
    removeMember,
    leave,
    invitations,
    invite,
    resend,
    isResending,
    revokeOrDelete,
    roles,
    permissionCatalog,
    loadRoles,
    loadPermissionCatalog,
    createRole,
    updateRole,
    deleteRole,
  }
})
