import axios from 'axios'
import { AxiosHeaders } from 'axios'
import type { AxiosRequestHeaders } from 'axios'

import type { ProblemDetails } from './types'
import { getRuntimeConfig } from '@/lib/runtime-config'

declare module 'axios' {
  interface InternalAxiosRequestConfig {
    _retry?: boolean
  }
}

type FailedQueueItem = {
  resolve: () => void
  reject: (reason?: unknown) => void
  // This request's own error, for when the refresh leaves the organization instead of replaying it.
  original: unknown
}

type OrganizationAccessHandler = (problem: ProblemDetails, url: string) => void

let organizationAccessHandler: OrganizationAccessHandler | null = null

export function setOrganizationAccessHandler(handler: OrganizationAccessHandler | null) {
  organizationAccessHandler = handler
}

const ORGANIZATION_ACCESS_CODES = new Set(['OrganizationSuspended', 'OrganizationAccessDenied'])
// Calls that don't run through the organization gate, so these codes can't describe the working one.
const ORGANIZATION_ACCESS_EXEMPT = [
  '/api/auth',
  '/api/organizations',
  '/api/invitations',
  '/api/system',
  '/api/permissions',
]

function isOrganizationGated(url: string) {
  const path = url.split('?')[0] ?? url
  return !ORGANIZATION_ACCESS_EXEMPT.some(
    (prefix) => path === prefix || path.startsWith(`${prefix}/`),
  )
}

let isRefreshing = false
let failedQueue: FailedQueueItem[] = []

function toProblemDetails(
  data: unknown,
  fallbackStatus: number,
  fallbackTitle: string,
): ProblemDetails | null {
  if (!data || typeof data !== 'object') {
    return null
  }

  const { detail, errors, status, title, type, code, organizationName } =
    data as Partial<ProblemDetails>

  if (typeof status !== 'number') {
    return null
  }

  return {
    status,
    title: typeof title === 'string' && title.length > 0 ? title : fallbackTitle,
    detail: typeof detail === 'string' && detail.length > 0 ? detail : undefined,
    type: typeof type === 'string' ? type : undefined,
    code: typeof code === 'string' ? code : undefined,
    organizationName: typeof organizationName === 'string' ? organizationName : undefined,
    errors: errors && typeof errors === 'object' ? errors : undefined,
  }
}

function rejectFailedQueueWithOriginals() {
  failedQueue.forEach(({ reject, original }) => reject(original))
  failedQueue = []
}

function processFailedQueue(error: unknown) {
  failedQueue.forEach(({ reject, resolve }) => {
    if (error) {
      reject(error)
      return
    }

    resolve()
  })

  failedQueue = []
}

function toApiError(error: unknown): ApiError | null {
  if (!axios.isAxiosError(error) || !error.response) {
    return null
  }

  const contentType = error.response.headers['content-type']
  const hasProblemContentType =
    typeof contentType === 'string' && contentType.includes('application/problem+json')
  const problem = toProblemDetails(
    error.response.data,
    error.response.status,
    error.response.statusText || i18n.global.t('errors.requestFailed'),
  )

  if (!hasProblemContentType && !problem) {
    return null
  }

  return new ApiError(
    problem ?? {
      status: error.response.status,
      title: error.response.statusText || i18n.global.t('errors.requestFailed'),
    },
  )
}

function applyCommonHeaders(config: { headers?: AxiosRequestHeaders; url?: string }) {
  const headers = AxiosHeaders.from(config.headers)
  const localeSource = i18n.global.locale as string | { value: string }
  const locale = typeof localeSource === 'string' ? localeSource : localeSource.value

  if (config.url !== '/api/health') {
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone
    headers.set('X-TimeZone', timeZone === 'Asia/Saigon' ? 'Asia/Ho_Chi_Minh' : timeZone)
  }

  headers.set('Accept-Language', locale)

  // A cross-site <form> submission can't set a custom header, so a forged request never carries
  // this — the value is unchecked by the backend, only presence matters. See CsrfProtectionMiddleware.
  headers.set('X-CSRF-Protection', '1')

  return headers
}

// Authenticated calls under /api/auth/, not sign-in flows: an expired access token answers them
// with a bodyless 401, which must go through refresh-and-retry like any other call.
const AUTHENTICATED_AUTH_URLS = new Set(['/api/auth/switch-organization', '/api/auth/sessions'])
const AUTHENTICATED_AUTH_PREFIXES = ['/api/auth/sessions/']

function isAuthFlowRequest(url?: string) {
  if (!url) return false
  const path = url.split('?')[0] ?? url
  if (
    AUTHENTICATED_AUTH_URLS.has(path) ||
    AUTHENTICATED_AUTH_PREFIXES.some((prefix) => path.startsWith(prefix))
  ) {
    return false
  }
  return url.startsWith('/api/auth/')
}

function isPasswordChangeRequest(url?: string) {
  return url === '/api/profile/password'
}

// Form-level 401s from EnsureCurrentPasswordAsync (wrong current password, or its attempt lock):
// they are about the form, not an expired session. Refreshing and replaying would spend another
// attempt, and the replayed 401 would then sign the user out.
const FORM_LEVEL_401_CODES = new Set([
  'InvalidCurrentPassword',
  'EmailChangeTemporarilyLocked',
  'PasswordChangeTemporarilyLocked',
])

function isFormLevelRejection(data: unknown) {
  const code = (data as { code?: unknown } | undefined)?.code
  return typeof code === 'string' && FORM_LEVEL_401_CODES.has(code)
}

/**
 * Array params as repeated keys (`ids=3&ids=4`), not axios's default `ids[]=3`: ASP.NET binds the
 * bracketed form to an empty array and silently answers 200 with unfiltered data.
 */
const REPEATED_KEY_ARRAY_PARAMS = { indexes: null } as const

export const apiClient = axios.create({
  // Build-time value at module load; main.ts re-applies getRuntimeConfig() once config.json is in.
  baseURL: getRuntimeConfig().apiBaseUrl,
  withCredentials: true,
  timeout: 300_000,
  paramsSerializer: REPEATED_KEY_ARRAY_PARAMS,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const refreshClient = axios.create({
  // Build-time value at module load; main.ts re-applies getRuntimeConfig() once config.json is in.
  baseURL: getRuntimeConfig().apiBaseUrl,
  withCredentials: true,
  timeout: 300_000,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use((config) => {
  const headers = applyCommonHeaders(config)

  config.headers = headers
  return config
})

refreshClient.interceptors.request.use((config) => {
  config.headers = applyCommonHeaders(config)
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (!axios.isAxiosError(error) || !error.response) {
      return Promise.reject(error)
    }

    const auth = useAuthStore()
    const originalRequest = error.config

    if (
      error.response.status === 401 &&
      originalRequest &&
      !isAuthFlowRequest(originalRequest.url) &&
      !isPasswordChangeRequest(originalRequest.url) &&
      !isFormLevelRejection(error.response.data)
    ) {
      if (originalRequest._retry) {
        auth.clearAuth()
        return Promise.reject(toApiError(error) ?? error)
      }

      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({
            resolve: () => resolve(apiClient(originalRequest)),
            reject,
            original: toApiError(error) ?? error,
          })
        })
      }

      originalRequest._retry = true
      isRefreshing = true
      const previousOrganizationId = auth.organizationId
      const originalError = toApiError(error) ?? error

      return auth
        .refreshToken()
        .then(() => {
          if (previousOrganizationId !== null && auth.organizationId === null) {
            // The requests were sent for an organization the refresh does not grant any more (TECH00 6.5).
            rejectFailedQueueWithOriginals()
            void useOrganizationContextStore()
              .handleOrganizationLost(previousOrganizationId)
              .catch(() => undefined)
            return Promise.reject(originalError)
          }
          processFailedQueue(null)
          return apiClient(originalRequest)
        })
        .catch((refreshError) => {
          if (refreshError === originalError) {
            return Promise.reject(originalError)
          }
          const apiError = toApiError(refreshError) ?? refreshError
          clearKeepLoginPreference()
          auth.clearAuth()
          processFailedQueue(apiError)
          return Promise.reject(apiError)
        })
        .finally(() => {
          isRefreshing = false
        })
    }

    const apiError = toApiError(error)

    if (
      apiError &&
      apiError.status === 403 &&
      organizationAccessHandler &&
      ORGANIZATION_ACCESS_CODES.has(apiError.problem.code ?? '') &&
      originalRequest?.url &&
      isOrganizationGated(originalRequest.url)
    ) {
      organizationAccessHandler(apiError.problem, originalRequest.url)
    }

    if (apiError) {
      return Promise.reject(apiError)
    }

    return Promise.reject(error)
  },
)
