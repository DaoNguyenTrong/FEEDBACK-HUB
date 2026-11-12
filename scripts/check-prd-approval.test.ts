import { expect, test } from 'bun:test'
import { checkPrdApproval } from './check-prd-approval'

const prd = (status: string, version: string, rows: string[]) =>
  [
    '# PRD-99 — Thử',
    '',
    '| Mục | Giá trị |',
    '|---|---|',
    `| Trạng thái | ${status} |`,
    `| Phiên bản | ${version} |`,
    '',
    '## 14. Lịch sử thay đổi',
    '',
    '| Ngày | Phiên bản | Thay đổi | Nguồn |',
    '|---|---|---|---|',
    ...rows,
    '',
  ].join('\n')

const draft = '| 2026-10-20 | 1.0 | Bản nháp | — |'
const ba = (v: string) => `| 2026-10-21 | ${v} | Sửa theo review | BA review |`
const approve = (v: string) => `| 2026-10-22 | ${v} | Người dùng duyệt PRD | Quyết định của người dùng |`

test('approvedWithBaReviewThenApproval_passes', () =>
  expect(checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0')]))).toEqual([]))

test('approvedWithoutBaReview_fails', () =>
  expect(checkPrdApproval(prd('Approved', '1.0', [draft, approve('1.0')]))).toEqual([
    'no BA review before the approval of v1.0',
  ]))

test('contentAfterApproval_fails', () =>
  expect(
    checkPrdApproval(
      prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 | Thêm AC-9 | SA (thay người dùng) |']),
    ),
  ).toEqual(['content change after the approval of v1.0: Thêm AC-9']))

test('metadataAfterApproval_passes', () =>
  expect(
    checkPrdApproval(
      prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 | Điền dòng Kế hoạch | metadata |']),
    ),
  ).toEqual([]))

test('notApproved_isSkipped', () => expect(checkPrdApproval(prd('In review', '1.0', [draft]))).toEqual([]))

test('approvedWithoutApprovalRow_fails', () =>
  expect(checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0')]))).toEqual(['no user-approval row for v1.0']))

test('onlyRowsOfTheCurrentVersionCount', () =>
  expect(checkPrdApproval(prd('Approved', '1.1', [draft, ba('1.0'), approve('1.0'), approve('1.1')]))).toEqual([
    'no BA review before the approval of v1.1',
  ]))

test('lastApprovalRowIsTheOneChecked', () =>
  expect(
    checkPrdApproval(
      prd('Approved', '1.1', [approve('1.1'), ba('1.1'), '| 2026-10-23 | 1.1 | Người dùng duyệt lại | Quyết định của người dùng |']),
    ),
  ).toEqual([]))
