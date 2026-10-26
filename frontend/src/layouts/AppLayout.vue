<script setup lang="ts">
import AppFooter from './AppFooter.vue'
import AppRail from './AppRail.vue'
import AppTopBar from './AppTopBar.vue'
import AppearanceSettings from './AppearanceSettings.vue'
import CommandPalette from './CommandPalette.vue'

const notificationsStore = useNotificationsStore()
const route = useRoute()
const layout = useLayoutStore()
const auth = useAuthStore()

// Replaces the routed view instead of covering it, so a forbidden page never mounts or fetches.
const forbidden = computed(() => routeAccess(route.meta, auth) === 'forbidden')

// Pages scroll inside `main`, not `window`, so the router's `scrollBehavior` never reaches it.
// Keyed on `path` only: a query change (filters, paging) keeps the scroll position.
const mainEl = ref<HTMLElement | null>(null)
watch(
  () => route.path,
  () => mainEl.value?.scrollTo({ top: 0 }),
)

onMounted(() => {
  notificationsStore.startPolling()
  notificationsStore.connectRealtime()
})
onUnmounted(() => {
  notificationsStore.stopPolling()
  notificationsStore.disconnectRealtime()
})
</script>

<template>
  <!--
    `relative` on shell and `main` is load-bearing: without it an `sr-only` label escapes the `overflow` clip.
    `h-dvh`, not `h-screen`, keeps the footer above the address bar; `isolate` keeps the backdrop over the bg.
  -->
  <div data-testid="app-shell" class="relative isolate flex h-dvh overflow-hidden bg-page text-ink">
    <AmbientBackdrop strength="soft" />
    <AppRail />
    <div class="flex min-h-0 min-w-0 flex-1 flex-col">
      <AppTopBar />
      <main
        ref="mainEl"
        class="relative flex-1 overflow-y-auto p-4 [scrollbar-gutter:stable] desktop:p-6"
      >
        <AccessProblemState v-if="forbidden" kind="forbidden" />
        <RouterView v-else />
      </main>
      <AppFooter />
    </div>

    <!--
      Overlays opened from the top bar (the avatar menu, "Go to…", Ctrl K); mounted once for the whole
      shell. The profile panel lives here, not in the avatar menu, so its unsaved form outlives the menu.
    -->
    <AppearanceSettings />
    <CommandPalette />
    <ProfilePanel v-model:visible="layout.profileOpen" />
  </div>
</template>
