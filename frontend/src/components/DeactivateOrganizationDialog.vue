<script setup lang="ts">
const REASON_MAX = 500

const props = defineProps<{
  organizationName: string
  reasonRequired: boolean
  variant: 'member' | 'system-member' | 'system'
  busy: boolean
  // The organization is already deactivated: the action only retries leaving it.
  deactivated?: boolean
  // Server-side failures: `error` is shown as a notice, `reasonError` under the reason field.
  error?: string | null
  reasonError?: string | null
}>()
const show = defineModel<boolean>('show', { required: true })
const emit = defineEmits<{
  submit: [reason: string | null]
  'update:reasonError': [value: null]
}>()

const { t } = useI18n()
const controlSize = useControlSize()
const { isDesktop } = useViewport()

const reason = ref('')
const missingReason = ref(false)

const BULLET_KEYS = {
  member: 'member',
  'system-member': 'systemMember',
  system: 'system',
} as const

const bulletKey = computed(() => BULLET_KEYS[props.variant])
const reasonProblem = computed(() =>
  missingReason.value ? t('errorCodes.DeactivationReasonRequired') : (props.reasonError ?? null),
)

watch(show, (open) => {
  if (open) {
    reason.value = ''
    missingReason.value = false
  }
})

const reasonInputProps: Record<string, string> = {
  id: 'deactivate-org-reason',
  'data-testid': 'deactivate-org-reason',
}

function onReasonInput(value: string) {
  reason.value = value.slice(0, REASON_MAX)
  missingReason.value = false
  if (props.reasonError) emit('update:reasonError', null)
}

function submit() {
  if (props.busy) return
  if (props.deactivated) {
    emit('submit', null)
    return
  }
  const trimmed = reason.value.trim()
  if (trimmed === '' && props.reasonRequired) {
    missingReason.value = true
    return
  }
  emit('submit', trimmed === '' ? null : trimmed)
}
</script>

<template>
  <n-modal
    v-model:show="show"
    preset="dialog"
    :show-icon="false"
    :title="t('deactivateOrganization.title', { name: props.organizationName })"
    :closable="!props.busy"
    :close-on-esc="!props.busy"
    :mask-closable="!props.busy"
    class="max-w-[28rem]"
    data-testid="deactivate-org-dialog"
  >
    <div class="flex flex-col gap-3">
      <p class="text-body text-ink">{{ t('deactivateOrganization.intro') }}</p>
      <ul class="list-disc space-y-1 pl-5 text-body text-ink" data-testid="deactivate-org-bullets">
        <li>{{ t(`deactivateOrganization.${bulletKey}.widget`) }}</li>
        <li>{{ t(`deactivateOrganization.${bulletKey}.access`) }}</li>
        <li>{{ t(`deactivateOrganization.${bulletKey}.data`) }}</li>
      </ul>

      <InlineNotice v-if="props.error" tone="danger" data-testid="deactivate-org-error">
        {{ props.error }}
      </InlineNotice>

      <div class="flex flex-col gap-1.5">
        <label for="deactivate-org-reason" class="text-sm font-medium text-ink">
          {{ t('deactivateOrganization.reason')
          }}<template v-if="!props.reasonRequired">
            {{ ' ' }}{{ t('deactivateOrganization.optional') }}</template
          >
        </label>
        <n-input
          :value="reason"
          type="textarea"
          :rows="3"
          :maxlength="REASON_MAX"
          :size="controlSize"
          :status="reasonProblem ? 'error' : undefined"
          :input-props="reasonInputProps"
          @update:value="onReasonInput"
        />
        <div class="flex items-start justify-between gap-3">
          <p
            v-if="reasonProblem"
            class="text-xs text-danger"
            role="alert"
            data-testid="deactivate-org-reason-error"
          >
            {{ reasonProblem }}
          </p>
          <span
            class="ml-auto text-xs text-ink-muted"
            data-testid="deactivate-org-count"
            aria-live="off"
          >
            {{ t('deactivateOrganization.count', { n: reason.length }) }}
          </span>
        </div>
      </div>
    </div>

    <template #action>
      <div
        class="flex w-full gap-2"
        :class="isDesktop ? 'justify-end' : 'flex-col-reverse [&>*]:w-full'"
      >
        <n-button
          :size="controlSize"
          :disabled="props.busy"
          data-testid="deactivate-org-cancel"
          @click="show = false"
        >
          {{ t('common.cancel') }}
        </n-button>
        <n-button
          type="error"
          :size="controlSize"
          :loading="props.busy"
          data-testid="deactivate-org-submit"
          @click="submit"
        >
          {{
            props.deactivated
              ? t('organizations.backToWelcome')
              : t('deactivateOrganization.submit')
          }}
        </n-button>
      </div>
    </template>
  </n-modal>
</template>
