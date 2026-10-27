<script setup lang="ts">
import { Home, Plus } from '@vicons/tabler'
import type { OrganizationDto } from '@/api/types'

defineProps<{
  organizations: OrganizationDto[]
  currentId: string | null
  busyId: string | null
  error: string | null
  showTitle?: boolean
}>()
const emit = defineEmits<{ pick: [organization: OrganizationDto]; welcome: []; create: [] }>()

const { t } = useI18n()

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
      <OrganizationRows
        :organizations="organizations"
        :current-id="currentId"
        :busy-id="busyId"
        menu
        testid-prefix="org-switcher-item-"
        @pick="emit('pick', $event)"
      />

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
