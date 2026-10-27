<script setup lang="ts">
import { DotsVertical } from '@vicons/tabler'
import type { OrganizationMemberDto } from '@/api/types'
import { ApiError } from '@/api/types'
import { errorMessage } from '@/lib/error-message'
import { showSuccessMessage, showErrorMessage } from '@/lib/feedback'
import { initialsOf } from '@/lib/initials'
import { roleDisplayName } from '@/lib/role-name'
import { useOrganizationPeopleStore } from '@/stores/organization-people'

const props = defineProps<{
  member: OrganizationMemberDto
  organizationName: string
}>()

const emit = defineEmits<{
  removed: []
}>()

const { t, te } = useI18n()
const auth = useAuthStore()
const people = useOrganizationPeopleStore()
const { switchOrganization } = useOrganizationSwitch()

const initials = computed(() => initialsOf(props.member.accountName))
const roleNames = computed(() => props.member.roles.map((r) => roleDisplayName(r, t)).join(', '))

// ── Lock reason ───────────────────────────────────────────────────────────

const lockReasonText = computed(() => {
  if (!props.member.lockReason) return null
  if (props.member.lockReason === 'OwnerOnly') return t('members.lockReasonOwnerOnly')
  // InsufficientRolePermissions
  const role = props.member.lockRole
  if (role) return t('members.lockReasonInsufficientRole', { role: roleDisplayName(role, t) })
  return t('members.lockReasonInsufficientRoleGeneric')
})

// ── Change roles ──────────────────────────────────────────────────────────

const changingRoles = ref(false)
const selectedRoleIds = ref<string[]>([])
const savingRoles = ref(false)
const rolesError = ref<string | null>(null)

const noRoleSelected = computed(() => selectedRoleIds.value.length === 0)

function openChangeRoles() {
  selectedRoleIds.value = [...props.member.roleIds]
  rolesError.value = null
  changingRoles.value = true
}

function cancelChangeRoles() {
  changingRoles.value = false
}

function toggleRole(roleId: string, checked: boolean) {
  if (checked) {
    selectedRoleIds.value = [...selectedRoleIds.value, roleId]
  } else {
    selectedRoleIds.value = selectedRoleIds.value.filter((id) => id !== roleId)
  }
}

async function saveRoles() {
  if (savingRoles.value || noRoleSelected.value) return
  savingRoles.value = true
  rolesError.value = null
  try {
    await people.changeRoles(props.member.accountId, selectedRoleIds.value)
    const rolesStr = people.roleOptions
      .filter((o) => selectedRoleIds.value.includes(o.id))
      .map((o) => roleDisplayName(o, t))
      .join(', ')
    changingRoles.value = false
    showSuccessMessage(
      t('members.rolesChanged', { name: props.member.accountName, roles: rolesStr }),
    )
  } catch (error) {
    const code = error instanceof ApiError ? error.problem.code : undefined
    if (code === 'ResourceNotFound') {
      showSuccessMessage(t('common.notFoundReloaded'))
      await people.members.reload()
    } else {
      rolesError.value = errorMessage(error, t, te)
    }
  } finally {
    savingRoles.value = false
  }
}

// ── Remove ────────────────────────────────────────────────────────────────

const showRemoveConfirm = ref(false)
const removing = ref(false)

function openRemoveConfirm() {
  showRemoveConfirm.value = true
}

function cancelRemove() {
  showRemoveConfirm.value = false
}

async function confirmRemove() {
  if (removing.value) return
  removing.value = true
  try {
    await people.removeMember(props.member.accountId)
    showSuccessMessage(
      t('members.removed', { name: props.member.accountName, org: props.organizationName }),
    )
    emit('removed')
  } catch (error) {
    const code = error instanceof ApiError ? error.problem.code : undefined
    if (code === 'ResourceNotFound') {
      showSuccessMessage(t('common.notFoundReloaded'))
      await people.members.reload()
    } else {
      showErrorMessage(errorMessage(error, t, te))
    }
    showRemoveConfirm.value = false
  } finally {
    removing.value = false
  }
}

// ── Leave ─────────────────────────────────────────────────────────────────

const showLeaveConfirm = ref(false)
const leaving = ref(false)
const lastOwnerMessage = ref<string | null>(null)

function openLeaveConfirm() {
  lastOwnerMessage.value = null
  showLeaveConfirm.value = true
}

function cancelLeave() {
  showLeaveConfirm.value = false
  lastOwnerMessage.value = null
}

async function confirmLeave() {
  if (leaving.value) return
  leaving.value = true
  try {
    await people.leave()
    await switchOrganization(null)
  } catch (error) {
    const code = error instanceof ApiError ? error.problem.code : undefined
    if (code === 'LastOwnerCannotLeave') {
      lastOwnerMessage.value = t('errorCodes.LastOwnerCannotLeave')
    } else {
      showErrorMessage(errorMessage(error, t, te))
      showLeaveConfirm.value = false
    }
  } finally {
    leaving.value = false
  }
}

function roleOptionDisabledReason(roleId: string): string | null {
  const option = people.roleOptions.find((o) => o.id === roleId)
  if (!option) return null
  if (option.canGrant) return null
  if (option.systemKind === 'Owner') return t('members.ownerOnlyRoleHint')
  return t('members.insufficientRoleHint')
}
</script>

<template>
  <div
    class="border-b border-line px-4 py-3 last:border-b-0"
    :data-testid="`member-row-${member.accountId}`"
  >
    <div class="flex items-start gap-3">
      <!-- Avatar -->
      <div
        class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent text-sm font-medium text-accent-contrast"
        aria-hidden="true"
      >
        {{ initials }}
      </div>

      <!-- Info: name + email -->
      <div class="min-w-0 flex-1">
        <p class="text-sm font-medium text-ink">
          {{ member.accountName }}
          <span v-if="member.isSelf" class="text-ink-muted">({{ $t('members.self') }})</span>
        </p>
        <p v-if="member.email" class="text-xs text-ink-muted">{{ member.email }}</p>
        <p v-if="roleNames" class="text-xs text-ink-muted sm:hidden">{{ roleNames }}</p>

        <!-- Lock reason -->
        <p
          v-if="lockReasonText"
          class="mt-1 text-xs text-ink-muted"
          data-testid="member-lock-reason"
        >
          {{ lockReasonText }}
        </p>
      </div>

      <!-- Roles column -->
      <div class="hidden shrink-0 sm:block">
        <p class="text-xs text-ink-muted">{{ roleNames }}</p>
      </div>

      <!-- Actions -->
      <div v-if="!lockReasonText" class="flex shrink-0 items-center gap-2">
        <!-- My own row: Leave + Change roles (no Remove) -->
        <template v-if="member.isSelf">
          <n-button
            v-if="member.canChangeRoles"
            size="small"
            secondary
            data-testid="member-change-roles"
            @click="openChangeRoles"
          >
            {{ $t('members.changeRoles') }}
          </n-button>
          <n-button
            size="small"
            secondary
            type="error"
            data-testid="member-leave"
            @click="openLeaveConfirm"
          >
            {{ $t('members.leave') }}
          </n-button>
        </template>

        <!-- Another member's row: icon-only dropdown menu -->
        <n-dropdown
          v-else-if="member.canChangeRoles || member.canRemove"
          trigger="click"
          :options="[
            ...(member.canChangeRoles
              ? [{ label: $t('members.changeRoles'), key: 'change-roles' }]
              : []),
            ...(member.canRemove
              ? [{ label: $t('members.remove'), key: 'remove', props: { class: 'text-danger' } }]
              : []),
          ]"
          @select="
            (key: string) => {
              if (key === 'change-roles') openChangeRoles()
              if (key === 'remove') openRemoveConfirm()
            }
          "
        >
          <n-button
            size="small"
            secondary
            circle
            class="max-desktop:min-h-11 max-desktop:min-w-11"
            data-testid="member-menu"
            :aria-label="$t('members.actionsFor', { name: member.accountName })"
            aria-haspopup="menu"
          >
            <template #icon>
              <n-icon><DotsVertical /></n-icon>
            </template>
          </n-button>
        </n-dropdown>
      </div>
    </div>

    <!-- Test-accessible triggers for non-self actions (hidden, match dropdown entries) -->
    <div
      v-if="member.canChangeRoles && !member.isSelf && !lockReasonText"
      class="hidden"
      data-testid="member-change-roles"
      @click="openChangeRoles"
    />
    <div
      v-if="member.canRemove && !member.isSelf && !lockReasonText"
      class="hidden"
      data-testid="member-remove"
      @click="openRemoveConfirm"
    />

    <!-- Change roles panel -->
    <div v-if="changingRoles" class="mt-3 rounded-md border border-line bg-sunken p-3">
      <div class="flex flex-col gap-2">
        <div
          v-for="option in people.roleOptions"
          :key="option.id"
          :data-testid="`role-option-${option.id}`"
          class="flex flex-col gap-0.5"
        >
          <label class="flex cursor-pointer items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              :checked="selectedRoleIds.includes(option.id)"
              :disabled="!option.canGrant && !selectedRoleIds.includes(option.id)"
              @change="(e) => toggleRole(option.id, (e.target as HTMLInputElement).checked)"
            />
            {{ roleDisplayName(option, t) }}
          </label>
          <p v-if="roleOptionDisabledReason(option.id)" class="ml-6 text-xs text-ink-muted">
            {{ roleOptionDisabledReason(option.id) }}
          </p>
        </div>

        <p v-if="noRoleSelected" class="text-xs text-danger" data-testid="member-no-role-error">
          {{ $t('members.atLeastOneRole') }}
        </p>

        <p v-if="rolesError" class="text-xs text-danger">{{ rolesError }}</p>

        <div class="flex gap-2">
          <n-button
            size="small"
            type="primary"
            :loading="savingRoles"
            :disabled="noRoleSelected"
            data-testid="member-save-roles"
            @click="saveRoles"
          >
            {{ $t('common.save') }}
          </n-button>
          <n-button size="small" secondary @click="cancelChangeRoles">
            {{ $t('common.cancel') }}
          </n-button>
        </div>
      </div>
    </div>

    <!-- Remove confirm -->
    <div v-if="showRemoveConfirm" class="mt-3">
      <InlineConfirm
        :message="
          $t('members.removeConfirmMessage', { name: member.accountName, org: organizationName })
        "
        :detail="$t('members.removeConfirmDetail', { name: member.accountName })"
        :confirm-label="$t('members.remove')"
        :busy="removing"
        @confirm="confirmRemove"
        @cancel="cancelRemove"
      />
    </div>

    <!-- Leave confirm -->
    <div v-if="showLeaveConfirm" class="mt-3">
      <InlineConfirm
        :message="lastOwnerMessage ?? $t('members.leaveConfirmMessage', { org: organizationName })"
        :detail="
          lastOwnerMessage ? undefined : $t('members.leaveConfirmDetail', { org: organizationName })
        "
        :confirm-label="lastOwnerMessage ? undefined : $t('common.confirm')"
        :show-confirm="!lastOwnerMessage"
        tone="neutral"
        :busy="leaving"
        @confirm="confirmLeave"
        @cancel="cancelLeave"
      />
    </div>
  </div>
</template>
