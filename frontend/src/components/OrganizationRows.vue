<script setup lang="ts">
import { Check, Loader } from '@vicons/tabler'
import type { OrganizationDto } from '@/api/types'
import { initialsOf } from '@/lib/initials'
import { roleDisplayName } from '@/lib/role-name'

const props = defineProps<{
  organizations: OrganizationDto[]
  currentId: string | null
  busyId: string | null
  /** Menu semantics inside the switcher; plain buttons on the welcome page. */
  menu?: boolean
  testidPrefix: string
}>()
const emit = defineEmits<{ pick: [organization: OrganizationDto] }>()

const { t } = useI18n()

function rolesOf(organization: OrganizationDto) {
  return organization.myRoles.map((role) => roleDisplayName(role, t)).join(', ')
}

const itemClass =
  'flex w-full cursor-pointer items-center gap-3 rounded-md px-2 py-1.5 text-left text-body text-ink transition-colors hover:bg-ghost-hover max-desktop:min-h-11'
</script>

<template>
  <button
    v-for="organization in props.organizations"
    :key="organization.id"
    type="button"
    :role="props.menu ? 'menuitemradio' : undefined"
    :aria-checked="props.menu ? organization.id === props.currentId : undefined"
    :aria-disabled="organization.status ? undefined : 'true'"
    :class="[itemClass, organization.status ? '' : 'cursor-not-allowed opacity-60']"
    :data-testid="`${props.testidPrefix}${organization.id}`"
    @click="emit('pick', organization)"
  >
    <span
      class="flex size-8 shrink-0 items-center justify-center rounded-md bg-sunken text-caption text-ink-muted"
      aria-hidden="true"
    >
      {{ initialsOf(organization.name) }}
    </span>
    <span class="flex min-w-0 flex-1 flex-col leading-tight">
      <span class="truncate text-row">{{ organization.name }}</span>
      <span class="truncate text-caption text-ink-muted">{{ rolesOf(organization) }}</span>
    </span>
    <StatusLabel v-if="!organization.status" tone="neutral">
      {{ t('organizations.suspended') }}
    </StatusLabel>
    <n-icon v-else-if="props.busyId === organization.id" class="shrink-0">
      <Loader class="animate-spin" />
    </n-icon>
    <n-icon v-else-if="props.menu && organization.id === props.currentId" class="shrink-0">
      <Check />
    </n-icon>
  </button>
</template>
