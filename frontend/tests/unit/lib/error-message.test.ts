import { describe, expect, it } from 'vitest'
import { ApiError } from '@/api/types'
import { errorMessage } from '@/lib/error-message'

const t = (key: string) => `t:${key}`
const te = (key: string) => key === 'errorCodes.KnownCode'

function apiError(problem: { code?: string; detail?: string }) {
  return new ApiError({ status: 400, title: 'Title', ...problem })
}

describe('errorMessage', () => {
  it('maps a known error code to its locale text', () => {
    expect(errorMessage(apiError({ code: 'KnownCode' }), t, te)).toBe('t:errorCodes.KnownCode')
  })

  it('returns the mapped text even when a detail is present', () => {
    const error = apiError({ code: 'KnownCode', detail: 'Sao Mai Tech' })
    expect(errorMessage(error, t, te)).toBe('t:errorCodes.KnownCode')
  })

  it('returns the detail for an unmapped code', () => {
    expect(errorMessage(apiError({ code: 'Other', detail: 'Plain detail' }), t, te)).toBe(
      'Plain detail',
    )
  })

  it('returns the detail when the error has no code', () => {
    expect(errorMessage(apiError({ detail: 'Plain detail' }), t, te)).toBe('Plain detail')
  })

  it('falls back to the generic text for a non-ApiError and for an empty detail', () => {
    expect(errorMessage(new Error('boom'), t, te)).toBe('t:errors.requestFailed')
    expect(errorMessage('boom', t, te)).toBe('t:errors.requestFailed')
    expect(errorMessage(apiError({ code: 'Other', detail: '' }), t, te)).toBe(
      't:errors.requestFailed',
    )
  })
})
