<script setup lang="ts">
import { Search } from '@vicons/tabler'
import AppRailNav from './AppRailNav.vue'

const { t } = useI18n()
const route = useRoute()
const layout = useLayoutStore()
const { isPhone, isDesktop } = useViewport()
const homeRoute = useHomeRoute()

// The slide-in menu closes on any navigation, and cannot stay open once the screen outgrows a phone.
watch(
  () => route.path,
  () => layout.closeMobile(),
)
watch(isPhone, (phone) => {
  if (!phone) layout.closeMobile()
})

function openPalette() {
  layout.closeMobile()
  layout.openPalette()
}
</script>

<template>
  <aside
    v-if="!isPhone"
    data-testid="app-sidebar"
    class="glass-thin my-3 ml-3 flex shrink-0 flex-col gap-2 rounded-lg p-2"
    :class="isDesktop ? 'w-64' : 'w-16 items-center'"
  >
    <RouterLink
      :to="homeRoute"
      data-testid="app-sidebar-home"
      :aria-label="isDesktop ? undefined : t('app.name')"
      :title="isDesktop ? undefined : t('app.name')"
      class="flex min-h-11 items-center gap-3 rounded-md px-2 text-ink"
    >
      <img src="/weatherplus-mark.webp" alt="" class="size-8 shrink-0 object-cover" />
      <span v-if="isDesktop" class="truncate text-section">{{ t('app.name') }}</span>
    </RouterLink>
    <AppRailNav :variant="isDesktop ? 'wide' : 'icon'" class="min-h-0 flex-1 overflow-y-auto" />
  </aside>

  <n-drawer
    v-else
    v-model:show="layout.mobileOpen"
    placement="left"
    :width="288"
    :aria-label="t('app.name')"
    :style="{ backgroundColor: 'var(--color-panel-solid)' }"
  >
    <n-drawer-content :native-scrollbar="false">
      <div data-testid="app-sidebar-mobile" class="flex flex-col gap-3">
        <button
          type="button"
          data-testid="app-sidebar-mobile-palette"
          class="flex min-h-11 w-full items-center gap-2 rounded-md border border-line bg-sunken px-3 text-body text-ink-muted"
          @click="openPalette"
        >
          <n-icon :size="18"><Search /></n-icon>
          <span>{{ t('layout.palette.trigger') }}</span>
        </button>
        <AppRailNav variant="wide" @navigate="layout.closeMobile()" />
      </div>
    </n-drawer-content>
  </n-drawer>
</template>
