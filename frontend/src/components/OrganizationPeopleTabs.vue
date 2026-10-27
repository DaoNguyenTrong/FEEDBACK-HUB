<script setup lang="ts">
import { Permissions } from '@/lib/permissions'
import { useOrganizationPeopleStore } from '@/stores/organization-people'

const props = defineProps<{
  active: 'members' | 'invitations' | 'roles'
}>()

const auth = useAuthStore()
const router = useRouter()
const people = useOrganizationPeopleStore()

const canManageMembers = computed(() => auth.hasPermission(Permissions.OrganizationMembersManage))
const canManageRoles = computed(() => auth.hasPermission(Permissions.OrganizationRolesManage))
const rolesRouteExists = computed(() => router.hasRoute('organization-roles'))

const membersCount = computed(() => (people.members.loaded ? people.members.totalCount : null))
const invitationsCount = computed(() =>
  people.invitations.loaded ? people.invitations.totalCount : null,
)
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center justify-between gap-4">
      <h1 class="text-section text-ink">{{ $t('nav.members') }}</h1>
      <div class="flex shrink-0 items-center">
        <slot name="action" />
      </div>
    </div>

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
        <span v-if="membersCount !== null" class="opacity-80"> {{ membersCount }}</span>
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
        <span v-if="invitationsCount !== null" class="opacity-80"> {{ invitationsCount }}</span>
      </RouterLink>

      <RouterLink
        v-if="canManageRoles && rolesRouteExists"
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

    <slot />
  </div>
</template>
