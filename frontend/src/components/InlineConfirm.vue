<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    message: string
    detail?: string
    confirmLabel?: string
    tone?: 'danger' | 'neutral'
    busy?: boolean
    showConfirm?: boolean
  }>(),
  { tone: 'danger', showConfirm: true },
)
const emit = defineEmits<{ confirm: []; cancel: [] }>()

const { t } = useI18n()
const size = useControlSize()
const root = ref<HTMLElement | null>(null)

onMounted(() => root.value?.focus())

function onEscape() {
  if (!props.busy) emit('cancel')
}
</script>

<template>
  <div
    ref="root"
    role="group"
    tabindex="-1"
    :aria-label="props.message"
    class="rounded-md border border-line bg-sunken px-3 py-2.5 outline-none focus-visible:ring-2 focus-visible:ring-focus"
    @keydown.esc="onEscape"
  >
    <p class="text-row text-ink">{{ props.message }}</p>
    <p v-if="props.detail" class="mt-0.5 text-meta text-ink-muted">{{ props.detail }}</p>
    <div class="mt-2.5 flex flex-wrap justify-end gap-2">
      <n-button
        :size="size"
        secondary
        :disabled="props.busy"
        data-testid="inline-confirm-cancel"
        @click="emit('cancel')"
      >
        {{ t('common.cancel') }}
      </n-button>
      <n-button
        v-if="props.showConfirm"
        :size="size"
        :type="props.tone === 'danger' ? 'error' : 'primary'"
        :loading="props.busy"
        :disabled="props.busy"
        data-testid="inline-confirm-confirm"
        @click="emit('confirm')"
      >
        {{ props.confirmLabel ?? t('common.confirm') }}
      </n-button>
    </div>
  </div>
</template>
