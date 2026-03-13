export type AssignmentUser = {
  githubLogin: string
  displayName: string
  email: string
}

export type Assignment = {
  id: string
  className: string
  module: string
  title: string
  dueDate: string
  dueTime: string
  note: string
  mandatory: boolean
  createdBy: AssignmentUser
  updatedBy: AssignmentUser
  createdAt: string
  updatedAt: string
}

export type AssignmentInput = {
  module: string
  title: string
  dueDate: string
  dueTime: string
  note: string
  mandatory: boolean
}

export type AuthUser = {
  githubLogin: string
  displayName: string
  email: string
  isAllowedEditor: boolean
  isAdmin: boolean
}

export type AuthState = {
  authenticated: boolean
  user: AuthUser | null
}

export type AuthHookState = AuthState & {
  isLoading: boolean
  source: 'loading' | 'api' | 'mock' | 'local'
  logout: () => Promise<void>
}

export type AssignmentFilters = {
  module: string
  mandatory: string
  from: string
  to: string
}

export type AssignmentFormState = AssignmentInput

export type AccessControlState = {
  admins: string[]
  editors: string[]
}

export type AuditLogItem = {
  timestamp: string
  action: 'add' | 'edit' | 'delete' | string
  actorLogin: string
  actorDisplayName: string
  assignmentId: string
  title: string
  dueDate: string
  dueTime: string
}
