<script setup lang="ts">
import { ChevronDown } from '@vicons/tabler'
import type { OrganizationDto } from '@/api/types'
import { errorMessage } from '@/lib/error-message'
import { initialsOf } from '@/lib/initials'
import OrgSwitcherPanel from './OrgSwitcherPanel.vue'

const { t, te } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const layout = useLayoutStore()
const organizationsStore = useOrganizationsStore()
const { switchOrganization } = useOrganizationSwitch()
const { isPhone } = useViewport()

const open = ref(false)
const triggerEl = ref<HTMLElement | null>(null)
const panel = ref<{ $el: HTMLElement } | null>(null)
const menuEl = computed(() => panel.value?.$el ?? null)
const busyId = ref<string | null>(null)
const switching = ref(false)
const error = ref<string | null>(null)

const { onKeydown } = useMenuKeyboard(menuEl, triggerEl, open)

const currentName = computed(() =>
  auth.organizationId === null
    ? t('organizations.personalWorkspace')
    : (auth.organizationName ?? ''),
)

watch(open, async (isOpen) => {
  if (!isOpen) return
  error.value = null
  if (organizationsStore.loaded) return
  try {
    await organizationsStore.fetchMine()
  } catch (failure) {
    error.value = errorMessage(failure, t, te)
  }
})

async function runSwitch(organizationId: string | null, busy: string | null) {
  // A successful switch ends in a page reload, so the flag is only released on failure.
  if (switching.value) return
  switching.value = true
  busyId.value = busy
  error.value = null
  try {
    await switchOrganization(organizationId)
  } catch (failure) {
    error.value = errorMessage(failure, t, te)
    switching.value = false
    busyId.value = null
  }
}

function pick(organization: OrganizationDto) {
  if (!organization.status) return
  if (organization.id === auth.organizationId) {
    open.value = false
    return
  }
  void runSwitch(organization.id, organization.id)
}

function backToWelcome() {
  if (auth.organizationId === null) {
    open.value = false
    void router.push(useHomeRoute())
    return
  }
  void runSwitch(null, null)
}

function create() {
  open.value = false
  layout.openCreateOrganization()
}

const panelProps = computed(() => ({
  organizations: organizationsStore.myOrganizations,
  currentId: auth.organizationId,
  busyId: busyId.value,
  error: error.value,
}))

const triggerClass =
  'flex min-w-0 max-w-full cursor-pointer items-center gap-2 rounded-md p-1 text-ink transition-colors hover:bg-ghost-hover max-desktop:min-h-11'
</script>

<template>
  <n-popover
    :show="open && !isPhone"
    :trigger="isPhone ? 'manual' : 'click'"
    placement="bottom-start"
    :width="320"
    @update:show="open = $event"
  >
    <template #trigger>
      <button
        ref="triggerEl"
        type="button"
        aria-haspopup="menu"
        :aria-expanded="open"
        :class="triggerClass"
        data-testid="org-switcher-trigger"
        @click="isPhone && (open = !open)"
      >
        <span
          class="flex size-8 shrink-0 items-center justify-center rounded-md bg-sunken text-caption text-ink-muted"
          aria-hidden="true"
        >
          {{ initialsOf(currentName) }}
        </span>
        <span class="min-w-0 truncate text-row">{{ currentName }}</span>
        <n-icon class="shrink-0 text-ink-muted" :size="14"><ChevronDown /></n-icon>
      </button>
    </template>

    <OrgSwitcherPanel
      v-if="!isPhone"
      ref="panel"
      v-bind="panelProps"
      show-title
      @keydown="onKeydown"
      @pick="pick"
      @welcome="backToWelcome"
      @create="create"
    />
  </n-popover>

  <AppSheet v-if="isPhone" v-model:show="open" :title="t('organizations.switcherTitle')">
    <OrgSwitcherPanel
      ref="panel"
      v-bind="panelProps"
      @keydown="onKeydown"
      @pick="pick"
      @welcome="backToWelcome"
      @create="create"
    />
  </AppSheet>
</template>
