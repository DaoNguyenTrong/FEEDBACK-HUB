<script setup lang="ts">
import { accessProblemOf } from '@/lib/access-problem'
import type { AccessProblem } from '@/lib/access-problem'
import { Permissions } from '@/lib/permissions'
import { useOrganizationPeopleStore } from '@/stores/organization-people'
import InvitationRow from '@/components/InvitationRow.vue'
import InviteMemberSheet from '@/components/InviteMemberSheet.vue'
import OrganizationPeopleTabs from '@/components/OrganizationPeopleTabs.vue'

const { t } = useI18n()
const auth = useAuthStore()
const people = useOrganizationPeopleStore()
const { isPhone } = useViewport()

const accessProblem = ref<AccessProblem | null>(null)
const showInviteSheet = ref(false)

onMounted(async () => {
  try {
    await people.invitations.loadMore()
  } catch (error) {
    accessProblem.value = accessProblemOf(error)
  }

  if (auth.hasPermission(Permissions.OrganizationMembersManage)) {
    people.loadRoleOptions().catch(() => {})
  }
})

const shown = computed(() => people.invitations.items.length)
const total = computed(() => people.invitations.totalCount)
</script>

<template>
  <AccessProblemState v-if="accessProblem" :kind="accessProblem" />

  <div v-else class="mx-auto flex w-full max-w-3xl flex-col gap-4 p-4 desktop:p-6">
    <OrganizationPeopleTabs active="invitations">
      <template #action>
        <n-button
          type="primary"
          size="small"
          data-testid="invite-member-btn"
          @click="showInviteSheet = true"
        >
          {{ isPhone ? $t('invitations.inviteShort') : $t('invitations.inviteMember') }}
        </n-button>
      </template>
    </OrganizationPeopleTabs>

    <div v-if="people.invitations.loading" class="py-8 text-center text-ink-muted">
      {{ $t('common.loading') }}
    </div>

    <template v-else>
      <!-- Empty state -->
      <EmptyState
        v-if="people.invitations.items.length === 0"
        :title="$t('invitations.emptyTitle')"
        :description="$t('invitations.emptyDescription')"
        data-testid="invitations-empty"
      >
        <template #action>
          <n-button type="primary" @click="showInviteSheet = true">
            {{ $t('invitations.inviteMember') }}
          </n-button>
        </template>
      </EmptyState>

      <template v-else>
        <div class="rounded-lg border border-line overflow-hidden">
          <InvitationRow
            v-for="invitation in people.invitations.items"
            :key="invitation.id"
            :invitation="invitation"
            @removed="people.invitations.reload()"
          />
        </div>

        <div v-if="people.invitations.hasMore" class="flex flex-col items-center gap-2 py-2">
          <p class="text-sm text-ink-muted">
            {{ $t('invitations.showingOf', { shown, total }) }}
          </p>
          <n-button
            secondary
            data-testid="invitations-show-more"
            @click="people.invitations.loadMore()"
          >
            {{ $t('common.showMore') }}
          </n-button>
        </div>
      </template>
    </template>
  </div>

  <InviteMemberSheet v-model:show="showInviteSheet" />
</template>
