<script setup lang="ts">
import { Lock, MoodSad } from '@vicons/tabler'
import type { AccessProblem } from '@/lib/access-problem'

// A caller's attrs would land on EmptyState and could replace the generic title or line (BR-45).
defineOptions({ inheritAttrs: false })

const props = defineProps<{ kind: AccessProblem }>()

const router = useRouter()
const { t } = useI18n()

function goHome() {
  return router.push(useHomeRoute())
}

const key = computed(() => (props.kind === 'forbidden' ? 'forbidden' : 'notFound'))
</script>

<template>
  <EmptyState
    :title="t(`accessProblem.${key}Title`)"
    :description="t(`accessProblem.${key}Text`)"
    :heading-level="1"
  >
    <template #icon>
      <n-icon size="24"><Lock v-if="props.kind === 'forbidden'" /><MoodSad v-else /></n-icon>
    </template>
    <template #action>
      <n-button type="primary" @click="goHome">
        {{ t('accessProblem.goHome') }}
      </n-button>
    </template>
  </EmptyState>
</template>
