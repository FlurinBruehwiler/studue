import { PencilLine } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatDisplayDate } from '@/lib/date'

export function AssignmentCard({ assignment, canEdit, onSelect, onEdit }) {
  return (
    <article className="animate-fade-up space-y-1">
      <div className="text-[1.2rem] font-semibold tracking-tight text-foreground sm:text-[1.45rem]">
        {formatDisplayDate(assignment.dueDate)}
      </div>
      <div className="rounded-[0.95rem] border-[3px] border-slate-900 bg-[#f8f6f2] p-2.5 sm:rounded-[1.15rem] sm:p-3.5">
        <div className="flex items-start justify-between gap-2">
          <button type="button" className="flex-1 text-left" onClick={onSelect}>
            <div className="mb-1 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[0.64rem] font-semibold uppercase tracking-[0.18em] text-muted-foreground sm:text-[0.72rem]">
              <span>{assignment.module}</span>
              {assignment.dueTime ? <span>{assignment.dueTime}</span> : null}
            </div>
            <h3 className="text-[1.02rem] font-semibold leading-tight text-foreground sm:text-[1.15rem]">
              {assignment.title}
            </h3>
          </button>

          <div className="flex items-center gap-1.5">
            <Badge variant={assignment.mandatory ? 'accent' : 'outline'}>
              {assignment.mandatory ? 'Mandatory' : 'Optional'}
            </Badge>
            {canEdit ? (
              <Button variant="ghost" size="sm" onClick={onEdit}>
                <PencilLine className="h-4 w-4" />
              </Button>
            ) : null}
          </div>
        </div>
      </div>
    </article>
  )
}
