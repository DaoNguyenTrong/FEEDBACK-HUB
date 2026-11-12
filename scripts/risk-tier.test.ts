import { expect, test } from 'bun:test'
import { tierOf, tierOfChange, type Tier } from './risk-tier'

test.each<[string, Tier]>([
  ['docs/process/lifecycle.md', 0],
  ['frontend/src/locales/vi.ts', 0],
  ['shared/openapi/openapi.json', 0],
  ['frontend/src/api/generated/products.ts', 0],
  ['.claude/agents/implementer.md', 0],
  ['frontend/tests/unit/views/products.test.ts', 0],
  ['frontend/src/views/products/ProductList.vue', 1],
  ['backend/src/FeedbackHub.Application/Services/Products/ProductService.cs', 1],
  ['scripts/check-comments.ts', 1],
  ['backend/src/FeedbackHub.Application/Services/Auth/TokenIssuer.cs', 2],
  ['backend/src/FeedbackHub.API/Common/AuthCookies.cs', 2],
  ['backend/src/FeedbackHub.API/Middleware/TenantAccessMiddleware.cs', 2],
  ['backend/src/FeedbackHub.Infrastructure/Persistence/Migrations/20261112_X.cs', 2],
  ['backend/src/FeedbackHub.API/Program.cs', 2],
  ['frontend/src/api/client.ts', 2],
  ['frontend/src/router/index.ts', 2],
  ['.github/workflows/ci.yml', 2],
  ['lefthook.yml', 2],
  ['scripts/risk-tier.ts', 2],
])('tierOf(%s) = %d', (path, tier) => expect(tierOf(path)).toBe(tier))

test('tierOf_authDirectoryWithoutKeywordInName_isTier2', () =>
  expect(tierOf('backend/src/FeedbackHub.Application/Services/Auth/LoginFlow.cs')).toBe(2))

test('tierOf_testUnderSensitivePath_isTier1', () =>
  expect(tierOf('backend/tests/FeedbackHub.Application.Tests/Services/Auth/AuthServiceTests.cs')).toBe(1))

test('tierOf_lowercaseKeyword_isTier2', () => expect(tierOf('frontend/src/stores/auth.ts')).toBe(2))

test('tierOf_tierScriptTest_isTier1', () => expect(tierOf('scripts/risk-tier.test.ts')).toBe(1))

test('tierOfChange_takesTheMaxAndNamesTheReason', () => {
  const r = tierOfChange([
    { path: 'docs/a.md', changedLines: [] },
    { path: 'frontend/src/api/client.ts', changedLines: [] },
  ])
  expect(r.tier).toBe(2)
  expect(r.reasons[0]).toStartWith('frontend/src/api/client.ts')
})

test('tierOfChange_allowAnonymousAddedInTier1File_isTier2', () =>
  expect(
    tierOfChange([
      {
        path: 'backend/src/FeedbackHub.API/Controllers/ProductsController.cs',
        changedLines: ['    [AllowAnonymous]'],
      },
    ]).tier,
  ).toBe(2))

test('tierOfChange_markerInDoc_staysTier0', () =>
  expect(
    tierOfChange([{ path: 'docs/tech/01.md', changedLines: ['Dùng [Authorize] cho mọi action'] }]).tier,
  ).toBe(0))

test('tierOfChange_markerInTest_staysAtItsPathTier', () =>
  expect(
    tierOfChange([{ path: 'frontend/tests/unit/x.test.ts', changedLines: ['withCredentials: true'] }]).tier,
  ).toBe(0))

test('tierOfChange_empty_isTier0', () => expect(tierOfChange([]).tier).toBe(0))

test('tierOfChange_reasonsCappedAtFive', () => {
  const files = Array.from({ length: 8 }, (_, i) => ({ path: `frontend/src/router/r${i}.ts`, changedLines: [] }))
  expect(tierOfChange(files).reasons).toHaveLength(5)
})
