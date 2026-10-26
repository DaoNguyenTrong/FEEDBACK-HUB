<script setup lang="ts">
import { accessProblemOf } from '@/lib/access-problem'
import type { AccessProblem } from '@/lib/access-problem'
import { Permissions } from '@/lib/permissions'
import { useOrganizationPeopleStore } from '@/stores/organization-people'
import MemberRow from '@/components/MemberRow.vue'
import OrganizationPeopleTabs from '@/components/OrganizationPeopleTabs.vue'

const { t } = useI18n()
const auth = useAuthStore()
const people = useOrganizationPeopleStore()

const accessProblem = ref<AccessProblem | null>(null)

onMounted(async () => {
  try {
    await people.members.loadMore()
  } catch (error) {
    accessProblem.value = accessProblemOf(error)
  }

  if (auth.hasPermission(Permissions.OrganizationMembersManage)) {
    people.loadRoleOptions().catch(() => {})
  }
})

const shown = computed(() => people.members.items.length)
const total = computed(() => people.members.totalCount)

async function loadMore() {
  await people.members.loadMore()
}
</script>

<template>
  <AccessProblemState v-if="accessProblem" :kind="accessProblem" />

  <div v-else class="mx-auto flex w-full max-w-3xl flex-col gap-4 p-4 desktop:p-6">
    <OrganizationPeopleTabs active="members" />

    <div v-if="people.members.loading" class="py-8 text-center text-ink-muted">
      {{ $t('common.loading') }}
    </div>

    <template v-else>
      <div class="flex flex-col gap-2">
        <MemberRow
          v-for="member in people.members.items"
          :key="member.accountId"
          :member="member"
          :organization-name="auth.organizationName ?? ''"
        />
      </div>

      <div v-if="people.members.hasMore" class="flex flex-col items-center gap-2 py-2">
        <p class="text-sm text-ink-muted">
          {{ $t('members.showingOf', { shown, total }) }}
        </p>
        <n-button secondary data-testid="members-show-more" @click="loadMore">
          {{ $t('common.showMore') }}
        </n-button>
      </div>
    </template>
  </div>
</template>
