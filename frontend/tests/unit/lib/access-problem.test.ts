import { describe, expect, it } from 'vitest'
import { ApiError } from '@/api/types'
import { accessProblemOf } from '@/lib/access-problem'

function problem(status: number, code?: string) {
  return new ApiError({
    status,
    title: 'Sao Mai Tech',
    detail: 'Role Trưởng IT',
    code,
  })
}

describe('accessProblemOf', () => {
  it.each([
    [404, undefined, 'not-found'],
    [403, 'OrganizationAccessDenied', 'forbidden'],
    [403, 'SystemAdministratorRequired', 'forbidden'],
    [403, 'OrganizationSuspended', null],
    [403, undefined, null],
    [401, 'OrganizationAccessDenied', null],
    [500, undefined, null],
  ])('maps %s %s to %s', (status, code, expected) => {
    expect(accessProblemOf(problem(status, code))).toBe(expected)
  })

  it('returns null for an error that is not an ApiError', () => {
    expect(accessProblemOf(new Error('boom'))).toBeNull()
    expect(accessProblemOf({ status: 404 })).toBeNull()
    expect(accessProblemOf(undefined)).toBeNull()
  })

  it('returns only the kind, never anything from the problem', () => {
    expect(accessProblemOf(problem(404))).toBe('not-found')
    expect(accessProblemOf(problem(403, 'OrganizationAccessDenied'))).toBe('forbidden')
  })
})
