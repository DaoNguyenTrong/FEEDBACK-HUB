<script setup lang="ts">
import { normalizeSlugInput, suggestSlug } from '@/lib/slug-suggestion'
import { errorMessage } from '@/lib/error-message'
import { mapValidationErrors } from '@/lib/validation'

const show = defineModel<boolean>('show', { required: true })

const { t, te } = useI18n()
const organizationsStore = useOrganizationsStore()
const auth = useAuthStore()
const controlSize = useControlSize()
const { switchOrganization } = useOrganizationSwitch()

// `data-testid` is not in naive-ui's typed input attributes, hence the widened type.
const nameInputProps: Record<string, string> = {
  id: 'create-org-name',
  'data-testid': 'create-org-name',
}
const slugInputProps: Record<string, string> = {
  id: 'create-org-slug',
  'data-testid': 'create-org-slug',
}

const name = ref('')
const slug = ref('')
// Once the person types in the identifier, the name stops overwriting it.
const slugEdited = ref(false)
const nameError = ref<string | null>(null)
const slugError = ref<string | null>(null)
const formError = ref<string | null>(null)
const emailUnconfirmed = ref(false)
const resendState = ref<'idle' | 'sending' | 'sent'>('idle')
const resendError = ref<string | null>(null)
const submitting = ref(false)

function reset() {
  name.value = ''
  slug.value = ''
  slugEdited.value = false
  clearErrors()
  emailUnconfirmed.value = false
  resendState.value = 'idle'
  resendError.value = null
}

function clearErrors() {
  nameError.value = null
  slugError.value = null
  formError.value = null
}

watch(show, (open) => {
  if (open) reset()
})

function onNameInput(value: string) {
  name.value = value
  nameError.value = null
  if (!slugEdited.value) slug.value = suggestSlug(value)
}

function onSlugInput(value: string) {
  slugEdited.value = true
  slug.value = normalizeSlugInput(value)
  slugError.value = null
}

async function submit() {
  if (submitting.value) return
  clearErrors()
  emailUnconfirmed.value = false

  const trimmed = name.value.trim()
  if (trimmed === '') {
    nameError.value = t('organizations.nameRequired')
    return
  }

  submitting.value = true
  try {
    const organization = await organizationsStore.create({ name: trimmed, slug: slug.value })
    await switchOrganization(organization.id)
  } catch (error) {
    showFailure(error)
  } finally {
    submitting.value = false
  }
}

function showFailure(error: unknown) {
  const code = error instanceof ApiError ? error.problem.code : undefined
  if (code === 'OrganizationSlugInvalid' || code === 'OrganizationSlugAlreadyExists') {
    slugError.value = errorMessage(error, t, te)
  } else if (code === 'EmailNotConfirmed') {
    emailUnconfirmed.value = true
  } else if (code === 'ValidationFailed' && error instanceof ApiError) {
    const fields = mapValidationErrors(error.problem.errors)
    nameError.value = fields.name ?? null
    slugError.value = fields.slug ?? null
    if (!nameError.value && !slugError.value) formError.value = t('errors.requestFailed')
  } else {
    formError.value = errorMessage(error, t, te)
  }
}

async function resend() {
  const email = auth.account?.email
  if (!email || resendState.value === 'sending') return
  resendState.value = 'sending'
  resendError.value = null
  try {
    await auth.resendVerification({ email })
    resendState.value = 'sent'
  } catch (error) {
    resendState.value = 'idle'
    resendError.value = errorMessage(error, t, te)
  }
}
</script>

<template>
  <AppSheet v-model:show="show" :title="t('organizations.createTitle')">
    <form class="flex flex-col gap-4" novalidate @submit.prevent="submit">
      <InlineNotice v-if="emailUnconfirmed" tone="warning" data-testid="create-org-unverified">
        <p>{{ t('errorCodes.EmailNotConfirmed') }}</p>
        <p v-if="resendState === 'sent'" class="mt-1">{{ t('auth.resendVerificationSuccess') }}</p>
        <p v-if="resendError" class="mt-1">{{ resendError }}</p>
        <template v-if="resendState !== 'sent'" #action>
          <n-button
            text
            type="primary"
            :size="controlSize"
            :loading="resendState === 'sending'"
            data-testid="create-org-resend"
            @click="resend"
          >
            {{ t('organizations.resendVerification') }}
          </n-button>
        </template>
      </InlineNotice>
      <InlineNotice v-if="formError" tone="danger" data-testid="create-org-error">
        {{ formError }}
      </InlineNotice>

      <div class="flex flex-col gap-1.5">
        <label for="create-org-name" class="text-sm font-medium text-ink">
          {{ t('organizations.name') }}<RequiredMark />
        </label>
        <n-input
          :value="name"
          class="w-full"
          :size="controlSize"
          :status="nameError ? 'error' : undefined"
          :input-props="nameInputProps"
          @update:value="onNameInput"
        />
        <p v-if="nameError" class="text-xs text-danger" data-testid="create-org-name-error">
          {{ nameError }}
        </p>
      </div>

      <div class="flex flex-col gap-1.5">
        <label for="create-org-slug" class="text-sm font-medium text-ink">
          {{ t('organizations.slug') }}<RequiredMark />
        </label>
        <n-input
          :value="slug"
          class="w-full"
          :size="controlSize"
          :status="slugError ? 'error' : undefined"
          :input-props="slugInputProps"
          @update:value="onSlugInput"
        />
        <p v-if="slugError" class="text-xs text-danger" data-testid="create-org-slug-error">
          {{ slugError }}
        </p>
        <p class="text-xs text-ink-muted">{{ t('organizations.slugHint') }}</p>
      </div>
    </form>

    <template #footer>
      <div class="flex justify-end gap-2">
        <n-button :size="controlSize" @click="show = false">{{ t('common.cancel') }}</n-button>
        <n-button
          type="primary"
          :size="controlSize"
          :loading="submitting"
          data-testid="create-org-submit"
          @click="submit"
        >
          {{ t('organizations.createTitle') }}
        </n-button>
      </div>
    </template>
  </AppSheet>
</template>
