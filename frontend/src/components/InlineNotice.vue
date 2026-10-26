<script setup lang="ts">
import { AlertCircle, AlertTriangle, CircleCheck, InfoCircle, X } from '@vicons/tabler'

type Tone = 'info' | 'success' | 'warning' | 'danger'

const props = defineProps<{ tone: Tone; dismissible?: boolean }>()
const emit = defineEmits<{ dismiss: [] }>()

const { t } = useI18n()

const TONE_CLASSES: Record<Tone, string> = {
  info: 'bg-info-soft text-info',
  success: 'bg-success-soft text-success',
  warning: 'bg-warning-soft text-warning',
  danger: 'bg-danger-soft text-danger',
}
const TONE_ICONS = {
  info: InfoCircle,
  success: CircleCheck,
  warning: AlertTriangle,
  danger: AlertCircle,
}
</script>

<template>
  <div
    :role="props.tone === 'danger' ? 'alert' : 'status'"
    class="flex items-start gap-3 rounded-md px-3 py-2.5 text-meta"
    :class="TONE_CLASSES[props.tone]"
  >
    <n-icon :size="18" class="mt-px shrink-0" aria-hidden="true">
      <component :is="TONE_ICONS[props.tone]" />
    </n-icon>
    <div class="min-w-0 flex-1 text-ink">
      <slot />
    </div>
    <div v-if="$slots.action" class="shrink-0">
      <slot name="action" />
    </div>
    <button
      v-if="props.dismissible"
      type="button"
      class="-m-1 flex shrink-0 items-center justify-center rounded-md p-1 hover:bg-ghost-hover max-desktop:min-h-11 max-desktop:min-w-11"
      data-testid="inline-notice-dismiss"
      :aria-label="t('common.close')"
      @click="emit('dismiss')"
    >
      <n-icon :size="16"><X /></n-icon>
    </button>
  </div>
</template>
