const JSON_HEADERS = {
  'Content-Type': 'application/json',
}

async function request(path, options = {}) {
  const response = await fetch(path, {
    credentials: 'include',
    ...options,
    headers: {
      ...JSON_HEADERS,
      ...(options.headers ?? {}),
    },
  })

  if (!response.ok) {
    let payload = null

    try {
      payload = await response.json()
    } catch {
      payload = null
    }

    const error = new Error(`Request failed with status ${response.status}`)
    error.status = response.status
    error.payload = payload
    throw error
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

export const apiClient = {
  getCurrentUser() {
    return request('/api/auth/me', { method: 'GET', headers: {} })
  },
  getAssignments(params = {}) {
    const search = new URLSearchParams()

    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        search.set(key, String(value))
      }
    })

    const query = search.toString()
    return request(`/api/assignments${query ? `?${query}` : ''}`, {
      method: 'GET',
      headers: {},
    })
  },
  createAssignment(input) {
    return request('/api/assignments', {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },
  updateAssignment(id, input) {
    return request(`/api/assignments/${id}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    })
  },
  deleteAssignment(id) {
    return request(`/api/assignments/${id}`, {
      method: 'DELETE',
    })
  },
  logout() {
    return request('/api/auth/logout', {
      method: 'POST',
    })
  },
}
