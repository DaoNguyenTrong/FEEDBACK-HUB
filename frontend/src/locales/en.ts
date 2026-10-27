interface DeactivateBullets {
  widget: string
  access: string
  data: string
}

export interface LocaleSchema {
  common: {
    confirm: string
    cancel: string
    save: string
    close: string
    loading: string
    search: string
    goHome: string
    notFoundReloaded: string
    showMore: string
    language: string
    theme: string
    lightMode: string
    darkMode: string
    fontSize: string
    decreaseFontSize: string
    increaseFontSize: string
    resetFontSize: string
    sortBy: string
    sortDefault: string
    sortAscending: string
    sortDescending: string
    yes: string
    no: string
    user: string
  }
  layout: {
    userMenu: {
      open: string
      appearance: string
      vietnamese: string
      english: string
    }
    settings: {
      title: string
      colorMode: string
      light: string
      dark: string
      system: string
    }
    palette: {
      trigger: string
      placeholder: string
      empty: string
      pages: string
      actions: string
      switchLanguage: string
    }
  }
  markdownEditor: {
    split: string
    editor: string
    preview: string
    emptyPreview: string
  }
  errorCodes: Record<string, string>
  accessProblem: {
    notFoundTitle: string
    notFoundText: string
    forbiddenTitle: string
    forbiddenText: string
    goHome: string
  }
  errors: {
    requestFailed: string
    networkError: string
    unauthorized: string
    forbidden: string
    serverError: string
    serverErrorTitle: string
    serverErrorDescription: string
    serverErrorRetry: string
    tooManyRequests: string
  }
  auth: {
    login: string
    logout: string
    register: string
    username: string
    password: string
    email: string
    name: string
    namePlaceholder: string
    loginTitle: string
    loginHint: string
    registerTitle: string
    loginButton: string
    registerButton: string
    keepLogin: string
    keepLoginHint: string
    usernameRequired: string
    passwordRequired: string
    invalidCredentials: string
    accountTemporarilyLocked: string
    loginFailed: string
    logoutConfirmMessage: string
    logoutFailed: string
    usernamePlaceholder: string
    passwordPlaceholder: string
    emailNotConfirmed: string
    emailRequired: string
    emailPlaceholder: string
    resendVerification: string
    resendVerificationSuccess: string
    resendVerificationFailed: string
    nameRequired: string
    emailInvalid: string
    passwordTooShort: string
    usernameAlreadyExists: string
    emailAlreadyExists: string
    registerFailed: string
    registerSuccessTitle: string
    registerSuccessMessage: string
    backToLogin: string
    verifyingEmail: string
    verifyEmailInvalidToken: string
    verifyEmailMissingToken: string
    verifyEmailFailed: string
    orContinueWith: string
    continueWithGoogle: string
    googleEmailNotConfirmed: string
    googleEmailNotVerifiedByProvider: string
    googleLoginFailed: string
    continueWithMicrosoft: string
    microsoftEmailNotConfirmed: string
    microsoftEmailNotVerifiedByProvider: string
    microsoftLoginFailed: string
    registerLink: string
  }
  app: {
    name: string
    version: string
    copyright: string
    apiVersion: string
    apiBuildTime: string
    apiStatus: {
      checking: string
      online: string
      offline: string
      error: string
    }
    welcomeTitle: string
    welcomeDescription: string
  }
  nav: {
    home: string
    organizations: string
    organizationSettings: string
    members: string
    invitations: string
    roles: string
    profile: string
    openMenu: string
    groups: {
      organization: string
      system: string
    }
    userGuide: string
  }
  invitations: {
    title: string
    inviteMember: string
    inviteShort: string
    email: string
    emailHint: string
    role: string
    expiry: string
    send: string
    sending: string
    sent: string
    resentToast: string
    revoke: string
    revokeConfirmMessage: string
    revokeConfirmDetail: string
    delete: string
    resend: string
    resending: string
    pending: string
    expired: string
    expiredLabel: string
    lockReasonOwnerOnly: string
    lockReasonInsufficientRole: string
    emptyTitle: string
    emptyDescription: string
    showingOf: string
  }
  members: {
    self: string
    actions: string
    actionsFor: string
    changeRoles: string
    leave: string
    remove: string
    atLeastOneRole: string
    rolesChanged: string
    removed: string
    removeConfirmMessage: string
    removeConfirmDetail: string
    leaveConfirmMessage: string
    leaveConfirmDetail: string
    showingOf: string
    lockReasonOwnerOnly: string
    lockReasonInsufficientRole: string
    lockReasonInsufficientRoleGeneric: string
    ownerOnlyRoleHint: string
    insufficientRoleHint: string
  }
  guideAuth: {
    opening: string
    failed: string
  }
  profile: {
    title: string
    updateSuccess: string
    passwordChanged: string
    currentPassword: string
    newPassword: string
    changePassword: string
    passwordTab: string
    personalInfo: string
    sessionsTabShort: string
    avatarChange: string
    avatarRemove: string
    avatarUpdated: string
    avatarRemoved: string
    avatarTooLarge: string
    avatarInvalidType: string
    avatarAlt: string
    currentPasswordPlaceholder: string
    newPasswordPlaceholder: string
    currentPasswordRequired: string
    invalidCurrentPassword: string
    newPasswordRequired: string
    newPasswordTooShort: string
    passwordChangeTemporarilyLocked: string
    pendingEmail: string
    resendEmailChange: string
    cancelEmailChange: string
    emailChangeResent: string
    emailChangeTemporarilyLocked: string
    name: string
    namePlaceholder: string
    nameRequired: string
    email: string
    emailPlaceholder: string
    emailRequired: string
    emailInvalid: string
    phone: string
    phonePlaceholder: string
    position: string
    positionPlaceholder: string
    address: string
    addressPlaceholder: string
    sessionsTitle: string
    sessionsEmpty: string
    currentDevice: string
    rememberLogin: string
    lastActive: string
    sessionLoginAt: string
    unknownDevice: string
    unknownIp: string
    signOutDevice: string
    signOutOtherDevices: string
    signOutDeviceConfirm: string
    signOutOtherDevicesConfirm: string
    deviceSignedOut: string
    otherDevicesSignedOut: string
  }
  confirmEmailChange: {
    confirming: string
    success: string
    successBody: string
    successBodyNoEmail: string
    noLongerValid: string
    alreadyTaken: string
    missingToken: string
    failed: string
    backToApp: string
  }
  roles: {
    systemKind: { Owner: string; Admin: string; Member: string }
    create: string
    createShort: string
    edit: string
    delete: string
    actionsFor: string
    allPermissions: string
    noPermissions: string
    holders: string
    viewOnly: string
    viewOnlyMissingPermissions: string
    deleteBlockedHasHolders: string
    deleteConfirmMessage: string
    deleteConfirmDetail: string
    nameLabel: string
    nameRequired: string
    permissionsLabel: string
    grantableOnly: string
    created: string
    saved: string
    deleted: string
  }
  permissions: {
    organizations_manage: { name: string; description: string }
    organizations_members_manage: { name: string; description: string }
    organizations_roles_manage: { name: string; description: string }
    apikeys_manage: { name: string; description: string }
    auditlogs_view: { name: string; description: string }
    files_manage: { name: string; description: string }
    settings_manage: { name: string; description: string }
    feedbackhub_admin: { name: string; description: string }
  }
  welcome: {
    greeting: string
    choose: string
    noOrganizations: string
    createCard: string
    createHint: string
    allSuspended: string
    justSuspended: string
    systemOrganizations: string
  }
  invite: {
    title: string
    summary: string
    emailHint: string
    passwordHint: string
    submit: string
    hasAccount: string
    emailTaken: string
    accept: string
    loadFailed: string
    wrongAccount: { title: string; signOut: string }
    emailNotConfirmed: { title: string; text: string }
    expired: { title: string; text: string }
    noLongerValid: { title: string; text: string }
    suspended: { title: string; text: string }
  }
  organizations: {
    switcherTitle: string
    suspended: string
    backToWelcome: string
    create: string
    personalWorkspace: string
    createTitle: string
    name: string
    nameRequired: string
    resendVerification: string
    slug: string
    slugHint: string
  }
  organizationSettings: {
    title: string
    infoTitle: string
    name: string
    slug: string
    slugFixed: string
    save: string
    renamed: string
    deactivateTitle: string
    deactivateText: string
    deactivateAction: string
  }
  deactivateOrganization: {
    title: string
    intro: string
    reason: string
    optional: string
    count: string
    submit: string
    member: DeactivateBullets
    systemMember: DeactivateBullets
    system: DeactivateBullets
  }
  notifications: {
    title: string
    empty: string
    markAllRead: string
    markAsRead: string
    OrganizationMemberAdded: string
  }
  pushNotifications: {
    title: string
    enable: string
    description: string
    notSupported: string
    permissionDenied: string
  }
}

const en: LocaleSchema = {
  common: {
    confirm: 'Confirm',
    cancel: 'Cancel',
    save: 'Save',
    close: 'Close',
    loading: 'Loading...',
    search: 'Search',
    goHome: 'Go to workspace',
    notFoundReloaded: 'This item no longer exists. The list has been reloaded.',
    showMore: 'Show more',
    language: 'Language',
    theme: 'Theme',
    lightMode: 'Light',
    darkMode: 'Dark',
    fontSize: 'Font size',
    decreaseFontSize: 'Decrease font size',
    increaseFontSize: 'Increase font size',
    resetFontSize: 'Reset font size',
    sortBy: 'Sort by',
    sortDefault: 'Default',
    sortAscending: 'Ascending',
    sortDescending: 'Descending',
    yes: 'Yes',
    no: 'No',
    user: 'User',
  },
  layout: {
    userMenu: {
      open: 'Account',
      appearance: 'Appearance',
      vietnamese: 'Tiếng Việt',
      english: 'English',
    },
    settings: {
      title: 'Appearance settings',
      colorMode: 'Color mode',
      light: 'Light',
      dark: 'Dark',
      system: 'System',
    },
    palette: {
      trigger: 'Go to…',
      placeholder: 'Type a page or action',
      empty: 'No results',
      pages: 'Pages',
      actions: 'Actions',
      switchLanguage: 'Switch language',
    },
  },
  markdownEditor: {
    split: 'Split',
    editor: 'Editor',
    preview: 'Preview',
    emptyPreview: 'Nothing to preview yet',
  },
  errorCodes: {
    OrganizationSlugInvalid:
      'Use only a–z, 0–9 and "-", start and end with a letter or digit, no two "-" in a row, 3 to 50 characters.',
    OrganizationSlugAlreadyExists: 'This identifier is already taken.',
    EmailNotConfirmed: 'Verify your email before doing this.',
    OrganizationNameRequired: 'Enter the organization name.',
    DeactivationReasonRequired: 'Enter a reason.',
    OwnerOnlyOperation: 'Only an Owner can do this.',
    InsufficientRolePermissions: "You don't have every permission of this role.",
    OrganizationCannotRemoveLastOwner: 'The organization needs at least one owner.',
    OrganizationMemberRequiresAtLeastOneRole: 'Every member needs at least one role.',
    CannotRemoveSelf: "You can't remove yourself. Use Leave organization.",
    LastOwnerCannotLeave:
      'You are the only owner. Give the owner role to someone else before you leave.',
    InvitationEmailAlreadyMember: 'This person is already a member of {org}.',
    FieldEmailAddress: 'Enter an email like name@company.com.',
    InvitationEmailDeliveryFailed: "The invitation wasn't sent. Please try again.",
    OrganizationRoleNameAlreadyExists: 'That name is already used in this organization.',
    RoleNameReservedForSystemRole: 'That name belongs to a built-in role.',
    OrganizationSystemRoleImmutable: "Built-in roles can't be edited or deleted.",
    UnknownPermissionCode: 'Unknown permission.',
    RoleHasHolders: 'This role is still held by members.',
    FieldMaxLength: 'The role name can be at most 100 characters.',
    InvitationExpired: 'This invitation has expired',
    InvitationNoLongerValid: 'This invitation can no longer be used',
    InvitationWrongAccount: "This invitation isn't for the account you are using",
    InvitationOrganizationSuspended: 'The organization is suspended',
  },
  accessProblem: {
    notFoundTitle: 'Not found',
    notFoundText: "This content doesn't exist or you don't have access to it.",
    forbiddenTitle: 'No permission',
    forbiddenText: "Your account doesn't have permission to open this page.",
    goHome: 'Go to home',
  },
  errors: {
    requestFailed: 'Request failed',
    networkError: 'Network error',
    unauthorized: 'Unauthorized',
    forbidden: 'Forbidden',
    serverError: 'Server error',
    serverErrorTitle: 'We can’t reach the server',
    serverErrorDescription:
      'The service is temporarily unavailable or unreachable. It usually recovers on its own — try again in a moment.',
    serverErrorRetry: 'Try again',
    tooManyRequests: 'Too many attempts. Wait a moment and try again.',
  },
  auth: {
    login: 'Login',
    logout: 'Logout',
    register: 'Register',
    username: 'Username',
    password: 'Password',
    email: 'Email',
    name: 'Name',
    namePlaceholder: 'Enter your full name',
    loginTitle: 'Sign in',
    loginHint: 'Use your assigned username and password to continue.',
    registerTitle: 'Create an account',
    loginButton: 'Sign in',
    registerButton: 'Create account',
    keepLogin: 'Keep me signed in',
    keepLoginHint: 'Restore your session automatically the next time you open the app.',
    usernameRequired: 'Username is required.',
    passwordRequired: 'Password is required.',
    invalidCredentials: 'Your username or password is incorrect.',
    accountTemporarilyLocked:
      'This account is temporarily locked after too many failed sign-in attempts. Please try again in a few minutes.',
    loginFailed: 'Unable to sign in right now. Please try again.',
    logoutConfirmMessage: 'Do you want to sign out from this session?',
    logoutFailed:
      'Unable to notify the server before signing out. Your local session has been cleared.',
    usernamePlaceholder: 'Enter your username',
    passwordPlaceholder: 'Enter your password',
    emailNotConfirmed: 'Please verify your email address before signing in.',
    emailRequired: 'Email is required.',
    emailPlaceholder: 'Enter your email address',
    resendVerification: 'Resend verification email',
    resendVerificationSuccess:
      'If an account exists for this email, a verification link has been sent.',
    resendVerificationFailed:
      'Unable to resend the verification email right now. Please try again.',
    nameRequired: 'Name is required.',
    emailInvalid: 'Enter a valid email address.',
    passwordTooShort: 'Password must be at least {min} characters.',
    usernameAlreadyExists: 'This username is already taken.',
    emailAlreadyExists: 'An account with this email already exists.',
    registerFailed: 'Unable to create your account right now. Please try again.',
    registerSuccessTitle: 'Check your email',
    registerSuccessMessage:
      "We've sent a verification link to {email}. Verify your email to sign in.",
    backToLogin: 'Already have an account? Sign in',
    verifyingEmail: 'Verifying your email…',
    verifyEmailInvalidToken: 'This verification link is invalid or has expired.',
    verifyEmailMissingToken: 'This verification link is missing or malformed.',
    verifyEmailFailed: 'Unable to verify your email right now. Please try again.',
    orContinueWith: 'Or continue with',
    continueWithGoogle: 'Sign in with Google',
    googleEmailNotConfirmed:
      'An account with this email already exists but has not been verified yet. Verify it below to link Google sign-in.',
    googleEmailNotVerifiedByProvider:
      "Google hasn't verified this email address. Please verify it with Google first.",
    googleLoginFailed: 'Unable to sign in with Google right now. Please try again.',
    continueWithMicrosoft: 'Sign in with Microsoft',
    microsoftEmailNotConfirmed:
      'An account with this email already exists but has not been verified yet. Verify it below to link Microsoft sign-in.',
    microsoftEmailNotVerifiedByProvider:
      "Microsoft hasn't verified this account's email. Please sign up with your email instead.",
    microsoftLoginFailed: 'Unable to sign in with Microsoft right now. Please try again.',
    registerLink: "Don't have an account? Create one",
  },
  app: {
    name: 'FeedbackHub',
    version: 'Version {version}',
    copyright: '© 2026 App Starter',
    apiVersion: 'API v{version}',
    apiBuildTime: 'Build {time}',
    apiStatus: {
      checking: 'Checking API…',
      online: 'Connected',
      offline: 'API offline',
      error: 'API unavailable',
    },
    welcomeTitle: 'Welcome back',
    welcomeDescription: 'This is your starting point — build your first feature from here.',
  },
  nav: {
    home: 'Home',
    organizations: 'Organizations',
    organizationSettings: 'Organization management',
    members: 'Members',
    invitations: 'Invitations',
    roles: 'Roles',
    profile: 'Profile',
    openMenu: 'Open menu',
    groups: {
      organization: 'Organization',
      system: 'System',
    },
    userGuide: 'User guide',
  },
  guideAuth: {
    opening: 'Opening the user guide…',
    failed: "Couldn't open the user guide. Please try again later.",
  },
  profile: {
    title: 'Profile',
    updateSuccess: 'Profile updated',
    passwordChanged: 'Password changed',
    currentPassword: 'Current password',
    newPassword: 'New password',
    changePassword: 'Change password',
    passwordTab: 'Password',
    personalInfo: 'Profile',
    sessionsTabShort: 'Sessions',
    avatarChange: 'Change photo',
    avatarRemove: 'Remove photo',
    avatarUpdated: 'Profile photo updated',
    avatarRemoved: 'Profile photo removed',
    avatarTooLarge: 'The photo must not exceed {max} MB.',
    avatarInvalidType: 'Choose a PNG, JPEG or WebP image.',
    avatarAlt: 'Profile photo',
    currentPasswordPlaceholder: 'Enter your current password',
    newPasswordPlaceholder: 'Enter a new password',
    currentPasswordRequired: 'Current password is required.',
    invalidCurrentPassword: 'The current password is incorrect.',
    newPasswordRequired: 'New password is required.',
    newPasswordTooShort: 'New password must be at least {min} characters.',
    passwordChangeTemporarilyLocked:
      'Changing the password is temporarily locked after too many wrong current passwords. Please try again in a few minutes.',
    pendingEmail: 'Waiting for confirmation: {email}. Open the link in the email to switch.',
    resendEmailChange: 'Resend',
    cancelEmailChange: 'Cancel',
    emailChangeResent: 'Confirmation email sent again.',
    emailChangeTemporarilyLocked: 'Email change is temporarily locked. Try again later.',
    name: 'Name',
    namePlaceholder: 'Enter the full name',
    nameRequired: 'Name is required.',
    email: 'Email',
    emailPlaceholder: 'Enter the email address',
    emailRequired: 'Email is required.',
    emailInvalid: 'Enter a valid email address.',
    phone: 'Phone',
    phonePlaceholder: 'Enter the phone number',
    position: 'Position',
    positionPlaceholder: 'Enter the position',
    address: 'Address',
    addressPlaceholder: 'Enter the address',
    sessionsTitle: 'Active sessions',
    sessionsEmpty: 'No active sessions.',
    currentDevice: 'This device',
    rememberLogin: 'Remember login',
    lastActive: 'Last active',
    sessionLoginAt: 'Signed in',
    unknownDevice: 'Unknown device',
    unknownIp: 'Unknown IP',
    signOutDevice: 'Sign out',
    signOutOtherDevices: 'Sign out of other devices',
    signOutDeviceConfirm: 'Sign out of this device?',
    signOutOtherDevicesConfirm: 'This will sign you out of every other device. Continue?',
    deviceSignedOut: 'Device signed out',
    otherDevicesSignedOut: 'Signed out of other devices',
  },
  confirmEmailChange: {
    confirming: 'Confirming your new email…',
    success: 'Email changed',
    successBody: 'This account now uses {email} to sign in and receive mail.',
    successBodyNoEmail: 'Your account email has been changed successfully.',
    noLongerValid: 'This email change link is no longer valid.',
    alreadyTaken: 'An account with this email already exists.',
    missingToken: 'This email change link is missing or malformed.',
    failed: 'Unable to confirm the email change right now. Please try again.',
    backToApp: 'Open FeedbackHub',
  },
  roles: {
    systemKind: { Owner: 'Owner', Admin: 'Admin', Member: 'Member' },
    create: 'Create role',
    createShort: 'Create',
    edit: 'Edit role',
    delete: 'Delete role',
    actionsFor: 'Actions for role {name}',
    allPermissions: 'All permissions',
    noPermissions: 'No permissions',
    holders: '{n} person | {n} people',
    viewOnly: 'View only',
    viewOnlyMissingPermissions:
      "You don't have every permission in this role, so you can only view it.",
    deleteBlockedHasHolders:
      'This role is still held by {n} person. Change their role or remove them from the organization first. | This role is still held by {n} people. Change their roles or remove them from the organization first.',
    deleteConfirmMessage: 'Delete role {name}?',
    deleteConfirmDetail:
      '{n} pending invitation with this role will no longer work. | {n} pending invitations with this role will no longer work.',
    nameLabel: 'Role name',
    nameRequired: 'Enter a role name.',
    permissionsLabel: 'Permissions',
    grantableOnly: 'You can only pick permissions you have.',
    created: 'Created role {name}',
    saved: 'Saved role {name}',
    deleted: 'Deleted role {name}',
  },
  permissions: {
    organizations_manage: {
      name: 'Manage organization',
      description: 'Rename and deactivate the organization.',
    },
    organizations_members_manage: {
      name: 'Manage members',
      description: 'Invite and remove members and change their roles.',
    },
    organizations_roles_manage: {
      name: 'Manage roles',
      description: 'Create, edit and delete custom roles.',
    },
    apikeys_manage: {
      name: 'Manage API keys',
      description: "Create and revoke the organization's API keys.",
    },
    auditlogs_view: {
      name: 'View audit log',
      description: 'See who did what in the organization.',
    },
    files_manage: {
      name: 'Manage files',
      description: 'Manage the files uploaded to the organization.',
    },
    settings_manage: {
      name: 'Manage settings',
      description: "Change the organization's general settings.",
    },
    feedbackhub_admin: {
      name: 'FeedbackHub administration',
      description: 'Manage products and all feedback of the organization.',
    },
  },
  welcome: {
    greeting: 'Hi {name}',
    choose: 'Choose an organization to continue',
    noOrganizations:
      'No organization yet? Create one, or ask a manager to send you an invitation by email.',
    createCard: 'Create a new organization',
    createHint: 'Or ask a manager to send you an invitation by email.',
    allSuspended:
      '{names} is suspended. Contact the FeedbackHub system administrator to reopen it.',
    justSuspended:
      "{name} has just been suspended. What you just did wasn't carried out. Contact the FeedbackHub system administrator to reopen it.",
    systemOrganizations: 'Organizations',
  },
  invite: {
    title: 'Invitation to {org}',
    summary: 'You were invited as {role}. The invitation is valid until {time}.',
    emailHint: "Email from the invitation. It can't be changed.",
    passwordHint: '{min} to {max} characters.',
    submit: 'Create account and join',
    hasAccount: 'Already have an account? Sign in',
    emailTaken: 'This email already has an account. Sign in to accept the invitation.',
    accept: 'Accept invitation',
    loadFailed: "We couldn't open this invitation. Check your connection and try again.",
    wrongAccount: {
      title: "This invitation isn't for the account you are using",
      signOut: 'Sign out to use another account',
    },
    emailNotConfirmed: {
      title: 'Verify your email before using the invitation',
      text: 'Open the verification email in your inbox, then open this invitation again.',
    },
    expired: {
      title: 'This invitation has expired',
      text: 'The invitation expired at {time}. Ask an organization manager to send it again.',
    },
    noLongerValid: {
      title: 'This invitation can no longer be used',
      text: 'It may have been revoked, or you may have received a newer one. Open the latest invitation email.',
    },
    suspended: {
      title: '{org} is suspended',
      text: 'The invitation is still valid until {time}. You can accept it once the organization is active again.',
    },
  },
  organizations: {
    switcherTitle: 'Your organizations',
    suspended: 'Suspended',
    backToWelcome: 'Back to welcome page',
    create: 'Create organization',
    personalWorkspace: 'Personal workspace',
    createTitle: 'Create organization',
    name: 'Organization name',
    nameRequired: 'Enter the organization name.',
    slug: 'Short identifier',
    slugHint: "Can't be changed after creation.",
    resendVerification: 'Resend verification email',
  },
  organizationSettings: {
    title: 'Organization management',
    infoTitle: 'Organization details',
    name: 'Organization name',
    slug: 'Short identifier',
    slugFixed: "Can't be changed.",
    save: 'Save',
    renamed: 'Renamed to {name}',
    deactivateTitle: 'Deactivate organization',
    deactivateText:
      'Suspend all activity in {name}. Data is kept. Only a FeedbackHub system administrator can reactivate it.',
    deactivateAction: 'Deactivate organization',
  },
  deactivateOrganization: {
    title: 'Deactivate {name}?',
    intro: 'The whole organization is suspended right away:',
    reason: 'Reason',
    optional: '(optional)',
    count: '{n}/500',
    submit: 'Deactivate organization',
    member: {
      widget:
        "The widget of every product stops receiving feedback, and customers' private links are suspended.",
      access: 'No one can enter the organization, including you.',
      data: 'Data is kept. Only a FeedbackHub system administrator can reactivate it.',
    },
    systemMember: {
      widget:
        "The widget of every product stops receiving feedback, and customers' private links are suspended.",
      access: 'No one can enter the organization, including you.',
      data: 'Data is kept. You can reactivate it in the Organizations list.',
    },
    system: {
      widget:
        "The widget of every product stops receiving feedback, and customers' private links are suspended.",
      access: 'Members cannot enter the organization.',
      data: 'Data is kept. You can reactivate it at any time.',
    },
  },
  invitations: {
    title: 'Invitations',
    inviteMember: 'Invite member',
    inviteShort: 'Invite',
    email: 'Email',
    emailHint: 'Can be sent to people who do not have a Hub account yet.',
    role: 'Role',
    expiry: 'Invitations expire after 7 days and can be revoked before they are used.',
    send: 'Send invitation',
    sending: 'Sending…',
    sent: 'Invitation sent to {email}',
    resentToast: 'Invitation resent. The invitation is valid until {time}',
    revoke: 'Revoke',
    revokeConfirmMessage: 'Revoke the invitation to {email}?',
    revokeConfirmDetail: 'The link in the email will no longer work.',
    delete: 'Delete',
    resend: 'Resend',
    resending: 'Sending…',
    pending: 'Valid until {time}',
    expired: 'Expired at {time}',
    expiredLabel: 'Expired',
    lockReasonOwnerOnly: 'Only an Owner can revoke this invitation',
    lockReasonInsufficientRole: "You don't have every permission of the {role} role",
    emptyTitle: 'No pending invitations',
    emptyDescription:
      'Invitations you send will appear here until they are used, revoked or expired.',
    showingOf: 'Showing {shown} of {total} invitations',
  },
  members: {
    self: 'you',
    actions: 'Actions',
    actionsFor: 'Actions for {name}',
    changeRoles: 'Change roles',
    leave: 'Leave organization',
    remove: 'Remove from organization',
    atLeastOneRole: 'Every member needs at least one role.',
    rolesChanged: "Changed {name}'s roles to {roles}.",
    removed: 'Removed {name} from {org}',
    removeConfirmMessage: 'Remove {name} from {org}?',
    removeConfirmDetail: "{name} will no longer be able to see this organization's data.",
    leaveConfirmMessage: 'Leave {org}?',
    leaveConfirmDetail:
      "You will no longer be able to see {org}'s data. To come back, you will need a new invitation.",
    showingOf: 'Showing {shown} of {total} members',
    lockReasonOwnerOnly: 'Only an Owner can change this row',
    lockReasonInsufficientRole: "You don't have every permission of the {role} role",
    lockReasonInsufficientRoleGeneric: "You don't have enough permissions to change this row",
    ownerOnlyRoleHint: 'Only an Owner can select this role.',
    insufficientRoleHint: "You don't have every permission of this role.",
  },
  notifications: {
    title: 'Notifications',
    empty: 'No notifications yet.',
    markAllRead: 'Mark all as read',
    markAsRead: 'Mark as read',
    OrganizationMemberAdded: 'You were added to organization {organizationName}',
  },
  pushNotifications: {
    title: 'Push notifications',
    enable: 'Push notifications',
    description: 'Get notified on this device even when the app is closed.',
    notSupported: 'Push notifications are not supported on this browser.',
    permissionDenied: 'Notifications are blocked for this site in your browser settings.',
  },
}

export default en
