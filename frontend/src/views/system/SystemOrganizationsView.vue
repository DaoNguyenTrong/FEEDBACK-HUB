<script setup lang="ts">
import { DotsVertical } from '@vicons/tabler'
import { getSystemOrganizations } from '@/api/generated/system-organizations/system-organizations'
import { ApiError } from '@/api/types'
import type { SystemOrganizationDto } from '@/api/types'
import { accessProblemOf } from '@/lib/access-problem'
import type { AccessProblem } from '@/lib/access-problem'
import { errorMessage } from '@/lib/error-message'
import { showErrorMessage, showSuccessMessage } from '@/lib/feedback'
import { initialsOf } from '@/lib/initials'
import { toPagedResult } from '@/lib/paged-result'
import { mapValidationErrors } from '@/lib/validation'
import DeactivateOrganizationDialog from '@/components/DeactivateOrganizationDialog.vue'

const PAGE_SIZE = 50

const client = getSystemOrganizations()
const { t, te } = useI18n()
const { isPhone } = useViewport()

const list = useLazyList<SystemOrganizationDto>({
  pageSize: PAGE_SIZE,
  fetchPage: async (pageNumber, pageSize) =>
    toPagedResult(await client.systemOrganizationsList({ pageNumber, pageSize })),
})

const accessProblem = ref<AccessProblem | null>(null)
const loadError = ref<string | null>(null)

const shown = computed(() => list.items.value.length)

async function load() {
  accessProblem.value = null
  loadError.value = null
  try {
    await list.reset()
  } catch (error) {
    accessProblem.value = accessProblemOf(error)
    if (!accessProblem.value) loadError.value = errorMessage(error, t, te)
  }
}

onMounted(load)

function memberCountOf(organization: SystemOrganizationDto) {
  return Number(organization.memberCount)
}

function setDeactivated(id: string, isDeactivated: boolean) {
  list.items.value = list.items.value.map((organization) =>
    organization.id === id ? { ...organization, isDeactivated } : organization,
  )
}

async function reloadAfterNotFound(error: unknown) {
  if (!(error instanceof ApiError) || error.status !== 404) return false
  showSuccessMessage(t('common.notFoundReloaded'))
  await load()
  return true
}

// ── Phone: the row's actions open from the bottom ─────────────────────────

const sheetOpen = ref(false)
const sheetTarget = ref<SystemOrganizationDto | null>(null)

function openSheet(organization: SystemOrganizationDto) {
  sheetTarget.value = organization
  sheetOpen.value = true
}

// ── Deactivate ────────────────────────────────────────────────────────────

const dialogOpen = ref(false)
const dialogTarget = ref<SystemOrganizationDto | null>(null)
const deactivating = ref(false)
const deactivateError = ref<string | null>(null)
const reasonError = ref<string | null>(null)

function openDeactivate(organization: SystemOrganizationDto) {
  sheetOpen.value = false
  dialogTarget.value = organization
  deactivateError.value = null
  reasonError.value = null
  dialogOpen.value = true
}

async function deactivate(reason: string | null) {
  const target = dialogTarget.value
  if (!target || deactivating.value) return
  deactivating.value = true
  deactivateError.value = null
  reasonError.value = null
  try {
    await client.systemOrganizationsDeactivate(target.id, { reason })
    setDeactivated(target.id, true)
    dialogOpen.value = false
    showSuccessMessage(t('systemOrganizations.deactivatedToast', { name: target.name }))
  } catch (error) {
    if (await reloadAfterNotFound(error)) {
      dialogOpen.value = false
    } else {
      showDeactivateFailure(error)
    }
  } finally {
    deactivating.value = false
  }
}

function showDeactivateFailure(error: unknown) {
  const code = error instanceof ApiError ? error.problem.code : undefined
  if (code === 'DeactivationReasonRequired') {
    reasonError.value = errorMessage(error, t, te)
  } else if (code === 'ValidationFailed' && error instanceof ApiError) {
    reasonError.value = mapValidationErrors(error.problem.errors).reason ?? null
    if (!reasonError.value) deactivateError.value = t('errors.requestFailed')
  } else {
    deactivateError.value = errorMessage(error, t, te)
  }
}

// ── Reactivate ────────────────────────────────────────────────────────────

const confirmingId = ref<string | null>(null)
const reactivating = ref(false)

function askReactivate(organization: SystemOrganizationDto) {
  sheetOpen.value = false
  confirmingId.value = organization.id
}

async function reactivate(organization: SystemOrganizationDto) {
  if (reactivating.value) return
  reactivating.value = true
  try {
    await client.systemOrganizationsReactivate(organization.id)
    setDeactivated(organization.id, false)
    confirmingId.value = null
    showSuccessMessage(t('systemOrganizations.reactivatedToast', { name: organization.name }))
  } catch (error) {
    confirmingId.value = null
    if (!(await reloadAfterNotFound(error))) showErrorMessage(errorMessage(error, t, te))
  } finally {
    reactivating.value = false
  }
}
</script>

<template>
  <AccessProblemState v-if="accessProblem" :kind="accessProblem" />

  <div v-else class="mx-auto flex w-full max-w-3xl flex-col gap-4 p-4 desktop:p-6">
    <div class="flex flex-col gap-1">
      <h1 class="text-section text-ink">{{ t('systemOrganizations.title') }}</h1>
      <p v-if="!list.loading.value && !loadError" class="text-body text-ink-muted">
        {{ t('systemOrganizations.count', { n: list.totalCount.value }) }}
      </p>
    </div>

    <InlineNotice v-if="loadError" tone="danger" data-testid="system-orgs-error">
      {{ loadError }}
    </InlineNotice>

    <div v-else-if="list.loading.value" class="py-8 text-center text-ink-muted">
      {{ t('common.loading') }}
    </div>

    <template v-else>
      <div class="overflow-hidden rounded-lg border border-line">
        <div
          v-for="organization in list.items.value"
          :key="organization.id"
          class="border-b border-line px-4 py-3 last:border-b-0"
          :data-testid="`system-org-row-${organization.id}`"
        >
          <div class="flex items-center gap-3">
            <div
              class="flex size-9 shrink-0 items-center justify-center rounded-md bg-sunken text-caption text-ink-muted"
              aria-hidden="true"
            >
              {{ initialsOf(organization.name) }}
            </div>

            <div class="min-w-0 flex-1">
              <p class="truncate text-row text-ink">{{ organization.name }}</p>
              <p class="text-caption text-ink-muted">
                {{ t('systemOrganizations.members', { n: memberCountOf(organization) }) }}
              </p>
            </div>

            <StatusLabel v-if="organization.isDeactivated" tone="neutral">
              {{ t('systemOrganizations.deactivated') }}
            </StatusLabel>

            <n-button
              v-if="isPhone"
              size="small"
              secondary
              circle
              class="max-desktop:min-h-11 max-desktop:min-w-11"
              :data-testid="`system-org-actions-${organization.id}`"
              :aria-label="t('systemOrganizations.actionsFor', { name: organization.name })"
              aria-haspopup="dialog"
              @click="openSheet(organization)"
            >
              <template #icon>
                <n-icon><DotsVertical /></n-icon>
              </template>
            </n-button>
            <template v-else>
              <n-button
                v-if="organization.isDeactivated"
                size="small"
                secondary
                :data-testid="`system-org-reactivate-${organization.id}`"
                @click="askReactivate(organization)"
              >
                {{ t('systemOrganizations.reactivate') }}
              </n-button>
              <n-button
                v-else
                size="small"
                secondary
                type="error"
                :data-testid="`system-org-deactivate-${organization.id}`"
                @click="openDeactivate(organization)"
              >
                {{ t('systemOrganizations.deactivate') }}
              </n-button>
            </template>
          </div>

          <InlineConfirm
            v-if="confirmingId === organization.id"
            class="mt-3"
            tone="neutral"
            :message="t('systemOrganizations.reactivateConfirm', { name: organization.name })"
            :detail="t('systemOrganizations.reactivateDetail')"
            :confirm-label="t('systemOrganizations.reactivate')"
            :busy="reactivating"
            @confirm="reactivate(organization)"
            @cancel="confirmingId = null"
          />
        </div>
      </div>

      <div v-if="list.hasMore.value" class="flex flex-col items-center gap-2 py-2">
        <p class="text-sm text-ink-muted">
          {{ t('systemOrganizations.showingOf', { shown, total: list.totalCount.value }) }}
        </p>
        <n-button
          secondary
          :loading="list.loadingMore.value"
          data-testid="system-orgs-show-more"
          @click="list.loadMore()"
        >
          {{ t('common.showMore') }}
        </n-button>
      </div>
    </template>
  </div>

  <AppSheet v-model:show="sheetOpen" :title="sheetTarget?.name ?? ''">
    <template v-if="sheetTarget">
      <p class="mb-4 text-body text-ink-muted">
        {{ t('systemOrganizations.members', { n: memberCountOf(sheetTarget) }) }}
      </p>
      <n-button
        v-if="sheetTarget.isDeactivated"
        block
        secondary
        data-testid="system-org-sheet-reactivate"
        @click="askReactivate(sheetTarget)"
      >
        {{ t('systemOrganizations.reactivate') }}
      </n-button>
      <n-button
        v-else
        block
        secondary
        type="error"
        data-testid="system-org-sheet-deactivate"
        @click="openDeactivate(sheetTarget)"
      >
        {{ t('systemOrganizations.deactivate') }}
      </n-button>
    </template>
  </AppSheet>

  <DeactivateOrganizationDialog
    v-model:show="dialogOpen"
    :organization-name="dialogTarget?.name ?? ''"
    :reason-required="true"
    variant="system"
    :busy="deactivating"
    :error="deactivateError"
    :reason-error="reasonError"
    @update:reason-error="reasonError = null"
    @submit="deactivate"
  />
</template>
