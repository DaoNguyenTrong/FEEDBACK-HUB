<script setup lang="ts">
import axios from 'axios'
import type { FormInst, FormRules } from 'naive-ui'
import { stringifyQuery } from 'vue-router'
import { getInvitations } from '@/api/generated/invitations/invitations'
import type {
  ExternalLoginResponse,
  InvitationLookupDto,
  InvitationLookupStatus,
  RegisterWithInvitationResponse,
} from '@/api/types'
import { formatDateTime } from '@/lib/date-format'
import { initialsOf } from '@/lib/initials'
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH } from '@/lib/password-policy'
import { roleDisplayName } from '@/lib/role-name'
import { reloadTo } from '@/lib/session-exit'
import { writeSuspendedNotice } from '@/lib/suspended-notice'

// Survives the reload of a login round trip; the address bar must not keep the token.
const TOKEN_KEY = 'invitation-token'

// The API refuses an accept with one of these codes; each is the state the page falls back to.
const REFUSAL_STATUS: Record<string, InvitationLookupStatus> = {
  InvitationWrongAccount: 'WrongAccount',
  InvitationExpired: 'Expired',
  InvitationNoLongerValid: 'NoLongerValid',
  InvitationOrganizationSuspended: 'OrganizationSuspended',
  EmailNotConfirmed: 'EmailNotConfirmed',
}

const route = useRoute()
const router = useRouter()
const { t, te, locale } = useI18n()
const auth = useAuthStore()
const { wordmarkSrc } = useBrandWordmark()
const { switchOrganization } = useOrganizationSwitch()
const client = getInvitations()

const status = ref<'loading' | 'failed' | InvitationLookupStatus>('loading')
const lookup = ref<InvitationLookupDto | null>(null)
let token: string | null = null

function readStoredToken(): string | null {
  try {
    return window.sessionStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

function storeToken(value: string) {
  token = value
  try {
    window.sessionStorage.setItem(TOKEN_KEY, value)
  } catch {
    // The page works from memory; only a reload or login round trip loses the token.
  }
}

function forgetToken() {
  token = null
  try {
    window.sessionStorage.removeItem(TOKEN_KEY)
  } catch {
    // Nothing stored if storage is blocked.
  }
}

function queryToken(): string | null {
  const raw = route.query.token
  const value = Array.isArray(raw) ? raw[0] : raw
  return typeof value === 'string' && value.length > 0 ? value : null
}

// Not a router navigation: it must finish before the first request, and only rewrites the bar.
function removeTokenFromAddress() {
  const { token: _token, ...rest } = route.query
  const search = stringifyQuery(rest)
  window.history.replaceState(
    window.history.state,
    '',
    `#${route.path}${search ? `?${search}` : ''}`,
  )
}

const metaTags: HTMLMetaElement[] = []

function addMetaTags() {
  for (const [name, content] of [
    ['robots', 'noindex'],
    ['referrer', 'no-referrer'],
  ] as const) {
    const meta = document.createElement('meta')
    meta.name = name
    meta.content = content
    document.head.appendChild(meta)
    metaTags.push(meta)
  }
}

function applyStatus(next: InvitationLookupStatus) {
  status.value = next
  // Nothing left to do with this token; the other states may still be resolved by the invitee.
  if (next === 'Expired' || next === 'NoLongerValid') forgetToken()
}

async function loadLookup() {
  status.value = 'loading'
  if (!token) {
    applyStatus('NoLongerValid')
    return
  }
  try {
    const result = await client.invitationsLookup({ token })
    lookup.value = result
    applyStatus(result.status)
  } catch {
    status.value = 'failed'
  }
}

onMounted(async () => {
  addMetaTags()
  const fromAddress = queryToken()
  if (fromAddress) {
    storeToken(fromAddress)
    removeTokenFromAddress()
  } else {
    token = readStoredToken()
  }
  await loadLookup()
})

onUnmounted(() => {
  metaTags.forEach((meta) => meta.remove())
})

const organizationName = computed(() => lookup.value?.organizationName ?? '')
const roleLabel = computed(() => (lookup.value?.role ? roleDisplayName(lookup.value.role, t) : ''))
const expiryLabel = computed(() =>
  lookup.value?.expiresAt ? formatDateTime(lookup.value.expiresAt, locale.value) : '',
)
const accountName = computed(() => auth.account?.name || auth.account?.username || '')
const loginLocation = { name: 'login', query: { redirect: '/invite' } }

const resultTitle = computed(() => {
  switch (status.value) {
    case 'WrongAccount':
      return t('invite.wrongAccount.title')
    case 'EmailNotConfirmed':
      return t('invite.emailNotConfirmed.title')
    case 'Expired':
      return t('invite.expired.title')
    case 'OrganizationSuspended':
      return t('invite.suspended.title', { org: organizationName.value })
    default:
      return t('invite.noLongerValid.title')
  }
})

const resultText = computed(() => {
  switch (status.value) {
    case 'EmailNotConfirmed':
      return t('invite.emailNotConfirmed.text')
    case 'Expired':
      return t('invite.expired.text', { time: expiryLabel.value })
    case 'OrganizationSuspended':
      return t('invite.suspended.text', { time: expiryLabel.value })
    case 'WrongAccount':
      return null
    default:
      return t('invite.noLongerValid.text')
  }
})

const isResultState = computed(
  () => status.value !== 'loading' && status.value !== 'failed' && status.value !== 'Valid',
)

// ── Create an account and join ───────────────────────────────────────────

const formRef = ref<FormInst | null>(null)
const form = reactive({ name: '', username: '', password: '' })
const emailTaken = ref(false)
const tooManyRequests = ref(false)
const {
  isLoading: isSubmitting,
  error: submitError,
  fieldErrors,
  run,
  clearFieldError,
} = useApiAction()

const rules = computed<FormRules>(() => ({
  name: [{ required: true, message: t('auth.nameRequired'), trigger: ['input', 'blur'] }],
  username: [{ required: true, message: t('auth.usernameRequired'), trigger: ['input', 'blur'] }],
  password: [
    { required: true, message: t('auth.passwordRequired'), trigger: ['input', 'blur'] },
    {
      min: PASSWORD_MIN_LENGTH,
      message: t('auth.passwordTooShort', { min: PASSWORD_MIN_LENGTH }),
      trigger: ['input', 'blur'],
    },
  ],
}))

function isTooManyRequests(error: unknown) {
  if (error instanceof ApiError) return error.status === 429
  return axios.isAxiosError(error) && error.response?.status === 429
}

function finishRegistration(result: RegisterWithInvitationResponse) {
  forgetToken()
  if (result.invitationOutcome === 'OrganizationSuspended') {
    // The account exists and is signed in, without an organization: the welcome page says why.
    if (organizationName.value) writeSuspendedNotice(organizationName.value)
    reloadTo(router.resolve({ name: 'welcome' }).fullPath)
    return
  }
  reloadTo(router.resolve(useHomeRoute()).fullPath)
}

async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  if (!token) return

  emailTaken.value = false
  tooManyRequests.value = false
  const invitationToken = token

  const result = await run(
    async () => {
      try {
        return await auth.registerWithInvitation({
          token: invitationToken,
          name: form.name.trim(),
          username: form.username.trim(),
          password: form.password,
        })
      } catch (failure) {
        if (!isTooManyRequests(failure)) throw failure
        tooManyRequests.value = true
        return undefined
      }
    },
    {
      onCode: {
        AccountUsernameAlreadyExists: (detail) => {
          fieldErrors.value = {
            ...fieldErrors.value,
            username: detail ?? t('auth.usernameAlreadyExists'),
          }
        },
        AccountEmailAlreadyExists: () => {
          emailTaken.value = true
        },
        InvitationExpired: () => applyStatus('Expired'),
        InvitationNoLongerValid: () => applyStatus('NoLongerValid'),
      },
      fallbackMessage: t('auth.registerFailed'),
    },
  )

  if (result) finishRegistration(result)
}

// ── Accept as the signed-in account ──────────────────────────────────────

const { isLoading: isAccepting, error: acceptError, run: runAccept } = useApiAction()

async function accept() {
  if (!token) return
  const invitationToken = token
  const onCode = Object.fromEntries(
    Object.entries(REFUSAL_STATUS).map(([code, refused]) => [code, () => applyStatus(refused)]),
  )

  const result = await runAccept(() => client.invitationsAccept({ token: invitationToken }), {
    onCode,
    fallbackMessage: t('errors.requestFailed'),
  })
  if (!result) return

  forgetToken()
  try {
    await switchOrganization(result.organizationId)
  } catch (failure) {
    acceptError.value = errorMessage(failure, t, te)
  }
}

async function onExternalSignedIn(response: ExternalLoginResponse) {
  // A new account has nothing else to confirm; an existing one gets the lookup for its own account.
  if (response.accountCreated) {
    await accept()
    return
  }
  await loadLookup()
}

// ── Other states ─────────────────────────────────────────────────────────

async function signOut() {
  try {
    await auth.logout(undefined, { redirect: '/invite' })
  } catch {
    // The auth store reloads to the login page whether or not the server call worked.
  }
}

const { isLoading: isResending, error: resendError, run: runResend } = useApiAction()
const resent = ref(false)

async function resendVerification() {
  const email = auth.account?.email
  if (!email) return
  const done = await runResend(
    async () => {
      await auth.resendVerification({ email })
      return true
    },
    { fallbackMessage: t('auth.resendVerificationFailed') },
  )
  if (done) resent.value = true
}
</script>

<template>
  <div class="relative isolate flex min-h-dvh flex-col bg-page text-ink">
    <AmbientBackdrop strength="full" />

    <header class="flex h-14 shrink-0 items-center justify-between px-4 desktop:px-6">
      <img :alt="t('app.name')" class="h-8 w-auto" :src="wordmarkSrc" />
      <AppControls />
    </header>

    <main class="mx-auto flex w-full max-w-md flex-1 flex-col justify-center gap-4 px-4 pb-10 pt-6">
      <div v-if="status === 'loading'" class="flex justify-center py-10">
        <n-spin size="large" />
      </div>

      <InlineNotice v-else-if="status === 'failed'" tone="danger" data-testid="invite-load-error">
        {{ t('invite.loadFailed') }}
      </InlineNotice>

      <section
        v-else-if="isResultState"
        class="flex flex-col gap-3 rounded-lg border border-line bg-raised p-6"
        :data-testid="`invite-state-${status}`"
      >
        <h1 class="text-section text-ink" data-testid="invite-state-title">{{ resultTitle }}</h1>
        <p v-if="resultText" class="text-body text-ink-muted">{{ resultText }}</p>

        <n-button
          v-if="status === 'WrongAccount'"
          type="primary"
          class="min-h-12 w-full!"
          data-testid="invite-signout"
          @click="signOut"
        >
          {{ t('invite.wrongAccount.signOut') }}
        </n-button>

        <template v-if="status === 'EmailNotConfirmed'">
          <InlineNotice v-if="resent" tone="success" data-testid="invite-resent">
            {{ t('auth.resendVerificationSuccess') }}
          </InlineNotice>
          <InlineNotice v-if="resendError" tone="danger">{{ resendError }}</InlineNotice>
          <n-button
            v-if="!resent"
            type="primary"
            class="min-h-12 w-full!"
            :loading="isResending"
            :disabled="isResending"
            data-testid="invite-resend"
            @click="resendVerification"
          >
            {{ t('auth.resendVerification') }}
          </n-button>
        </template>
      </section>

      <section v-else class="flex flex-col gap-5 rounded-lg border border-line bg-raised p-6">
        <div class="flex flex-col gap-1">
          <h1 class="text-section text-ink" data-testid="invite-title">
            {{ t('invite.title', { org: organizationName }) }}
          </h1>
          <p class="text-body text-ink-muted">
            {{ t('invite.summary', { role: roleLabel, time: expiryLabel }) }}
          </p>
        </div>

        <template v-if="auth.isAuthenticated">
          <div
            class="flex items-center gap-3 rounded-md border border-line p-3"
            data-testid="invite-account-card"
          >
            <span
              class="flex size-10 shrink-0 items-center justify-center rounded-full bg-accent text-caption text-on-accent"
              aria-hidden="true"
            >
              {{ initialsOf(accountName) }}
            </span>
            <span class="flex min-w-0 flex-col leading-tight">
              <span class="truncate text-row">{{ accountName }}</span>
              <span class="truncate text-caption text-ink-muted">{{ auth.account?.email }}</span>
            </span>
          </div>

          <InlineNotice v-if="acceptError" tone="danger" data-testid="invite-accept-error">
            {{ acceptError }}
          </InlineNotice>

          <n-button
            type="primary"
            class="min-h-12 w-full!"
            :loading="isAccepting"
            :disabled="isAccepting"
            data-testid="invite-accept"
            @click="accept"
          >
            {{ t('invite.accept') }}
          </n-button>
        </template>

        <template v-else>
          <n-form
            ref="formRef"
            :model="form"
            :rules="rules"
            class="space-y-5"
            data-testid="invite-form"
            @submit.prevent="handleSubmit"
          >
            <n-form-item
              path="name"
              :show-label="false"
              first
              :feedback="fieldErrors.name || undefined"
              :validation-status="fieldErrors.name ? 'error' : undefined"
            >
              <div class="w-full space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="invite-name"
                >
                  {{ t('auth.name') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.name"
                  type="text"
                  class="w-full"
                  :placeholder="t('auth.namePlaceholder')"
                  :input-props="{ id: 'invite-name', autocomplete: 'name' }"
                  @update:value="clearFieldError('name')"
                />
              </div>
            </n-form-item>

            <n-form-item
              path="username"
              :show-label="false"
              first
              :feedback="fieldErrors.username || undefined"
              :validation-status="fieldErrors.username ? 'error' : undefined"
            >
              <div class="w-full space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="invite-username"
                >
                  {{ t('auth.username') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.username"
                  type="text"
                  class="w-full"
                  :placeholder="t('auth.usernamePlaceholder')"
                  :input-props="{ id: 'invite-username', autocomplete: 'username' }"
                  @update:value="clearFieldError('username')"
                />
              </div>
            </n-form-item>

            <div class="w-full space-y-3">
              <label
                class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                for="invite-email"
              >
                {{ t('auth.email') }}
              </label>
              <n-input
                :value="lookup?.email ?? ''"
                type="text"
                class="w-full"
                readonly
                :input-props="{ id: 'invite-email', autocomplete: 'email' }"
              />
              <p class="text-caption text-ink-muted">{{ t('invite.emailHint') }}</p>
            </div>

            <n-form-item
              path="password"
              :show-label="false"
              first
              :feedback="fieldErrors.password || undefined"
              :validation-status="fieldErrors.password ? 'error' : undefined"
            >
              <div class="w-full space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="invite-password"
                >
                  {{ t('auth.password') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.password"
                  type="password"
                  show-password-on="click"
                  class="w-full"
                  :placeholder="t('auth.passwordPlaceholder')"
                  :maxlength="PASSWORD_MAX_LENGTH"
                  :input-props="{ id: 'invite-password', autocomplete: 'new-password' }"
                  @update:value="clearFieldError('password')"
                />
                <p class="text-caption text-ink-muted">
                  {{
                    t('invite.passwordHint', { min: PASSWORD_MIN_LENGTH, max: PASSWORD_MAX_LENGTH })
                  }}
                </p>
              </div>
            </n-form-item>

            <InlineNotice v-if="emailTaken" tone="warning" data-testid="invite-email-taken">
              {{ t('invite.emailTaken') }}
              <RouterLink :to="loginLocation" class="font-medium underline underline-offset-2">
                {{ t('auth.login') }}
              </RouterLink>
            </InlineNotice>
            <InlineNotice v-if="tooManyRequests" tone="danger" data-testid="invite-too-many">
              {{ t('errors.tooManyRequests') }}
            </InlineNotice>
            <InlineNotice v-if="submitError" tone="danger" data-testid="invite-submit-error">
              {{ submitError }}
            </InlineNotice>

            <n-button
              type="primary"
              attr-type="submit"
              class="min-h-12 w-full!"
              :loading="isSubmitting"
              :disabled="isSubmitting"
              data-testid="invite-submit"
            >
              {{ t('invite.submit') }}
            </n-button>
          </n-form>

          <div class="flex flex-col gap-3">
            <SocialLoginDivider />
            <GoogleLoginButton :on-signed-in="onExternalSignedIn" />
            <MicrosoftLoginButton :on-signed-in="onExternalSignedIn" />
          </div>

          <RouterLink
            :to="loginLocation"
            class="text-center text-body text-accent underline-offset-2 hover:underline max-desktop:flex max-desktop:min-h-11 max-desktop:items-center max-desktop:justify-center"
            data-testid="invite-login-link"
          >
            {{ t('invite.hasAccount') }}
          </RouterLink>
        </template>
      </section>
    </main>
  </div>
</template>
