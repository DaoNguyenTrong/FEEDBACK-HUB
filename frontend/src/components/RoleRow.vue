<script setup lang="ts">
import { DotsVertical } from '@vicons/tabler'
import type { RoleDto } from '@/api/types'
import { ApiError } from '@/api/types'
import { errorMessage } from '@/lib/error-message'
import { showErrorMessage, showSuccessMessage } from '@/lib/feedback'
import { permissionName } from '@/lib/permission-label'
import { roleDisplayName } from '@/lib/role-name'
import { useOrganizationPeopleStore } from '@/stores/organization-people'

const props = defineProps<{ role: RoleDto }>()
const emit = defineEmits<{ edit: [role: RoleDto] }>()

const { t, te } = useI18n()
const people = useOrganizationPeopleStore()

const displayName = computed(() => roleDisplayName(props.role, t))
const holderCount = computed(() => Number(props.role.holderCount))
const pendingInvitationCount = computed(() => Number(props.role.pendingInvitationCount ?? 0))

const permissionSummary = computed(() => {
  if (props.role.systemKind === 'Owner') return t('roles.allPermissions')
  if (props.role.permissionCodes.length === 0) return t('roles.noPermissions')
  return props.role.permissionCodes.map((code) => permissionName(code, t)).join(', ')
})

const viewOnlyCustom = computed(() => !props.role.canEdit && !props.role.isSystem)

// A 409 from Delete means someone took the role after the list loaded; the reload that follows
// brings the same reason from the server, this keeps the line visible until it arrives.
const holdersConflict = ref(false)
const blockedByHolders = computed(
  () => holdersConflict.value || props.role.deleteBlockedReason === 'HasHolders',
)

const showDeleteItem = computed(() => props.role.canDelete || blockedByHolders.value)
const menuOptions = computed(() => [
  ...(props.role.canEdit ? [{ label: t('roles.edit'), key: 'edit' }] : []),
  ...(showDeleteItem.value
    ? [
        {
          label: t('roles.delete'),
          key: 'delete',
          disabled: !props.role.canDelete || blockedByHolders.value,
          props: { class: 'text-danger' },
        },
      ]
    : []),
])

const showDeleteConfirm = ref(false)
const deleting = ref(false)

function onSelect(key: string) {
  if (key === 'edit') emit('edit', props.role)
  if (key === 'delete') showDeleteConfirm.value = true
}

async function confirmDelete() {
  if (deleting.value) return
  deleting.value = true
  try {
    await people.deleteRole(props.role.id)
    showSuccessMessage(t('roles.deleted', { name: displayName.value }))
  } catch (error) {
    const code = error instanceof ApiError ? error.problem.code : undefined
    showDeleteConfirm.value = false
    if (code === 'RoleHasHolders') {
      holdersConflict.value = true
      await people.loadRoles().catch(() => {})
    } else {
      showErrorMessage(errorMessage(error, t, te))
    }
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="border-b border-line px-4 py-3 last:border-b-0" :data-testid="`role-row-${role.id}`">
    <div class="flex items-start gap-3">
      <div class="min-w-0 flex-1">
        <p class="truncate text-sm font-medium text-ink" :title="displayName">
          {{ displayName }}
        </p>
        <p class="mt-0.5 text-xs text-ink-muted">{{ permissionSummary }}</p>
        <p
          v-if="viewOnlyCustom"
          class="mt-1 text-xs text-ink-muted"
          data-testid="role-view-only-reason"
        >
          {{ $t('roles.viewOnlyMissingPermissions') }}
        </p>
        <p
          v-if="blockedByHolders"
          class="mt-1 text-xs text-ink-muted"
          data-testid="role-delete-blocked"
        >
          {{ $t('roles.deleteBlockedHasHolders', { n: holderCount }, holderCount) }}
        </p>
      </div>

      <p class="shrink-0 text-xs text-ink-muted">
        {{ $t('roles.holders', { n: holderCount }, holderCount) }}
      </p>

      <div class="flex shrink-0 items-center gap-2">
        <span v-if="!role.canEdit" class="text-xs text-ink-muted">{{ $t('roles.viewOnly') }}</span>
        <n-dropdown
          v-if="menuOptions.length > 0"
          trigger="click"
          :options="menuOptions"
          @select="onSelect"
        >
          <n-button
            size="small"
            secondary
            circle
            class="max-desktop:min-h-11 max-desktop:min-w-11"
            data-testid="role-menu"
            :aria-label="$t('roles.actionsFor', { name: displayName })"
            aria-haspopup="menu"
          >
            <template #icon>
              <n-icon><DotsVertical /></n-icon>
            </template>
          </n-button>
        </n-dropdown>
      </div>
    </div>

    <div v-if="showDeleteConfirm" class="mt-3">
      <InlineConfirm
        :message="$t('roles.deleteConfirmMessage', { name: displayName })"
        :detail="
          pendingInvitationCount > 0
            ? $t('roles.deleteConfirmDetail', { n: pendingInvitationCount }, pendingInvitationCount)
            : undefined
        "
        :confirm-label="$t('roles.delete')"
        :busy="deleting"
        @confirm="confirmDelete"
        @cancel="showDeleteConfirm = false"
      />
    </div>
  </div>
</template>
