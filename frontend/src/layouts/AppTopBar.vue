<script setup lang="ts">
import { Menu2, Search } from '@vicons/tabler'
import UserMenu from './UserMenu.vue'

const { t } = useI18n()
const layout = useLayoutStore()
const { isPhone, isDesktop } = useViewport()

const isMac = computed(() => {
  const nav = navigator as Navigator & { userAgentData?: { platform?: string } }
  return /mac/i.test(nav.userAgentData?.platform ?? nav.platform ?? '')
})

const targetClass = 'max-desktop:min-h-11 max-desktop:min-w-11'
</script>

<template>
  <header class="glass-thin mx-3 mt-3 flex h-14 shrink-0 items-center gap-2 rounded-lg px-3">
    <button
      v-if="isPhone"
      type="button"
      :aria-label="t('nav.openMenu')"
      data-testid="app-header-menu"
      class="flex shrink-0 items-center justify-center rounded-md text-ink hover:bg-ghost-hover"
      :class="targetClass"
      @click="layout.openMobile()"
    >
      <n-icon :size="20"><Menu2 /></n-icon>
    </button>

    <div class="flex min-w-0 flex-1 items-center">
      <slot name="organization" />
    </div>

    <button
      type="button"
      :aria-label="t('layout.palette.trigger')"
      data-testid="app-header-palette"
      class="flex shrink-0 items-center justify-center gap-2 text-ink-muted hover:bg-ghost-hover"
      :class="[
        targetClass,
        isDesktop ? 'h-9 rounded-full border border-line bg-sunken px-3 text-body' : 'rounded-md',
      ]"
      @click="layout.openPalette()"
    >
      <n-icon :size="18"><Search /></n-icon>
      <template v-if="isDesktop">
        <span>{{ t('layout.palette.trigger') }}</span>
        <kbd class="rounded-sm border border-line px-1.5 text-caption">{{
          isMac ? '⌘ K' : 'Ctrl K'
        }}</kbd>
      </template>
    </button>

    <NotificationBell />
    <UserMenu />
  </header>
</template>
