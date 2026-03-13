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
    throw new Error(`Request failed with status ${response.status}`)
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
}
