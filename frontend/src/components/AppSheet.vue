<script setup lang="ts">
import { X } from '@vicons/tabler'

const props = defineProps<{ title: string }>()
const show = defineModel<boolean>('show', { required: true })

const { t } = useI18n()
const { isDesktop } = useViewport()

const sheetStyle = computed(() =>
  isDesktop.value
    ? { backgroundColor: 'var(--color-panel-solid)' }
    : {
        backgroundColor: 'var(--color-panel-solid)',
        maxHeight: '90dvh',
        borderRadius: 'var(--radius-xl) var(--radius-xl) 0 0',
      },
)
</script>

<template>
  <n-drawer
    v-model:show="show"
    data-testid="app-sheet"
    :aria-label="props.title"
    :placement="isDesktop ? 'right' : 'bottom'"
    :width="isDesktop ? 440 : '100%'"
    :height="isDesktop ? undefined : 'auto'"
    :show-mask="isDesktop ? 'transparent' : true"
    :style="sheetStyle"
  >
    <n-drawer-content :closable="false" :native-scrollbar="false">
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-section text-ink">{{ props.title }}</h2>
          <button
            type="button"
            class="flex items-center justify-center rounded-md p-2 text-ink-muted hover:bg-ghost-hover max-desktop:min-h-11 max-desktop:min-w-11"
            data-testid="app-sheet-close"
            :aria-label="t('common.close')"
            @click="show = false"
          >
            <n-icon :size="18"><X /></n-icon>
          </button>
        </div>
      </template>
      <slot />
      <template v-if="$slots.footer" #footer>
        <slot name="footer" />
      </template>
    </n-drawer-content>
  </n-drawer>
</template>
