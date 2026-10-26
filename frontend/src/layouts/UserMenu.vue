<script setup lang="ts">
import { Check, ChevronDown, Help, Language, Logout, Palette, User } from '@vicons/tabler'
import { resolveAssetUrl } from '@/lib/asset-url'
import { initialsOf } from '@/lib/initials'
import type { SupportedLocale } from '@/plugins/i18n'

const { t } = useI18n()
const auth = useAuthStore()
const layout = useLayoutStore()
const localeStore = useLocaleStore()
const { preference } = useThemePreference()
const confirmLogout = useConfirmLogout()

const open = ref(false)
const triggerEl = ref<HTMLElement | null>(null)
const menuEl = ref<HTMLElement | null>(null)

const displayName = computed(() => auth.account?.name || auth.account?.username || t('common.user'))
const initials = computed(() => initialsOf(displayName.value))
const avatarUrl = computed(() => resolveAssetUrl(auth.account?.avatarUrl))

// Served by nginx next to the app (frontend/user-guide); Vietnamese is the guide's root locale.
const userGuideHref = computed(() =>
  localeStore.currentLocale === 'en' ? '/guide/en/' : '/guide/',
)

const locales = computed<{ code: SupportedLocale; label: string }[]>(() => [
  { code: 'vi', label: t('layout.userMenu.vietnamese') },
  { code: 'en', label: t('layout.userMenu.english') },
])

function close() {
  open.value = false
}

// Keyboard access (WAI-ARIA menu pattern): the popover traps no focus and is teleported to the end of
// <body>, so focus is moved in on open, roved with the arrow keys, and handed back to the trigger on close.
function menuItems() {
  return Array.from(
    menuEl.value?.querySelectorAll<HTMLElement>('[role="menuitem"], [role="menuitemradio"]') ?? [],
  )
}

watch(open, async (isOpen) => {
  if (isOpen) {
    await nextTick()
    menuItems()[0]?.focus()
  } else if (menuEl.value?.contains(document.activeElement)) {
    // Closed by an item (profile, logout, an organization): focus would otherwise fall to <body>.
    // A click elsewhere has already moved focus out, so it is left alone.
    triggerEl.value?.focus()
  }
})

function closeToTrigger() {
  triggerEl.value?.focus()
  close()
}

function onMenuKeydown(event: KeyboardEvent) {
  const items = menuItems()
  const index = items.indexOf(document.activeElement as HTMLElement)
  let next: HTMLElement | undefined

  switch (event.key) {
    case 'ArrowDown':
      next = items[(index + 1) % items.length]
      break
    case 'ArrowUp':
      next = items[index <= 0 ? items.length - 1 : index - 1]
      break
    case 'Home':
      next = items[0]
      break
    case 'End':
      next = items[items.length - 1]
      break
    case 'Escape':
      // Prevented, so the drawer's window-level Esc leaves its own state alone.
      event.preventDefault()
      closeToTrigger()
      return
    case 'Tab':
      // Not prevented: focus is back on the trigger before the browser acts, so Tab moves on from there.
      closeToTrigger()
      return
    default:
      return
  }

  event.preventDefault()
  next?.focus()
}

function viewProfile() {
  close()
  layout.openProfile()
}

function openAppearance() {
  close()
  layout.openSettings()
}

function logout() {
  close()
  confirmLogout()
}

const itemClass =
  'flex w-full cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-left text-body text-ink transition-colors hover:bg-ghost-hover max-desktop:min-h-11'
</script>

<template>
  <n-popover v-model:show="open" trigger="click" placement="bottom-end" :width="280">
    <template #trigger>
      <button
        ref="triggerEl"
        type="button"
        :aria-label="t('layout.userMenu.open')"
        aria-haspopup="menu"
        :aria-expanded="open"
        class="flex cursor-pointer items-center gap-2 rounded-md p-1 text-ink transition-colors hover:bg-ghost-hover max-desktop:min-h-11 max-desktop:min-w-11"
        data-testid="app-header-user-menu"
      >
        <span
          data-testid="user-menu-avatar"
          class="flex size-8 shrink-0 items-center justify-center overflow-hidden rounded-full bg-accent text-caption text-on-accent"
          aria-hidden="true"
        >
          <img v-if="avatarUrl" :src="avatarUrl" alt="" class="h-full w-full object-cover" />
          <template v-else>{{ initials }}</template>
        </span>
        <span class="hidden min-w-0 max-w-40 desktop:block">
          <span class="block truncate text-row" :title="displayName">{{ displayName }}</span>
        </span>
        <n-icon
          data-testid="user-menu-chevron"
          class="hidden shrink-0 text-ink-muted desktop:block"
          :size="14"
          ><ChevronDown
        /></n-icon>
      </button>
    </template>

    <div ref="menuEl" class="flex flex-col gap-0.5" role="menu" @keydown="onMenuKeydown">
      <div class="flex items-center gap-3 px-2 py-1.5">
        <span
          data-testid="user-menu-panel-avatar"
          class="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-accent text-meta text-on-accent"
          aria-hidden="true"
        >
          <img v-if="avatarUrl" :src="avatarUrl" alt="" class="h-full w-full object-cover" />
          <template v-else>{{ initials }}</template>
        </span>
        <div class="flex min-w-0 flex-col leading-tight">
          <span class="truncate text-row text-ink">{{ displayName }}</span>
          <span class="truncate text-caption text-ink-muted">{{ auth.account?.email }}</span>
        </div>
      </div>

      <n-divider class="my-1!" />

      <button
        type="button"
        role="menuitem"
        :class="itemClass"
        data-testid="user-menu-profile"
        @click="viewProfile"
      >
        <n-icon class="shrink-0"><User /></n-icon>
        <span>{{ t('nav.profile') }}</span>
      </button>

      <button
        type="button"
        role="menuitem"
        :class="itemClass"
        data-testid="user-menu-appearance"
        @click="openAppearance"
      >
        <n-icon class="shrink-0"><Palette /></n-icon>
        <span class="flex-1">{{ t('layout.userMenu.appearance') }}</span>
        <span class="text-caption text-ink-muted">{{ t(`layout.settings.${preference}`) }}</span>
      </button>

      <div class="flex items-center gap-2 px-2 pt-1.5 text-caption text-ink-muted">
        <n-icon class="shrink-0"><Language /></n-icon>
        <span>{{ t('common.language') }}</span>
      </div>
      <button
        v-for="locale in locales"
        :key="locale.code"
        type="button"
        role="menuitemradio"
        :aria-checked="localeStore.currentLocale === locale.code"
        :class="itemClass"
        :data-testid="`user-menu-locale-${locale.code}`"
        @click="localeStore.setLocale(locale.code)"
      >
        <n-icon class="shrink-0"><Check v-if="localeStore.currentLocale === locale.code" /></n-icon>
        <span>{{ locale.label }}</span>
      </button>

      <n-divider class="my-1!" />

      <a
        role="menuitem"
        :href="userGuideHref"
        target="_blank"
        rel="noopener"
        :class="itemClass"
        data-testid="app-header-user-guide"
        @click="close"
      >
        <n-icon class="shrink-0"><Help /></n-icon>
        <span>{{ t('nav.userGuide') }}</span>
      </a>

      <button
        type="button"
        role="menuitem"
        :class="[itemClass, 'text-danger!']"
        data-testid="user-menu-logout"
        @click="logout"
      >
        <n-icon class="shrink-0"><Logout /></n-icon>
        <span>{{ t('auth.logout') }}</span>
      </button>
    </div>
  </n-popover>
</template>
