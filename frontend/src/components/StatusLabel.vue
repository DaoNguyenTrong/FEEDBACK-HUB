<script setup lang="ts">
type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger'

const props = defineProps<{ tone: Tone }>()

const TONE_CLASSES: Record<Tone, string> = {
  neutral: 'bg-sunken text-ink-muted',
  info: 'bg-info-soft text-info',
  success: 'bg-success-soft text-success',
  warning: 'bg-warning-soft text-warning',
  danger: 'bg-danger-soft text-danger',
}
</script>

<template>
  <span
    class="inline-flex items-center rounded-full px-2.5 py-0.5 text-caption whitespace-nowrap"
    :class="TONE_CLASSES[props.tone]"
    :data-tone="props.tone"
  >
    <slot />
  </span>
</template>
