<script setup lang="ts">
import { Mail } from '@vicons/tabler'
import type { InvitationDto } from '@/api/types'
import { ApiError } from '@/api/types'
import { formatDateTime } from '@/lib/date-format'
import { errorMessage } from '@/lib/error-message'
import { showSuccessMessage, showErrorMessage } from '@/lib/feedback'
import { roleDisplayName } from '@/lib/role-name'
import { useOrganizationPeopleStore } from '@/stores/organization-people'

const props = defineProps<{
  invitation: InvitationDto
}>()

const emit = defineEmits<{
  removed: []
}>()

const { t, locale, te } = useI18n()
const people = useOrganizationPeopleStore()

const roleLabel = computed(() => roleDisplayName(props.invitation.role, t))

const timeLabel = computed(() => {
  const time = formatDateTime(props.invitation.expiresAt, locale.value)
  if (props.invitation.status === 'Expired') {
    return t('invitations.expired', { time })
  }
  return t('invitations.pending', { time })
})

const lockReasonText = computed(() => {
  if (!props.invitation.lockReason) return null
  if (props.invitation.lockReason === 'OwnerOnly') return t('invitations.lockReasonOwnerOnly')
  const role = props.invitation.role
  return t('invitations.lockReasonInsufficientRole', { role: roleDisplayName(role, t) })
})

const isResending = computed(() => people.isResending(props.invitation.id))

// ── Resend ────────────────────────────────────────────────────────────────

async function handleResend() {
  if (isResending.value) return
  try {
    await people.resend(props.invitation.id)
    const updated =
      people.invitations.items.find(
        (i) => i.email === props.invitation.email && i.id !== props.invitation.id,
      ) ?? people.invitations.items.find((i) => i.email === props.invitation.email)
    if (updated) {
      showSuccessMessage(
        t('invitations.resentToast', { time: formatDateTime(updated.expiresAt, locale.value) }),
      )
    }
  } catch (error) {
    showErrorMessage(errorMessage(error, t, te))
  }
}

// ── Delete (expired — no confirm) ────────────────────────────────────────

const deleting = ref(false)

async function handleDelete() {
  if (deleting.value) return
  deleting.value = true
  try {
    await people.revokeOrDelete(props.invitation.id)
    emit('removed')
  } catch (error) {
    showErrorMessage(errorMessage(error, t, te))
  } finally {
    deleting.value = false
  }
}

// ── Revoke (pending — with confirm) ──────────────────────────────────────

const showRevokeConfirm = ref(false)
const revoking = ref(false)

function openRevokeConfirm() {
  showRevokeConfirm.value = true
}

function cancelRevoke() {
  showRevokeConfirm.value = false
}

async function confirmRevoke() {
  if (revoking.value) return
  revoking.value = true
  try {
    await people.revokeOrDelete(props.invitation.id)
    emit('removed')
  } catch (error) {
    const code = error instanceof ApiError ? error.problem.code : undefined
    if (code === 'ResourceNotFound') {
      showSuccessMessage(t('common.notFoundReloaded'))
      await people.invitations.reload()
    } else {
      showErrorMessage(errorMessage(error, t, te))
    }
    showRevokeConfirm.value = false
  } finally {
    revoking.value = false
  }
}
</script>

<template>
  <div
    class="border-b border-line px-4 py-3 last:border-b-0"
    :data-testid="`invitation-row-${invitation.id}`"
  >
    <div class="flex items-start gap-3">
      <!-- Leading mail icon -->
      <div
        class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-fill text-ink-muted"
        aria-hidden="true"
      >
        <n-icon><Mail /></n-icon>
      </div>

      <!-- Info -->
      <div class="min-w-0 flex-1">
        <p class="text-sm font-medium text-ink">{{ invitation.email }}</p>
        <p class="text-xs text-ink-muted">{{ roleLabel }} · {{ timeLabel }}</p>

        <!-- Expired label -->
        <StatusLabel
          v-if="invitation.status === 'Expired'"
          tone="warning"
          data-testid="invitation-expired-label"
          class="mt-1"
        >
          {{ $t('invitations.expiredLabel') }}
        </StatusLabel>

        <!-- Lock reason -->
        <p
          v-if="lockReasonText"
          class="mt-1 text-xs text-ink-muted"
          data-testid="invitation-lock-reason"
        >
          {{ lockReasonText }}
        </p>
      </div>

      <!-- Actions -->
      <div v-if="!lockReasonText" class="flex shrink-0 items-center gap-2">
        <!-- Resend (both pending and expired) -->
        <n-button
          v-if="invitation.canResend || invitation.status === 'Expired'"
          size="small"
          secondary
          :disabled="isResending"
          data-testid="invitation-resend"
          @click="handleResend"
        >
          {{ isResending ? $t('invitations.resending') : $t('invitations.resend') }}
        </n-button>

        <!-- Pending: Revoke -->
        <n-button
          v-if="invitation.status === 'Pending' && invitation.canRevokeOrDelete"
          size="small"
          secondary
          type="error"
          data-testid="invitation-revoke"
          @click="openRevokeConfirm"
        >
          {{ $t('invitations.revoke') }}
        </n-button>

        <!-- Expired: Delete (no confirm) -->
        <n-button
          v-if="invitation.status === 'Expired' && invitation.canRevokeOrDelete"
          size="small"
          secondary
          type="error"
          :loading="deleting"
          data-testid="invitation-delete"
          @click="handleDelete"
        >
          {{ $t('invitations.delete') }}
        </n-button>
      </div>
    </div>

    <!-- Revoke confirm -->
    <div v-if="showRevokeConfirm" class="mt-3" data-testid="invitation-revoke-confirm">
      <InlineConfirm
        :message="$t('invitations.revokeConfirmMessage', { email: invitation.email })"
        :detail="$t('invitations.revokeConfirmDetail')"
        :confirm-label="$t('invitations.revoke')"
        :busy="revoking"
        @confirm="confirmRevoke"
        @cancel="cancelRevoke"
      />
    </div>
  </div>
</template>
