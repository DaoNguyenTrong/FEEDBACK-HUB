import { ApiError } from '@/api/types'

export type AccessProblem = 'not-found' | 'forbidden'

const FORBIDDEN_CODES = new Set(['OrganizationAccessDenied', 'SystemAdministratorRequired'])

// Returns only the literal kind: a screen must never learn the object's name or the error's detail
// from here (BR-45). 403 `OrganizationSuspended` is not an access problem; the §6.5 flow handles it.
export function accessProblemOf(error: unknown): AccessProblem | null {
  if (!(error instanceof ApiError)) {
    return null
  }

  if (error.status === 404) {
    return 'not-found'
  }

  if (error.status === 403 && error.problem.code && FORBIDDEN_CODES.has(error.problem.code)) {
    return 'forbidden'
  }

  return null
}
