<script setup lang="ts">
import { accessProblemOf } from '@/lib/access-problem'
import type { AccessProblem } from '@/lib/access-problem'
import type { RoleDto } from '@/api/types'
import { useOrganizationPeopleStore } from '@/stores/organization-people'
import OrganizationPeopleTabs from '@/components/OrganizationPeopleTabs.vue'
import RoleRow from '@/components/RoleRow.vue'
import RoleSheet from '@/components/RoleSheet.vue'

const people = useOrganizationPeopleStore()
const { isPhone } = useViewport()

const accessProblem = ref<AccessProblem | null>(null)
const loading = ref(true)
const showSheet = ref(false)
const editingRole = ref<RoleDto | undefined>(undefined)

onMounted(async () => {
  try {
    await Promise.all([people.loadRoles(), people.loadPermissionCatalog()])
  } catch (error) {
    accessProblem.value = accessProblemOf(error)
  } finally {
    loading.value = false
  }
})

function openCreate() {
  editingRole.value = undefined
  showSheet.value = true
}

function openEdit(role: RoleDto) {
  editingRole.value = role
  showSheet.value = true
}
</script>

<template>
  <AccessProblemState v-if="accessProblem" :kind="accessProblem" />

  <div v-else class="mx-auto flex w-full max-w-3xl flex-col gap-4 p-4 desktop:p-6">
    <OrganizationPeopleTabs active="roles">
      <template #action>
        <n-button type="primary" size="small" data-testid="create-role-btn" @click="openCreate">
          {{ isPhone ? $t('roles.createShort') : $t('roles.create') }}
        </n-button>
      </template>
    </OrganizationPeopleTabs>

    <div v-if="loading" class="py-8 text-center text-ink-muted">{{ $t('common.loading') }}</div>

    <div v-else class="overflow-hidden rounded-lg border border-line">
      <RoleRow v-for="role in people.roles" :key="role.id" :role="role" @edit="openEdit" />
    </div>
  </div>

  <RoleSheet v-model:show="showSheet" :role="editingRole" />
</template>
