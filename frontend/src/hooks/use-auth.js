import { useEffect, useState } from 'react'

import { apiClient } from '@/api/client'
import { mockUser } from '@/lib/mock-data'

export function useAuth() {
  const [state, setState] = useState({
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

  return state
}
