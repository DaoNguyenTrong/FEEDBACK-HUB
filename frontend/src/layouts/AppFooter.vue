<script setup lang="ts">
const { t } = useI18n()
const version = __APP_VERSION__
// Polling is owned by App.vue (app-wide); the footer only reflects the shared state.
const { status } = storeToRefs(useHealthStore())

const statusDotClass = computed(() => {
  if (status.value === 'online') return 'bg-success'
  if (status.value === 'offline' || status.value === 'error') return 'bg-danger'
  return 'bg-ink-disabled'
})
</script>

<template>
  <footer
    class="flex items-center justify-between gap-3 border-t border-line px-4 py-1.5 text-caption text-ink-muted"
  >
    <div class="flex items-center gap-3">
      <span>{{ t('app.name') }}</span>
      <span class="hidden sm:inline">{{ t('app.version', { version }) }}</span>
    </div>

    <n-tooltip trigger="hover" placement="top">
      <template #trigger>
        <span
          class="h-2 w-2 shrink-0 rounded-full"
          :class="statusDotClass"
          role="status"
          :aria-label="t(`app.apiStatus.${status}`)"
        />
      </template>
      {{ t(`app.apiStatus.${status}`) }}
    </n-tooltip>
  </footer>
</template>
