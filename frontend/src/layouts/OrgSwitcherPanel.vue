<script setup lang="ts">
import { Check, Home, Loader, Plus } from '@vicons/tabler'
import type { OrganizationDto } from '@/api/types'
import { initialsOf } from '@/lib/initials'
import { roleDisplayName } from '@/lib/role-name'

defineProps<{
  organizations: OrganizationDto[]
  currentId: string | null
  busyId: string | null
  error: string | null
  showTitle?: boolean
}>()
const emit = defineEmits<{ pick: [organization: OrganizationDto]; welcome: []; create: [] }>()

const { t } = useI18n()

function rolesOf(organization: OrganizationDto) {
  return organization.myRoles.map((role) => roleDisplayName(role, t)).join(', ')
}

const itemClass =
  'flex w-full cursor-pointer items-center gap-3 rounded-md px-2 py-1.5 text-left text-body text-ink transition-colors hover:bg-ghost-hover max-desktop:min-h-11'
</script>

<template>
  <div class="flex max-h-96 min-w-64 flex-col gap-1 overflow-y-auto">
    <h2 v-if="showTitle" class="px-2 pt-1 text-caption text-ink-muted">
      {{ t('organizations.switcherTitle') }}
    </h2>

    <InlineNotice v-if="error" tone="danger" data-testid="org-switcher-error">
      {{ error }}
    </InlineNotice>

    <div role="menu" :aria-label="t('organizations.switcherTitle')" class="flex flex-col gap-0.5">
      <button
        v-for="organization in organizations"
        :key="organization.id"
        type="button"
        role="menuitemradio"
        :aria-checked="organization.id === currentId"
        :aria-disabled="organization.status ? undefined : 'true'"
        :class="[itemClass, organization.status ? '' : 'cursor-not-allowed opacity-60']"
        :data-testid="`org-switcher-item-${organization.id}`"
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
        <n-icon v-else-if="busyId === organization.id" class="shrink-0">
          <Loader class="animate-spin" />
        </n-icon>
        <n-icon v-else-if="organization.id === currentId" class="shrink-0"><Check /></n-icon>
      </button>

      <n-divider class="my-1!" />

      <button
        type="button"
        role="menuitem"
        :class="itemClass"
        data-testid="org-switcher-welcome"
        @click="emit('welcome')"
      >
        <n-icon class="shrink-0"><Home /></n-icon>
        <span>{{ t('organizations.backToWelcome') }}</span>
      </button>
      <button
        type="button"
        role="menuitem"
        :class="itemClass"
        data-testid="org-switcher-create"
        @click="emit('create')"
      >
        <n-icon class="shrink-0"><Plus /></n-icon>
        <span>{{ t('organizations.create') }}</span>
      </button>
    </div>
  </div>
</template>
