import { useEffect, useState } from 'react'

import { apiClient } from '@/api/client'
import { mockAssignments } from '@/lib/mock-data'

function filterMockAssignments(filters) {
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

export function useAssignments(filters, reloadKey = 0) {
  const { from, mandatory, module, to } = filters

  const [state, setState] = useState({
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
