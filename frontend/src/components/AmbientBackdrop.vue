<script setup lang="ts">
const props = defineProps<{ strength: 'full' | 'soft' }>()
</script>

<template>
  <div
    aria-hidden="true"
    data-ambient
    class="pointer-events-none fixed inset-0 -z-10 overflow-hidden"
    :style="{ opacity: `var(--ambient-${props.strength})` }"
  >
    <div
      class="absolute -left-24 -top-24 size-[32rem] rounded-full bg-[var(--ambient-1)] blur-3xl"
    />
    <div
      class="absolute -right-32 top-1/3 size-[36rem] rounded-full bg-[var(--ambient-2)] blur-3xl"
    />
    <div
      v-if="props.strength === 'full'"
      class="absolute -bottom-32 left-1/4 size-[30rem] rounded-full bg-[var(--ambient-3)] blur-3xl"
    />
  </div>
</template>
