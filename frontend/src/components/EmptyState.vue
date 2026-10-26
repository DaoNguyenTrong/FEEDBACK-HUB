<script setup lang="ts">
const props = defineProps<{ title: string; description?: string }>()
</script>

<template>
  <div class="flex flex-col items-center gap-3 px-4 py-10 text-center">
    <div
      v-if="$slots.icon"
      class="flex size-12 items-center justify-center rounded-full bg-accent-soft text-on-accent-soft"
    >
      <slot name="icon" />
    </div>
    <h2 class="text-section text-ink">{{ props.title }}</h2>
    <p v-if="props.description" class="max-w-md text-body text-ink-muted">
      {{ props.description }}
    </p>
    <div v-if="$slots.action" class="mt-1">
      <slot name="action" />
    </div>
  </div>
</template>
