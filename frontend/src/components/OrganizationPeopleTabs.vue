<script setup lang="ts">
import { Permissions } from '@/lib/permissions'

const props = defineProps<{
  active: 'members' | 'invitations' | 'roles'
}>()

const auth = useAuthStore()
const canManageMembers = computed(() => auth.hasPermission(Permissions.OrganizationMembersManage))
const canManageRoles = computed(() => auth.hasPermission(Permissions.OrganizationRolesManage))
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center justify-between gap-4">
      <nav class="flex gap-1" role="tablist">
        <RouterLink
          :to="{ name: 'organization-members' }"
          role="tab"
          :aria-selected="props.active === 'members'"
          class="rounded-md px-3 py-1.5 text-sm font-medium transition-colors"
          :class="
            props.active === 'members'
              ? 'bg-accent text-accent-contrast'
              : 'text-ink-muted hover:bg-fill hover:text-ink'
          "
          data-testid="tab-members"
        >
          {{ $t('nav.members') }}
        </RouterLink>

        <RouterLink
          v-if="canManageMembers"
          :to="{ name: 'organization-invitations' }"
          role="tab"
          :aria-selected="props.active === 'invitations'"
          class="rounded-md px-3 py-1.5 text-sm font-medium transition-colors"
          :class="
            props.active === 'invitations'
              ? 'bg-accent text-accent-contrast'
              : 'text-ink-muted hover:bg-fill hover:text-ink'
          "
          data-testid="tab-invitations"
        >
          {{ $t('nav.invitations') }}
        </RouterLink>

        <RouterLink
          v-if="canManageRoles"
          :to="{ name: 'organization-roles' }"
          role="tab"
          :aria-selected="props.active === 'roles'"
          class="rounded-md px-3 py-1.5 text-sm font-medium transition-colors"
          :class="
            props.active === 'roles'
              ? 'bg-accent text-accent-contrast'
              : 'text-ink-muted hover:bg-fill hover:text-ink'
          "
          data-testid="tab-roles"
        >
          {{ $t('nav.roles') }}
        </RouterLink>
      </nav>

      <div class="flex items-center">
        <slot name="action" />
      </div>
    </div>

    <slot />
  </div>
</template>
