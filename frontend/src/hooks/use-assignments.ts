import { useEffect, useState } from 'react'

import { apiClient } from '@/api/client'
import { mockAssignments } from '@/lib/mock-data'
import type { Assignment, AssignmentFilters } from '@/lib/types'

function filterMockAssignments(filters: AssignmentFilters): Assignment[] {
  return mockAssignments.filter((item) => {
    if (filters.module && item.module !== filters.module) {
      return false
    }

    if (filters.mandatory !== '' && String(item.mandatory) !== filters.mandatory) {
      return false
    }

    if (filters.from && item.dueDate < filters.from) {
      return false
    }

    if (filters.to && item.dueDate > filters.to) {
      return false
    }

    return true
  })
}

type AssignmentHookState = {
  items: Assignment[]
  isLoading: boolean
  source: 'loading' | 'api' | 'mock'
}

export function useAssignments(filters: AssignmentFilters, reloadKey = 0): AssignmentHookState {
  const { from, mandatory, module, to } = filters

  const [state, setState] = useState<AssignmentHookState>({
    items: [],
    isLoading: true,
    source: 'loading',
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
        })
      })
      .catch(() => {
        if (!active) {
          return
        }

        setState({
          items: filterMockAssignments({ from, mandatory, module, to }),
          isLoading: false,
          source: 'mock',
        })
      })

    return () => {
      active = false
    }
  }, [from, mandatory, module, reloadKey, to])

  return state
}
