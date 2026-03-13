import { useEffect, useState } from 'react'

import { ApiError, apiClient } from '@/api/client'
import type { AssignmentFilters, AssignmentHookState } from '@/lib/types'

export function useAssignments(filters: AssignmentFilters, reloadKey = 0): AssignmentHookState {
  const { from, mandatory, module, to } = filters

  const [state, setState] = useState<AssignmentHookState>({
    items: [],
    isLoading: true,
    source: 'loading',
    errorMessage: '',
  })

  useEffect(() => {
    let active = true

    apiClient
      .getAssignments({ from, mandatory, module, to })
      .then((data) => {
        if (!active) {
          return
        }

        setState({
          items: data.items ?? [],
          isLoading: false,
          source: 'api',
          errorMessage: '',
        })
      })
      .catch((error: unknown) => {
        if (!active) {
          return
        }

        const errorMessage =
          error instanceof ApiError
            ? `Could not load assignments (${error.status}).`
            : 'Could not connect to the server.'

        setState({
          items: [],
          isLoading: false,
          source: 'error',
          errorMessage,
        })
      })

    return () => {
      active = false
    }
  }, [from, mandatory, module, reloadKey, to])

  return state
}
