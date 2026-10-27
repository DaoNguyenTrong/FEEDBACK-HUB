import { appPermissionLabels } from '../permission-labels.app'
import type { Localized } from '../sidebar-types'

// [base] Names as the app spells them at permissions.<code>.name in the locales; a fork names its
// own in .vitepress/permission-labels.app.ts. tests/content-integrity.test.ts checks every name.
const basePermissionLabels: Record<string, Localized> = {
  'organizations.manage': { vi: 'Quản lý tổ chức', en: 'Manage organization' },
  'organizations.members.manage': { vi: 'Quản lý thành viên', en: 'Manage members' },
  'organizations.roles.manage': { vi: 'Quản lý vai trò', en: 'Manage roles' },
  'files.manage': { vi: 'Quản lý file', en: 'Manage files' },
  'apikeys.manage': { vi: 'Quản lý khoá API', en: 'Manage API keys' },
  'auditlogs.view': { vi: 'Xem nhật ký kiểm toán', en: 'View audit log' },
  'settings.manage': { vi: 'Quản lý cài đặt', en: 'Manage settings' },
  'feedbackhub.admin': { vi: 'Quản trị FeedbackHub', en: 'FeedbackHub administration' },
}

export const permissionLabels: Record<string, Localized> = {
  ...basePermissionLabels,
  ...appPermissionLabels,
}
