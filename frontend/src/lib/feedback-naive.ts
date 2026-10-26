import { createDiscreteApi, darkTheme } from 'naive-ui'
import type { DialogOptions, MessageOptions } from 'naive-ui'
import naiveThemeOverrides from '@/theme/naive-theme'
import {
  getConfirmationOverrideNaive,
  type ConfirmationOptionsNaive,
} from './confirmation-override'

/**
 * The discrete API mounts its own Vue app and doesn't inherit `<n-config-provider>`, so the theme
 * is passed here; `configProviderProps` accepts a Ref, so this computed follows the dark toggle.
 */
const { isDark } = useThemePreference()

const configProviderProps = computed(() => ({
  theme: isDark.value ? darkTheme : null,
  themeOverrides: isDark.value ? naiveThemeOverrides.dark : naiveThemeOverrides.light,
}))

const { message, dialog } = createDiscreteApi(['message', 'dialog'], {
  configProviderProps,
})

// Exposed so other composables (e.g. `useAppDialogNaive`) can reuse the SAME
// discrete dialog instance instead of creating a second `createDiscreteApi` call.
export { dialog }

export function requestConfirmationNaive(options: ConfirmationOptionsNaive) {
  const confirmOverride = getConfirmationOverrideNaive()
  if (confirmOverride) {
    confirmOverride(options)
    return
  }

  const dialogOptions: DialogOptions = {
    title: options.header,
    content: options.message,
    // Resolved per call, so the labels follow the locale active when the dialog opens.
    positiveText: options.acceptLabel ?? i18n.global.t('common.yes'),
    negativeText: options.rejectLabel ?? i18n.global.t('common.no'),
    onPositiveClick: () => {
      void options.accept?.()
    },
    onNegativeClick: () => {
      void options.reject?.()
    },
  }

  dialog.warning(dialogOptions)
}

type MessageKind = 'success' | 'info' | 'error'

export function messageOptions(kind: MessageKind, life?: number): MessageOptions {
  if (kind === 'error') {
    return { duration: life ?? 0, keepAliveOnHover: true, closable: true }
  }
  return { duration: life ?? 5000, keepAliveOnHover: true }
}

function formatText(summary: string, detail?: string) {
  return detail ? `${summary}\n${detail}` : summary
}

export function showSuccessMessageNaive(summary: string, detail?: string, life?: number) {
  message.success(formatText(summary, detail), messageOptions('success', life))
}

export function showErrorMessageNaive(summary: string, detail?: string, life?: number) {
  message.error(formatText(summary, detail), messageOptions('error', life))
}

export function showInfoMessageNaive(summary: string, detail?: string, life?: number) {
  message.info(formatText(summary, detail), messageOptions('info', life))
}
