<script setup lang="ts">
import { getCurrentOrganization } from '@/api/generated/current-organization/current-organization'
import { accessProblemOf } from '@/lib/access-problem'
import type { AccessProblem } from '@/lib/access-problem'
import { errorMessage } from '@/lib/error-message'
import { showSuccessMessage } from '@/lib/feedback'
import { mapValidationErrors } from '@/lib/validation'
import { ApiError } from '@/api/types'
import type { CurrentOrganizationDto } from '@/api/types'
import DeactivateOrganizationDialog from '@/components/DeactivateOrganizationDialog.vue'

const client = getCurrentOrganization()
const { t, te } = useI18n()
const auth = useAuthStore()
const organizationsStore = useOrganizationsStore()
const contextStore = useOrganizationContextStore()
const controlSize = useControlSize()
const { isPhone } = useViewport()

const organization = ref<CurrentOrganizationDto | null>(null)
const loading = ref(true)
const accessProblem = ref<AccessProblem | null>(null)
const loadError = ref<string | null>(null)

const name = ref('')
const nameError = ref<string | null>(null)
const saveError = ref<string | null>(null)
const saving = ref(false)

const dialogOpen = ref(false)
const deactivating = ref(false)
const deactivateError = ref<string | null>(null)
const reasonError = ref<string | null>(null)

const nameInputProps: Record<string, string> = {
  id: 'org-settings-name',
  'data-testid': 'org-settings-name',
}

onMounted(load)

async function load() {
  loading.value = true
  try {
    organization.value = await client.currentOrganizationGet()
    name.value = organization.value.name
  } catch (error) {
    accessProblem.value = accessProblemOf(error)
    if (!accessProblem.value) loadError.value = errorMessage(error, t, te)
  } finally {
    loading.value = false
  }
}

function onNameInput(value: string) {
  name.value = value
  nameError.value = null
}

async function save() {
  if (saving.value || !organization.value) return
  nameError.value = null
  saveError.value = null

  const trimmed = name.value.trim()
  if (trimmed === '') {
    nameError.value = t('organizations.nameRequired')
    return
  }

  saving.value = true
  try {
    const renamed = await client.currentOrganizationRename({ name: trimmed })
    organization.value = renamed
    name.value = renamed.name
    auth.organizationName = renamed.name
    organizationsStore.applyRename(renamed.id, renamed.name)
    showSuccessMessage(t('organizationSettings.renamed', { name: renamed.name }))
  } catch (error) {
    showSaveFailure(error)
  } finally {
    saving.value = false
  }
}

function showSaveFailure(error: unknown) {
  const code = error instanceof ApiError ? error.problem.code : undefined
  if (code === 'OrganizationNameRequired') {
    nameError.value = errorMessage(error, t, te)
  } else if (code === 'ValidationFailed' && error instanceof ApiError) {
    nameError.value = mapValidationErrors(error.problem.errors).name ?? t('errors.requestFailed')
  } else {
    saveError.value = errorMessage(error, t, te)
  }
}

function openDeactivate() {
  deactivateError.value = null
  reasonError.value = null
  dialogOpen.value = true
}

async function deactivate(reason: string | null) {
  if (deactivating.value) return
  deactivating.value = true
  deactivateError.value = null
  reasonError.value = null

  // Our own exit: the next 403 from the suspended organization must not read as "someone else did it".
  contextStore.beginOwnExit()
  try {
    await client.currentOrganizationDeactivate({ reason })
  } catch (error) {
    contextStore.cancelOwnExit()
    showDeactivateFailure(error)
    deactivating.value = false
    return
  }

  try {
    await contextStore.leaveToWelcome()
  } catch (error) {
    // Deactivated, but the switch failed: let the shared handler take over from here.
    contextStore.cancelOwnExit()
    deactivateError.value = errorMessage(error, t, te)
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
</script>

<template>
  <AccessProblemState v-if="accessProblem" :kind="accessProblem" />

  <div v-else class="mx-auto flex w-full max-w-[720px] flex-col gap-8 p-4 desktop:p-6">
    <h1 class="text-section text-ink">{{ t('organizationSettings.title') }}</h1>

    <InlineNotice v-if="loadError" tone="danger" data-testid="org-settings-load-error">
      {{ loadError }}
    </InlineNotice>

    <template v-if="organization">
      <section class="flex flex-col gap-4" data-testid="org-settings-info">
        <h2 class="text-row text-ink">{{ t('organizationSettings.infoTitle') }}</h2>
        <InlineNotice v-if="saveError" tone="danger" data-testid="org-settings-save-error">
          {{ saveError }}
        </InlineNotice>
        <form class="flex flex-col gap-4" novalidate @submit.prevent="save">
          <div class="flex flex-col gap-1.5">
            <label for="org-settings-name" class="text-sm font-medium text-ink">
              {{ t('organizationSettings.name') }}
            </label>
            <div class="flex gap-2" :class="isPhone ? 'flex-col' : 'items-start'">
              <n-input
                :value="name"
                class="w-full"
                :size="controlSize"
                :status="nameError ? 'error' : undefined"
                :input-props="nameInputProps"
                @update:value="onNameInput"
              />
              <n-button
                type="primary"
                attr-type="submit"
                :size="controlSize"
                :loading="saving"
                :class="isPhone ? 'w-full' : 'shrink-0'"
                data-testid="org-settings-save"
              >
                {{ t('organizationSettings.save') }}
              </n-button>
            </div>
            <p v-if="nameError" class="text-xs text-danger" data-testid="org-settings-name-error">
              {{ nameError }}
            </p>
          </div>

          <div class="flex flex-col gap-1">
            <span class="text-sm font-medium text-ink">{{ t('organizationSettings.slug') }}</span>
            <span class="font-mono text-body text-ink" data-testid="org-settings-slug">
              {{ organization.slug }}
            </span>
            <span class="text-xs text-ink-muted">{{ t('organizationSettings.slugFixed') }}</span>
          </div>
        </form>
      </section>

      <section class="flex flex-col gap-3" data-testid="org-settings-deactivate">
        <h2 class="text-row text-ink">{{ t('organizationSettings.deactivateTitle') }}</h2>
        <p class="text-body text-ink-muted">{{ t('organizationSettings.deactivateText') }}</p>
        <n-button
          secondary
          type="error"
          :size="controlSize"
          :class="isPhone ? 'w-full' : 'self-start'"
          data-testid="org-settings-deactivate-open"
          @click="openDeactivate"
        >
          {{ t('organizationSettings.deactivateAction') }}
        </n-button>
      </section>

      <DeactivateOrganizationDialog
        v-model:show="dialogOpen"
        :organization-name="organization.name"
        :reason-required="auth.isSystemAdministrator"
        :variant="auth.isSystemAdministrator ? 'system-member' : 'member'"
        :busy="deactivating"
        :error="deactivateError"
        :reason-error="reasonError"
        @submit="deactivate"
      />
    </template>
  </div>
</template>
