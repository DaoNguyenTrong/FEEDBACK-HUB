import type { ExternalLoginResponse } from '@/api/types'

// `onSignedIn` replaces the default hop to the redirect target (or home) for a page that carries on
// after the sign-in itself, e.g. the invitation page.
export function useGoogleAuth(
  onSignedIn?: (response: ExternalLoginResponse) => void | Promise<void>,
) {
  const router = useRouter()
  const route = useRoute()
  const { t } = useI18n()
  const { isLoading, error, run } = useApiAction()
  const showResendVerification = ref(false)

  async function handleCredential(credential: string) {
    showResendVerification.value = false

    const result = await run(() => useAuthStore().externalLogin('google', { credential }), {
      onCode: {
        ExternalLoginEmailNotConfirmed: () => {
          error.value = t('auth.googleEmailNotConfirmed')
          showResendVerification.value = true
        },
        ExternalLoginEmailNotVerifiedByProvider: () => {
          error.value = t('auth.googleEmailNotVerifiedByProvider')
        },
      },
      fallbackMessage: t('auth.googleLoginFailed'),
    })

    if (result && onSignedIn) {
      await onSignedIn(result)
    } else if (result) {
      await router.push(safeRedirectTarget(route.query.redirect) ?? useHomeRoute())
    }
  }

  return { isLoading, error, showResendVerification, handleCredential }
}
