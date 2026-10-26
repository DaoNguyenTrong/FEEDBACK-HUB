<script setup lang="ts">
import { Minus, Plus, Refresh } from '@vicons/tabler'
import type { ThemePreference } from '@/lib/theme'
import type { SupportedLocale } from '@/plugins/i18n'

const { t } = useI18n()
const layout = useLayoutStore()
const { settingsOpen } = storeToRefs(layout)
const { preference, setPreference } = useThemePreference()
const localeStore = useLocaleStore()

const COLOR_MODES: readonly ThemePreference[] = ['light', 'dark', 'system']

const locales = computed<{ code: SupportedLocale; label: string }[]>(() => [
  { code: 'vi', label: t('layout.userMenu.vietnamese') },
  { code: 'en', label: t('layout.userMenu.english') },
])

// Font size (same behaviour as AppControls)
const fontSize = ref(getFontSize())
const canDecrease = computed(() => fontSize.value > MIN_FONT_SIZE)
const canIncrease = computed(() => fontSize.value < MAX_FONT_SIZE)
const canReset = computed(() => fontSize.value !== DEFAULT_FONT_SIZE)

function decreaseFontSize() {
  fontSize.value = setFontSize(fontSize.value - 1)
}
function increaseFontSize() {
  fontSize.value = setFontSize(fontSize.value + 1)
}
function resetFontSize() {
  fontSize.value = setFontSize(DEFAULT_FONT_SIZE)
}

const segmentClass = (active: boolean) => [
  'flex-1 rounded-md border px-3 py-1.5 text-body transition-colors max-desktop:min-h-11',
  active
    ? 'border-accent bg-accent-soft text-on-accent-soft font-semibold'
    : 'border-line text-ink hover:bg-ghost-hover',
]
</script>

<template>
  <n-drawer v-model:show="settingsOpen" placement="right" :width="320">
    <n-drawer-content :title="t('layout.settings.title')" closable>
      <div class="flex flex-col gap-6">
        <section :aria-label="t('layout.settings.colorMode')">
          <h3 class="mb-2 text-section text-ink">{{ t('layout.settings.colorMode') }}</h3>
          <div class="flex gap-2">
            <button
              v-for="option in COLOR_MODES"
              :key="option"
              type="button"
              :data-testid="`appearance-theme-${option}`"
              :aria-pressed="preference === option"
              :class="segmentClass(preference === option)"
              @click="setPreference(option)"
            >
              {{ t(`layout.settings.${option}`) }}
            </button>
          </div>
        </section>

        <section :aria-label="t('common.language')">
          <h3 class="mb-2 text-section text-ink">{{ t('common.language') }}</h3>
          <div class="flex gap-2">
            <button
              v-for="option in locales"
              :key="option.code"
              type="button"
              :data-testid="`appearance-locale-${option.code}`"
              :aria-pressed="localeStore.currentLocale === option.code"
              :class="segmentClass(localeStore.currentLocale === option.code)"
              @click="localeStore.setLocale(option.code)"
            >
              {{ option.label }}
            </button>
          </div>
        </section>

        <section :aria-label="t('common.fontSize')">
          <h3 class="mb-2 text-section text-ink">{{ t('common.fontSize') }}</h3>
          <div class="flex items-center gap-2">
            <n-button
              secondary
              class="max-desktop:h-11! max-desktop:min-w-11!"
              :disabled="!canDecrease"
              :aria-label="t('common.decreaseFontSize')"
              @click="decreaseFontSize"
            >
              <template #icon
                ><n-icon><Minus /></n-icon
              ></template>
            </n-button>
            <span class="min-w-12 text-center text-sm tabular-nums">{{ fontSize }}px</span>
            <n-button
              secondary
              class="max-desktop:h-11! max-desktop:min-w-11!"
              :disabled="!canIncrease"
              :aria-label="t('common.increaseFontSize')"
              @click="increaseFontSize"
            >
              <template #icon
                ><n-icon><Plus /></n-icon
              ></template>
            </n-button>
            <n-button
              text
              class="max-desktop:h-11! max-desktop:min-w-11!"
              :disabled="!canReset"
              :aria-label="t('common.resetFontSize')"
              @click="resetFontSize"
            >
              <template #icon
                ><n-icon><Refresh /></n-icon
              ></template>
            </n-button>
          </div>
        </section>
      </div>
    </n-drawer-content>
  </n-drawer>
</template>
