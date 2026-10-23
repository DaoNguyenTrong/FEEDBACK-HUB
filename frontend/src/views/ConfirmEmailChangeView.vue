<script setup lang="ts">
import { getAuth } from '@/api/generated/auth/auth'
import { getProfile } from '@/api/generated/profile/profile'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const { wordmarkSrc } = useBrandWordmark()
const authStore = useAuthStore()

const status = ref<'confirming' | 'success' | 'error'>('confirming')
const errorMessage = ref('')

function extractToken(raw: unknown): string | null {
  return typeof raw === 'string' && raw.trim().length > 0 ? raw : null
}

// A signed-in reader keeps the old email in the auth store until the profile is read again.
async function reloadProfile() {
  if (!authStore.isAuthenticated) return
  try {
    const profile = await getProfile().profileGet()
    if (authStore.account) authStore.account.email = profile.email
  } catch {
    // The change is already confirmed; a stale header email is not worth an error screen.
  }
}

async function confirm() {
  const token = extractToken(route.query.token)

  // The token is single-use and secret: take it out of the address bar (and history) before any
  // request, so it can't leak through the Referer header, a screenshot or a shared URL.
  await router.replace({ path: route.path, query: {}, hash: route.hash })

  if (!token) {
    errorMessage.value = t('confirmEmailChange.missingToken')
    status.value = 'error'
    return
  }

  try {
    await getAuth().authConfirmEmailChange({ token })
    status.value = 'success'
    await reloadProfile()
  } catch (error) {
    const code = error instanceof ApiError ? error.problem.code : undefined
    if (code === 'EmailChangeLinkNoLongerValid') {
      errorMessage.value = t('confirmEmailChange.noLongerValid')
    } else if (code === 'AccountEmailAlreadyExists') {
      errorMessage.value = t('confirmEmailChange.alreadyTaken')
    } else {
      errorMessage.value = t('confirmEmailChange.failed')
    }
    status.value = 'error'
  }
}

onMounted(confirm)
</script>

<template>
  <section class="relative min-h-dvh bg-surface-50 px-4 py-8 dark:bg-surface-950">
    <div class="absolute right-4 top-4">
      <AppControls />
    </div>
    <div class="mx-auto flex min-h-[calc(100dvh-4rem)] max-w-md items-center justify-center">
      <div
        class="w-full overflow-hidden rounded-2xl border border-surface-200 bg-surface-0 dark:border-surface-800 dark:bg-surface-900"
      >
        <div class="h-1 bg-primary-500" />

        <div class="p-6 sm:p-8">
          <div class="flex justify-center">
            <img :alt="t('app.name')" class="h-12 w-auto" :src="wordmarkSrc" />
          </div>

          <div v-if="status === 'confirming'" class="mt-8 flex flex-col items-center gap-4 py-4">
            <n-spin size="large" />
            <p class="text-sm text-surface-600 dark:text-surface-300">
              {{ t('confirmEmailChange.confirming') }}
            </p>
          </div>

          <div v-else class="mt-8 space-y-5">
            <n-alert
              :type="status === 'success' ? 'success' : 'error'"
              :show-icon="false"
              data-testid="confirm-email-change-result"
            >
              {{ status === 'success' ? t('confirmEmailChange.success') : errorMessage }}
            </n-alert>
            <RouterLink
              class="block text-center text-sm font-medium text-primary-600 hover:underline dark:text-primary-200"
              :to="authStore.isAuthenticated ? { name: 'home' } : { name: 'login' }"
            >
              {{
                authStore.isAuthenticated
                  ? t('confirmEmailChange.backToApp')
                  : t('auth.backToLogin')
              }}
            </RouterLink>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
