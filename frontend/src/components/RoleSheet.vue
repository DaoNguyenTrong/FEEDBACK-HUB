<script setup lang="ts">
import type { RoleDto } from '@/api/types'
import { ApiError } from '@/api/types'
import { errorMessage } from '@/lib/error-message'
import { showSuccessMessage } from '@/lib/feedback'
import { permissionDescription, permissionName } from '@/lib/permission-label'
import { mapValidationErrors } from '@/lib/validation'
import { useOrganizationPeopleStore } from '@/stores/organization-people'

const props = defineProps<{ role?: RoleDto }>()
const show = defineModel<boolean>('show', { required: true })

const { t, te } = useI18n()
const auth = useAuthStore()
const people = useOrganizationPeopleStore()
const controlSize = useControlSize()

const nameInputProps: Record<string, string> = { id: 'role-name', 'data-testid': 'role-name' }

const name = ref('')
const selectedCodes = ref<string[]>([])
const nameError = ref<string | null>(null)
const formError = ref<string | null>(null)
const submitting = ref(false)

watch(
  show,
  (open) => {
    if (!open) return
    name.value = props.role?.name ?? ''
    selectedCodes.value = [...(props.role?.permissionCodes ?? [])]
    nameError.value = null
    formError.value = null
  },
  { immediate: true },
)

function toggle(code: string, checked: boolean) {
  selectedCodes.value = checked
    ? [...selectedCodes.value, code]
    : selectedCodes.value.filter((c) => c !== code)
}

// The server may send a message code (`FieldMaxLength`) or the message already translated.
function nameMessage(message: string): string {
  return te(`errorCodes.${message}`) ? t(`errorCodes.${message}`) : message
}

function showFailure(error: unknown) {
  const code = error instanceof ApiError ? error.problem.code : undefined
  if (code === 'OrganizationRoleNameAlreadyExists' || code === 'RoleNameReservedForSystemRole') {
    nameError.value = errorMessage(error, t, te)
  } else if (code === 'ValidationFailed' && error instanceof ApiError) {
    const message = mapValidationErrors(error.problem.errors).name
    if (message) nameError.value = nameMessage(message)
    else formError.value = errorMessage(error, t, te)
  } else {
    formError.value = errorMessage(error, t, te)
  }
}

async function submit() {
  if (submitting.value) return
  nameError.value = null
  formError.value = null

  const trimmed = name.value.trim()
  if (trimmed === '') {
    nameError.value = t('roles.nameRequired')
    return
  }

  submitting.value = true
  try {
    if (props.role) {
      await people.updateRole(props.role.id, trimmed, selectedCodes.value)
      showSuccessMessage(t('roles.saved', { name: trimmed }))
    } else {
      await people.createRole(trimmed, selectedCodes.value)
      showSuccessMessage(t('roles.created', { name: trimmed }))
    }
    show.value = false
  } catch (error) {
    showFailure(error)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <AppSheet v-model:show="show" :title="role ? t('roles.edit') : t('roles.create')">
    <form class="flex flex-col gap-4" novalidate @submit.prevent="submit">
      <InlineNotice v-if="formError" tone="danger" data-testid="role-error">
        {{ formError }}
      </InlineNotice>

      <div class="flex flex-col gap-1.5">
        <label for="role-name" class="text-sm font-medium text-ink">
          {{ t('roles.nameLabel') }}<RequiredMark />
        </label>
        <n-input
          v-model:value="name"
          class="w-full"
          :size="controlSize"
          :maxlength="100"
          :status="nameError ? 'error' : undefined"
          :input-props="nameInputProps"
          @update:value="nameError = null"
        />
        <p v-if="nameError" class="text-xs text-danger" data-testid="role-name-error">
          {{ nameError }}
        </p>
      </div>

      <fieldset class="flex flex-col gap-2">
        <legend class="text-sm font-medium text-ink">{{ t('roles.permissionsLabel') }}</legend>
        <p class="text-xs text-ink-muted">{{ t('roles.grantableOnly') }}</p>
        <div
          v-for="permission in people.permissionCatalog"
          :key="permission.code"
          :data-testid="`permission-option-${permission.code}`"
          class="flex flex-col gap-0.5"
        >
          <label class="flex cursor-pointer items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              :checked="selectedCodes.includes(permission.code)"
              :disabled="!auth.hasPermission(permission.code) || submitting"
              @change="(e) => toggle(permission.code, (e.target as HTMLInputElement).checked)"
            />
            {{ permissionName(permission.code, t) }}
          </label>
          <p class="ml-6 text-xs text-ink-muted">{{ permissionDescription(permission.code, t) }}</p>
        </div>
      </fieldset>
    </form>

    <template #footer>
      <div class="flex justify-end gap-2">
        <n-button :size="controlSize" :disabled="submitting" @click="show = false">
          {{ t('common.cancel') }}
        </n-button>
        <n-button
          type="primary"
          :size="controlSize"
          :loading="submitting"
          data-testid="role-submit"
          @click="submit"
        >
          {{ role ? t('common.save') : t('roles.create') }}
        </n-button>
      </div>
    </template>
  </AppSheet>
</template>
