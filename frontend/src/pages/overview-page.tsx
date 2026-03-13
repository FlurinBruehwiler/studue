import { useMemo, useState, type ReactNode } from 'react'
import { LogIn, LogOut, Plus } from 'lucide-react'

import { ApiError, apiClient } from '@/api/client'
import { AssignmentCard } from '@/components/assignment-card'
import { AdminPanel } from '@/components/admin-panel'
import { AssignmentDetailDialog } from '@/components/assignment-detail-dialog'
import { AssignmentFormDialog } from '@/components/assignment-form-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useAssignments } from '@/hooks/use-assignments'
import { useAuth } from '@/hooks/use-auth'
import { MODULE_OPTIONS } from '@/lib/modules'
import type { Assignment, AssignmentFilters, AssignmentInput } from '@/lib/types'

const initialFilters: AssignmentFilters = {
  module: '',
  mandatory: '',
  from: '',
  to: '',
}

export function OverviewPage() {
  const [filters, setFilters] = useState<AssignmentFilters>(initialFilters)
  const [selectedAssignment, setSelectedAssignment] = useState<Assignment | null>(null)
  const [editingAssignment, setEditingAssignment] = useState<Assignment | null>(null)
  const [isFormOpen, setIsFormOpen] = useState(false)
  const [formError, setFormError] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)

  const auth = useAuth()
  const assignments = useAssignments(filters, reloadKey)

  const today = new Date().toISOString().slice(0, 10)
  const visibleItems = useMemo(() => {
    return assignments.items.filter((assignment) => assignment.dueDate >= today)
  }, [assignments.items, today])

  const groupedAssignments = useMemo(() => {
    return visibleItems.reduce<Record<string, Assignment[]>>((groups, assignment) => {
      if (!groups[assignment.dueDate]) {
        groups[assignment.dueDate] = []
      }

      groups[assignment.dueDate].push(assignment)
      return groups
    }, {})
  }, [visibleItems])

  const moduleCounts = useMemo(() => {
    return visibleItems.reduce<Record<string, number>>((counts, assignment) => {
      counts[assignment.module] = (counts[assignment.module] ?? 0) + 1
      return counts
    }, {})
  }, [visibleItems])

  const mandatoryCounts = useMemo(() => {
    return visibleItems.reduce(
      (counts, assignment) => {
        if (assignment.mandatory) {
          counts.mandatory += 1
        } else {
          counts.optional += 1
        }

        return counts
      },
      { optional: 0, mandatory: 0 },
    )
  }, [visibleItems])

  const canEdit = auth.authenticated && Boolean(auth.user?.isAllowedEditor)
  const canAdmin = auth.authenticated && Boolean(auth.user?.isAdmin)

  async function handleSubmit(input: AssignmentInput) {
    setIsSaving(true)
    setFormError('')

    try {
      if (editingAssignment) {
        await apiClient.updateAssignment(editingAssignment.id, input)
      } else {
        await apiClient.createAssignment(input)
      }

      setIsFormOpen(false)
      setEditingAssignment(null)
      setReloadKey((value) => value + 1)
    } catch (error) {
      const message =
        error instanceof ApiError && error.payload && 'error' in error.payload
          ? String((error.payload as { error?: { message?: string } }).error?.message ?? '')
          : ''
      setFormError(message || 'Could not save the assignment.')
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDelete() {
    if (!selectedAssignment) {
      return
    }

    setIsDeleting(true)

    try {
      await apiClient.deleteAssignment(selectedAssignment.id)
      setSelectedAssignment(null)
      setReloadKey((value) => value + 1)
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className="sm:px-6 sm:py-5">
      <main className="min-h-screen w-full overflow-x-hidden bg-[#efefef] p-2.5 sm:mx-auto sm:min-h-0 sm:max-w-7xl sm:rounded-[1.5rem] sm:border-[3px] sm:border-slate-900 sm:p-4">
        <header className="flex flex-col gap-3 sm:gap-4">
          <div className="flex flex-col gap-2.5 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
            <h1 className="text-center font-serif text-[1.95rem] text-foreground sm:flex-1 sm:text-center sm:text-[2.8rem]">
              IT25a_WIN
            </h1>
            <div className="flex justify-center sm:flex-1 sm:justify-end">
              {canEdit ? (
                <button
                  type="button"
                  className="flex h-11 w-full max-w-24 items-center justify-center rounded-[0.9rem] border-[3px] border-slate-900 bg-[#bae5b8] text-slate-900 transition hover:brightness-95 sm:h-14 sm:max-w-28 sm:rounded-[1.1rem]"
                  onClick={() => {
                    setEditingAssignment(null)
                    setFormError('')
                    setIsFormOpen(true)
                  }}
                >
                  <Plus className="h-6 w-6 sm:h-7 sm:w-7" />
                </button>
              ) : (
                <Button asChild>
                  <a href="/api/auth/login">
                    <LogIn className="h-4 w-4" />
                    Login
                  </a>
                </Button>
              )}
            </div>
          </div>

          <div className="flex flex-col gap-2.5 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex min-w-0 flex-col gap-2 text-sm sm:flex-row sm:flex-wrap sm:items-center sm:gap-2">
              <span className="mr-2 text-foreground">Filter:</span>
              <select
                className="h-9 w-full min-w-0 rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 text-sm sm:w-auto sm:min-w-[16rem] sm:rounded-[0.95rem]"
                value={filters.module}
                onChange={(event) =>
                  setFilters((current) => ({
                    ...current,
                    module: event.target.value,
                  }))
                }
              >
                <option value="">All modules ({visibleItems.length})</option>
                {MODULE_OPTIONS.map((module) => (
                  <option key={module.value} value={module.value}>
                    {module.label} ({moduleCounts[module.value] ?? 0})
                  </option>
                ))}
              </select>
              <FilterChip
                active={filters.mandatory === 'false'}
                tone="green"
                onClick={() =>
                  setFilters((current) => ({
                    ...current,
                    mandatory: current.mandatory === 'false' ? '' : 'false',
                  }))
                }
              >
                Optional ({mandatoryCounts.optional})
              </FilterChip>
              <FilterChip
                active={filters.mandatory === 'true'}
                tone="red"
                onClick={() =>
                  setFilters((current) => ({
                    ...current,
                    mandatory: current.mandatory === 'true' ? '' : 'true',
                  }))
                }
              >
                Mandatory ({mandatoryCounts.mandatory})
              </FilterChip>
            </div>

            <div className="flex items-center gap-2 text-xs text-muted-foreground sm:text-sm">
              {auth.authenticated && auth.user ? (
                <>
                  <Badge>{auth.user.displayName}</Badge>
                  <Button variant="ghost" onClick={auth.logout}>
                    <LogOut className="h-4 w-4" />
                  </Button>
                </>
              ) : (
                <span>Public read access</span>
              )}
            </div>
          </div>
        </header>

        <AdminPanel isVisible={canAdmin} />

        <section className="mt-4 space-y-3 sm:mt-6 sm:space-y-4">
          {visibleItems.length === 0 && !assignments.isLoading ? (
            <div className="rounded-[1.1rem] border-[3px] border-dashed border-slate-900 bg-white/70 p-6 text-center text-sm text-muted-foreground sm:rounded-[1.5rem] sm:p-8">
              No assignments match the current filters.
            </div>
          ) : null}

          {Object.entries(groupedAssignments).map(([date, items]) => (
            <section key={date} className="space-y-2">
              {items.map((assignment) => (
                <AssignmentCard
                  key={assignment.id}
                  assignment={assignment}
                  canEdit={canEdit}
                  onSelect={() => setSelectedAssignment(assignment)}
                  onEdit={() => {
                    setEditingAssignment(assignment)
                    setFormError('')
                    setIsFormOpen(true)
                  }}
                />
              ))}
            </section>
          ))}
        </section>
      </main>

      <AssignmentDetailDialog
        assignment={selectedAssignment}
        canEdit={canEdit}
        isDeleting={isDeleting}
        onClose={() => setSelectedAssignment(null)}
        onEdit={() => {
          setEditingAssignment(selectedAssignment)
          setFormError('')
          setIsFormOpen(true)
        }}
        onDelete={handleDelete}
      />

      {isFormOpen ? (
        <AssignmentFormDialog
          key={editingAssignment?.id ?? 'new'}
          assignment={editingAssignment}
          isOpen={isFormOpen}
          isSaving={isSaving}
          error={formError}
          onClose={() => {
            setIsFormOpen(false)
            setEditingAssignment(null)
            setFormError('')
          }}
          onSubmit={handleSubmit}
        />
      ) : null}
    </div>
  )
}

type FilterChipProps = {
  active: boolean
  tone?: 'default' | 'green' | 'red'
  onClick: () => void
  children: ReactNode
}

function FilterChip({ active, tone = 'default', onClick, children }: FilterChipProps) {
  const palette = {
    default: active ? 'bg-white' : 'bg-[#f7f7f7]',
    green: active ? 'bg-green-100 text-green-900' : 'bg-white text-green-700',
    red: active ? 'bg-red-100 text-red-900' : 'bg-white text-red-700',
  }

  return (
    <button
      type="button"
      className={`rounded-xl border-2 border-slate-900 px-5 py-1.5 text-sm font-medium ${palette[tone]}`}
      onClick={onClick}
    >
      {children}
    </button>
  )
}
