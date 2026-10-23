// Hand-kept in sync with backend/src/FeedbackHub.Application/Common/Authorization/Permissions.cs
// — plain string constants have no codegen path from the OpenAPI spec.
export const Permissions = {
  OrganizationManage: 'organizations.manage',
  OrganizationMembersManage: 'organizations.members.manage',
  OrganizationRolesManage: 'organizations.roles.manage',
  FilesManage: 'files.manage',
  ApiKeysManage: 'apikeys.manage',
  AuditLogsView: 'auditlogs.view',
  SettingsManage: 'settings.manage',
  FeedbackHubAdmin: 'feedbackhub.admin',
} as const
