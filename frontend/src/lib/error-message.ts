import { ApiError } from '@/api/types'

type Translate = (key: string, values?: Record<string, unknown>) => string

export function errorMessage(error: unknown, t: Translate, te: (key: string) => boolean): string {
  if (error instanceof ApiError) {
    const { code, detail } = error.problem
    if (code && te(`errorCodes.${code}`)) return t(`errorCodes.${code}`)
    if (detail) return detail
  }
  return t('errors.requestFailed')
}
