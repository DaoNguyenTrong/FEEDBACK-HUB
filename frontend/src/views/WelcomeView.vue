<script setup lang="ts">
import { Plus } from '@vicons/tabler'
import type { OrganizationDto } from '@/api/types'
import { errorMessage } from '@/lib/error-message'
import UserMenu from '@/layouts/UserMenu.vue'
import AppearanceSettings from '@/layouts/AppearanceSettings.vue'

const { t, te } = useI18n()
const auth = useAuthStore()
const layout = useLayoutStore()
const organizationsStore = useOrganizationsStore()
const contextStore = useOrganizationContextStore()
const { switchOrganization } = useOrganizationSwitch()
const { wordmarkSrc } = useBrandWordmark()

// Read in setup: the notice is shown once, and the key is gone after the first read.
const suspendedNoticeName = contextStore.consumeSuspendedNotice()

const loading = ref(true)
const error = ref<string | null>(null)
const busyId = ref<string | null>(null)

const displayName = computed(() => auth.account?.name || auth.account?.username || t('common.user'))
const organizations = computed(() => organizationsStore.myOrganizations)
const hasActive = computed(() => organizations.value.some((organization) => organization.status))
const allSuspended = computed(() => organizations.value.length > 0 && !hasActive.value)
const suspendedNames = computed(() =>
  organizations.value
    .filter((organization) => !organization.status)
    .map((organization) => organization.name)
    .join(', '),
)

onMounted(async () => {
  try {
    await organizationsStore.fetchMine()
  } catch (failure) {
    error.value = errorMessage(failure, t, te)
  } finally {
    loading.value = false
  }
})

async function pick(organization: OrganizationDto) {
  if (!organization.status || busyId.value !== null) return
  busyId.value = organization.id
  error.value = null
  try {
    await switchOrganization(organization.id)
  } catch (failure) {
    error.value = errorMessage(failure, t, te)
    busyId.value = null
  }
}
</script>

<template>
  <div class="relative isolate flex min-h-dvh flex-col bg-page text-ink">
    <AmbientBackdrop strength="full" />

    <header class="flex h-14 shrink-0 items-center justify-between px-4 desktop:px-6">
      <img :alt="t('app.name')" class="h-8 w-auto" :src="wordmarkSrc" />
      <UserMenu />
    </header>

    <main class="mx-auto flex w-full max-w-lg flex-1 flex-col gap-4 px-4 pb-10 pt-6">
      <h1 class="text-section text-ink" data-testid="welcome-greeting">
        {{ t('welcome.greeting', { name: displayName }) }}
      </h1>

      <InlineNotice v-if="suspendedNoticeName" tone="info" data-testid="welcome-suspended-notice">
        {{ t('welcome.justSuspended', { name: suspendedNoticeName }) }}
      </InlineNotice>

      <InlineNotice v-if="error" tone="danger" data-testid="welcome-error">
        {{ error }}
      </InlineNotice>

      <template v-if="!loading">
        <p
          v-if="organizations.length === 0"
          class="text-body text-ink-muted"
          data-testid="welcome-empty"
        >
          {{ t('welcome.noOrganizations') }}
        </p>

        <InlineNotice v-else-if="allSuspended" tone="warning" data-testid="welcome-all-suspended">
          {{ t('welcome.allSuspended', { names: suspendedNames }) }}
        </InlineNotice>

        <section v-if="organizations.length > 0" class="flex flex-col gap-1">
          <h2 v-if="hasActive" class="text-caption text-ink-muted">{{ t('welcome.choose') }}</h2>
          <div class="flex flex-col gap-0.5" data-testid="welcome-organizations">
            <OrganizationRows
              :organizations="organizations"
              :current-id="auth.organizationId"
              :busy-id="busyId"
              testid-prefix="welcome-org-"
              @pick="pick"
            />
          </div>
        </section>
      </template>

      <button
        type="button"
        class="flex w-full cursor-pointer items-center gap-3 rounded-lg border border-line bg-raised p-3 text-left transition-colors hover:bg-ghost-hover max-desktop:min-h-11"
        data-testid="welcome-create"
        @click="layout.openCreateOrganization()"
      >
        <span
          class="flex size-8 shrink-0 items-center justify-center rounded-md bg-accent-soft text-on-accent-soft"
        >
          <n-icon><Plus /></n-icon>
        </span>
        <span class="flex min-w-0 flex-col leading-tight">
          <span class="text-row">{{ t('welcome.createCard') }}</span>
          <span v-if="organizations.length > 0" class="text-caption text-ink-muted">
            {{ t('welcome.createHint') }}
          </span>
        </span>
      </button>

      <RouterLink
        v-if="auth.isSystemAdministrator"
        to="/system/organizations"
        class="self-start text-body text-accent underline-offset-2 hover:underline max-desktop:flex max-desktop:min-h-11 max-desktop:items-center"
        data-testid="welcome-system-organizations"
      >
        {{ t('welcome.systemOrganizations') }}
      </RouterLink>
    </main>

    <AppearanceSettings />
    <ProfilePanel v-model:visible="layout.profileOpen" />
    <CreateOrganizationSheet v-model:show="layout.createOrganizationOpen" />
  </div>
</template>
