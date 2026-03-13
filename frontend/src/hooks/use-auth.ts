import { useEffect, useState } from 'react'

import { apiClient } from '@/api/client'
import { mockUser } from '@/lib/mock-data'
import type { AuthHookState } from '@/lib/types'

export function useAuth(): AuthHookState {
  const [state, setState] = useState<Omit<AuthHookState, 'logout'>>({
    ...mockUser,
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
          ...mockUser,
          isLoading: false,
          source: 'mock',
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
        ...mockUser,
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
