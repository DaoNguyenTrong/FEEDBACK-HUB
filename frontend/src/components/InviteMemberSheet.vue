<script setup lang="ts">
import { ApiError } from '@/api/types'
import { errorMessage } from '@/lib/error-message'
import { showSuccessMessage } from '@/lib/feedback'
import { roleDisplayName } from '@/lib/role-name'
import { useOrganizationPeopleStore } from '@/stores/organization-people'

const props = defineProps<{
  show: boolean
}>()

const emit = defineEmits<{
  'update:show': [value: boolean]
}>()

const { t, te } = useI18n()
const auth = useAuthStore()
const people = useOrganizationPeopleStore()

const email = ref('')
const selectedRoleId = ref<string | null>(null)
const sending = ref(false)
const emailError = ref<string | null>(null)
const formError = ref<string | null>(null)

function reset() {
  email.value = ''
  selectedRoleId.value = null
  emailError.value = null
  formError.value = null
}

watch(
  () => props.show,
  (val) => {
    if (val) reset()
  },
)

function roleOptionDisabledReason(roleId: string): string | null {
  const option = people.roleOptions.find((o) => o.id === roleId)
  if (!option) return null
  if (option.canGrant) return null
  if (option.systemKind === 'Owner') return t('members.ownerOnlyRoleHint')
  return t('members.insufficientRoleHint')
}

async function handleSend() {
  if (sending.value) return
  if (!selectedRoleId.value) return

  emailError.value = null
  formError.value = null
  sending.value = true
  try {
    await people.invite(email.value, selectedRoleId.value)
    showSuccessMessage(t('invitations.sent', { email: email.value }))
    emit('update:show', false)
  } catch (error) {
    const code = error instanceof ApiError ? error.problem.code : undefined
    if (code === 'InvitationEmailAlreadyMember') {
      emailError.value = t('errorCodes.InvitationEmailAlreadyMember', {
        org: auth.organizationName ?? '',
      })
    } else if (code === 'FieldEmailAddress') {
      emailError.value = t('errorCodes.FieldEmailAddress')
    } else if (code === 'InvitationEmailDeliveryFailed') {
      formError.value = t('errorCodes.InvitationEmailDeliveryFailed')
    } else {
      formError.value = errorMessage(error, t, te)
    }
  } finally {
    sending.value = false
  }
}
</script>

<template>
  <AppSheet
    :show="show"
    :title="$t('invitations.inviteMember')"
    @update:show="emit('update:show', $event)"
  >
    <div class="flex flex-col gap-4 p-4">
      <!-- Email -->
      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-ink" for="invite-email">
          {{ $t('invitations.email') }}
        </label>
        <input
          id="invite-email"
          v-model="email"
          type="email"
          class="rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink placeholder:text-ink-muted focus:outline-none focus:ring-2 focus:ring-accent"
          :disabled="sending"
          data-testid="invite-email"
        />
        <p v-if="emailError" class="text-xs text-danger" data-testid="invite-email-error">
          {{ emailError }}
        </p>
        <p class="text-xs text-ink-muted" data-testid="invite-email-hint">
          {{ $t('invitations.emailHint') }}
        </p>
      </div>

      <!-- Role -->
      <div class="flex flex-col gap-2">
        <p class="text-sm font-medium text-ink">{{ $t('invitations.role') }}</p>
        <div
          v-for="option in people.roleOptions"
          :key="option.id"
          :data-testid="`role-option-${option.id}`"
          class="flex flex-col gap-0.5"
        >
          <label class="flex cursor-pointer items-center gap-2 text-sm text-ink">
            <input
              type="radio"
              :value="option.id"
              :checked="selectedRoleId === option.id"
              :disabled="!option.canGrant || sending"
              @change="selectedRoleId = option.id"
            />
            {{ roleDisplayName(option, t) }}
          </label>
          <p v-if="roleOptionDisabledReason(option.id)" class="ml-6 text-xs text-ink-muted">
            {{ roleOptionDisabledReason(option.id) }}
          </p>
        </div>
      </div>

      <!-- Expiry hint -->
      <p class="text-xs text-ink-muted">{{ $t('invitations.expiry') }}</p>

      <!-- Form-level error -->
      <InlineNotice v-if="formError" tone="danger" data-testid="invite-error">
        {{ formError }}
      </InlineNotice>

      <!-- Send / Cancel buttons -->
      <div class="flex gap-2">
        <n-button
          type="primary"
          :loading="sending"
          :disabled="!selectedRoleId || !email || sending"
          data-testid="invite-send"
          @click="handleSend"
        >
          {{ sending ? $t('invitations.sending') : $t('invitations.send') }}
        </n-button>
        <n-button
          :disabled="sending"
          data-testid="invite-cancel"
          @click="emit('update:show', false)"
        >
          {{ $t('common.cancel') }}
        </n-button>
      </div>
    </div>
  </AppSheet>
</template>
