import { useMemo, useState } from 'react'
import { ArrowRight, Filter, LogIn, Plus } from 'lucide-react'

import { AssignmentCard } from '@/components/assignment-card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useAssignments } from '@/hooks/use-assignments'
import { useAuth } from '@/hooks/use-auth'

const initialFilters = {
  module: '',
  mandatory: '',
  from: '',
  to: '',
}

export function OverviewPage() {
  const [filters, setFilters] = useState(initialFilters)
  const auth = useAuth()
  const assignments = useAssignments(filters)

  const modules = useMemo(() => {
    return [...new Set(assignments.items.map((item) => item.module))].sort()
  }, [assignments.items])

  const canEdit = auth.authenticated && auth.user?.isAllowedEditor
  const upcomingCount = assignments.items.filter((item) => item.mandatory).length

  return (
    <div className="relative overflow-hidden">
      <div className="absolute inset-x-0 top-0 -z-10 h-[32rem] bg-hero-glow" />

      <main className="container py-6 sm:py-10">
        <section className="rounded-[2rem] border border-white/70 bg-white/72 p-6 shadow-soft backdrop-blur sm:p-8">
          <div className="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-3xl space-y-5">
              <Badge variant="outline">studue.ch</Badge>
              <div className="space-y-3">
                <h1 className="max-w-2xl font-serif text-4xl leading-tight text-foreground sm:text-5xl">
                  One calm place for your class deadlines.
                </h1>
                <p className="max-w-2xl text-base leading-7 text-muted-foreground sm:text-lg">
                  Studue collects assignments, keeps due dates visible, and lets your class update the list together through GitHub Enterprise login.
                </p>
              </div>
              <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                <Badge>Single class v1</Badge>
                <Badge>JSON API</Badge>
                <Badge>OAuth editors</Badge>
              </div>
            </div>

            <div className="grid gap-3 rounded-[1.5rem] bg-secondary/70 p-4 text-sm text-secondary-foreground sm:grid-cols-3 sm:p-5 lg:min-w-[24rem]">
              <StatCard label="Upcoming tasks" value={String(assignments.items.length)} />
              <StatCard label="Mandatory" value={String(upcomingCount)} />
              <StatCard label="Editor access" value={canEdit ? 'Allowed' : 'Login'} />
            </div>
          </div>

          <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:flex-wrap">
            {canEdit ? (
              <Button size="lg">
                <Plus className="h-4 w-4" />
                New assignment
              </Button>
            ) : (
              <Button asChild size="lg">
                <a href="/api/auth/login">
                  <LogIn className="h-4 w-4" />
                  Login with GitHub ZHAW
                </a>
              </Button>
            )}
            <Button variant="secondary" size="lg" asChild>
              <a href="/docs/api-contracts.md">
                API contracts
                <ArrowRight className="h-4 w-4" />
              </a>
            </Button>
          </div>
        </section>

        <section className="mt-8 grid gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
          <aside className="rounded-[1.5rem] border border-white/70 bg-white/80 p-5 shadow-soft backdrop-blur">
            <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
              <Filter className="h-4 w-4" />
              Filters
            </div>

            <div className="mt-5 space-y-4">
              <FilterField label="Module">
                <select
                  className="h-11 w-full rounded-2xl border border-border bg-background/80 px-4 text-sm outline-none ring-0"
                  value={filters.module}
                  onChange={(event) =>
                    setFilters((current) => ({ ...current, module: event.target.value }))
                  }
                >
                  <option value="">All modules</option>
                  {modules.map((module) => (
                    <option key={module} value={module}>
                      {module}
                    </option>
                  ))}
                </select>
              </FilterField>

              <FilterField label="Mandatory">
                <select
                  className="h-11 w-full rounded-2xl border border-border bg-background/80 px-4 text-sm outline-none ring-0"
                  value={filters.mandatory}
                  onChange={(event) =>
                    setFilters((current) => ({ ...current, mandatory: event.target.value }))
                  }
                >
                  <option value="">All</option>
                  <option value="true">Mandatory</option>
                  <option value="false">Optional</option>
                </select>
              </FilterField>

              <FilterField label="From">
                <input
                  type="date"
                  className="h-11 w-full rounded-2xl border border-border bg-background/80 px-4 text-sm outline-none ring-0"
                  value={filters.from}
                  onChange={(event) =>
                    setFilters((current) => ({ ...current, from: event.target.value }))
                  }
                />
              </FilterField>

              <FilterField label="To">
                <input
                  type="date"
                  className="h-11 w-full rounded-2xl border border-border bg-background/80 px-4 text-sm outline-none ring-0"
                  value={filters.to}
                  onChange={(event) =>
                    setFilters((current) => ({ ...current, to: event.target.value }))
                  }
                />
              </FilterField>
            </div>
          </aside>

          <section className="space-y-4">
            <div className="flex flex-col gap-2 rounded-[1.5rem] border border-dashed border-border bg-white/50 p-4 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
              <span>
                {assignments.isLoading
                  ? 'Loading assignments...'
                  : `${assignments.items.length} assignments shown`}
              </span>
              <span>
                {assignments.source === 'mock'
                  ? 'Using scaffold preview data until the backend is ready.'
                  : 'Connected to the backend API.'}
              </span>
            </div>

            {assignments.items.length === 0 && !assignments.isLoading ? (
              <div className="rounded-[1.5rem] border border-white/70 bg-white/80 p-8 text-center text-muted-foreground shadow-soft">
                No assignments match the current filters.
              </div>
            ) : null}

            <div className="space-y-4">
              {assignments.items.map((assignment) => (
                <AssignmentCard
                  key={assignment.id}
                  assignment={assignment}
                  canEdit={canEdit}
                />
              ))}
            </div>
          </section>
        </section>
      </main>
    </div>
  )
}

function FilterField({ label, children }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-foreground">{label}</span>
      {children}
    </label>
  )
}

function StatCard({ label, value }) {
  return (
    <div className="rounded-[1.25rem] bg-white/80 p-4">
      <div className="text-2xl font-semibold text-foreground">{value}</div>
      <div className="mt-1 text-xs uppercase tracking-[0.2em] text-muted-foreground">
        {label}
      </div>
    </div>
  )
}
