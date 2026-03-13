import type { AccessControlState, Assignment, AssignmentInput, AuditLogItem, AuthState } from '@/lib/types'

type JsonValue = Record<string, unknown> | null

class ApiError extends Error {
  status: number
  payload: JsonValue

  constructor(status: number, payload: JsonValue) {
    super(`Request failed with status ${status}`)
    this.status = status
    this.payload = payload
  }
}

const JSON_HEADERS = {
  'Content-Type': 'application/json',
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {
    credentials: 'include',
    ...options,
    headers: {
      ...JSON_HEADERS,
      ...(options.headers ?? {}),
    },
  })

  if (!response.ok) {
    let payload: JsonValue = null

    try {
      payload = (await response.json()) as JsonValue
    } catch {
      payload = null
    }

    throw new ApiError(response.status, payload)
  }

  if (response.status === 204) {
    return null as T
  }

  return (await response.json()) as T
}

export const apiClient = {
  getCurrentUser() {
    return request<AuthState>('/api/auth/me', { method: 'GET', headers: {} })
  },
  getAssignments(params: Partial<Record<'from' | 'to' | 'module' | 'mandatory', string>> = {}) {
    const search = new URLSearchParams()

    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        search.set(key, String(value))
      }
    })

    const query = search.toString()
    return request<{ items: Assignment[] }>(`/api/assignments${query ? `?${query}` : ''}`, {
      method: 'GET',
      headers: {},
    })
  },
  createAssignment(input: AssignmentInput) {
    return request<{ item: Assignment }>('/api/assignments', {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },
  updateAssignment(id: string, input: AssignmentInput) {
    return request<{ item: Assignment }>(`/api/assignments/${id}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    })
  },
  deleteAssignment(id: string) {
    return request<{ ok: boolean }>(`/api/assignments/${id}`, {
      method: 'DELETE',
    })
  },
  logout() {
    return request<{ ok: boolean }>('/api/auth/logout', {
      method: 'POST',
    })
  },
  getAccessControl() {
    return request<AccessControlState>('/api/admin/access-control', {
      method: 'GET',
      headers: {},
    })
  },
  addWhitelistEntry(githubLogin: string) {
    return request<AccessControlState>('/api/admin/editors', {
      method: 'POST',
      body: JSON.stringify({ githubLogin }),
    })
  },
  removeWhitelistEntry(githubLogin: string) {
    return request<AccessControlState>(`/api/admin/editors/${githubLogin}`, {
      method: 'DELETE',
    })
  },
  addAdminEntry(githubLogin: string) {
    return request<AccessControlState>('/api/admin/admins', {
      method: 'POST',
      body: JSON.stringify({ githubLogin }),
    })
  },
  getAuditLogs() {
    return request<{ items: AuditLogItem[] }>('/api/admin/logs', {
      method: 'GET',
      headers: {},
    })
  },
  removeAdminEntry(githubLogin: string) {
    return request<AccessControlState>(`/api/admin/admins/${githubLogin}`, {
      method: 'DELETE',
    })
  },
}

export { ApiError }
