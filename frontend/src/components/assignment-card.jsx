import { CalendarDays, CircleDot, PencilLine } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

export function AssignmentCard({ assignment, canEdit }) {
  return (
    <article className="animate-fade-up rounded-[1.5rem] border border-white/70 bg-white/80 p-5 shadow-soft backdrop-blur">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge>{assignment.module}</Badge>
            <Badge variant={assignment.mandatory ? 'accent' : 'outline'}>
              {assignment.mandatory ? 'Mandatory' : 'Optional'}
            </Badge>
          </div>

          <div>
            <h3 className="text-xl font-semibold text-foreground">{assignment.title}</h3>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
              {assignment.note}
            </p>
          </div>
        </div>

        {canEdit ? (
          <Button variant="outline" size="sm">
            <PencilLine className="h-4 w-4" />
            Edit
          </Button>
        ) : null}
      </div>

      <div className="mt-4 flex flex-col gap-3 border-t border-border/70 pt-4 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-2">
          <CalendarDays className="h-4 w-4" />
          <span>Due {assignment.dueDate}</span>
        </div>
        <div className="flex items-center gap-2">
          <CircleDot className="h-4 w-4" />
          <span>Last updated by {assignment.updatedBy.displayName}</span>
        </div>
      </div>
    </article>
  )
}
