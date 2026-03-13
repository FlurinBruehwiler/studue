import { useEffect, useState } from 'react'

import { apiClient } from '@/api/client'
import type { AuthHookState } from '@/lib/types'

const unauthenticatedState = {
  authenticated: false,
  user: null,
} as const

export function useAuth(): AuthHookState {
  const [state, setState] = useState<Omit<AuthHookState, 'logout'>>({
    ...unauthenticatedState,
    isLoading: true,
    source: 'loading',
  })

  useEffect(() => {
    let active = true

    apiClient
      .getCurrentUser()
      .then((data) => {
        if (!active) {
          return
        }

        setState({
          ...data,
          isLoading: false,
          source: 'api',
        })
      })
      .catch(() => {
        if (!active) {
          return
        }

        setState({
          ...unauthenticatedState,
          isLoading: false,
          source: 'error',
        })
      })

    return () => {
      active = false
    }
  }, [])

  async function logout(): Promise<void> {
    try {
      await apiClient.logout()
    } finally {
        setState({
          ...unauthenticatedState,
          isLoading: false,
          source: 'local',
        })
    }
  }

  return {
    ...state,
    logout,
  }
}
