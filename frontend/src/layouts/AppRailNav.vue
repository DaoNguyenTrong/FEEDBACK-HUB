<script setup lang="ts">
import { toNavSections } from './sidebar-menu'

const props = defineProps<{ variant: 'wide' | 'icon' }>()
const emit = defineEmits<{ navigate: [] }>()

const { t } = useI18n()
const route = useRoute()
const entries = useSidebarMenu()

const sections = computed(() => toNavSections(entries.value))

// A page without its own item names the item to show as open in `meta.activeMenu`.
const activeName = computed(() => route.meta.activeMenu ?? route.name)

const wide = computed(() => props.variant === 'wide')
const linkClass = computed(() => [
  'flex items-center rounded-md transition-colors',
  wide.value ? 'min-h-10 gap-3 px-3 text-row max-desktop:min-h-11' : 'size-11 justify-center',
])
const stateClass = (active: boolean) =>
  active ? 'bg-accent-soft text-on-accent-soft' : 'text-ink-muted hover:bg-ghost-hover'
</script>

<template>
  <nav :aria-label="t('app.name')" class="flex flex-col">
    <template v-for="(section, index) in sections" :key="section.key">
      <p
        v-if="wide && section.headingKey"
        class="px-3 pt-4 pb-1 text-caption uppercase tracking-wider text-ink-muted"
      >
        {{ t(section.headingKey) }}
      </p>
      <div v-else-if="!wide && index > 0" role="separator" class="my-2 border-t border-line" />

      <RouterLink
        v-for="link in section.links"
        :key="link.routeName"
        :to="{ name: link.routeName }"
        :aria-current="link.routeName === activeName ? 'page' : undefined"
        :aria-label="wide ? undefined : t(link.labelKey)"
        :title="wide ? undefined : t(link.labelKey)"
        :class="[linkClass, stateClass(link.routeName === activeName)]"
        @click="emit('navigate')"
      >
        <n-icon :size="20" class="shrink-0"><component :is="link.icon" /></n-icon>
        <span v-if="wide" class="truncate">{{ t(link.labelKey) }}</span>
      </RouterLink>
    </template>
  </nav>
</template>
