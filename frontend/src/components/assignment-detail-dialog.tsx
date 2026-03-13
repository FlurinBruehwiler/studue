import { Pencil, X } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { formatDisplayDateTime, formatSwissDateAndTime } from '@/lib/date'
import { getModuleLabel } from '@/lib/modules'
import type { Assignment } from '@/lib/types'

type AssignmentDetailDialogProps = {
  assignment: Assignment | null
  canEdit: boolean
  onClose: () => void
  onEdit: () => void
}

export function AssignmentDetailDialog({
  assignment,
  canEdit,
  onClose,
  onEdit,
}: AssignmentDetailDialogProps) {
  if (!assignment) {
    return null
  }

  return (
    <div
      className="fixed inset-0 z-40 flex items-center justify-center bg-black/10 p-0 backdrop-blur-sm sm:p-4"
      onClick={onClose}
    >
      <div
        className="h-full w-full overflow-y-auto bg-[#f8f6f2] p-5 sm:h-auto sm:max-w-5xl sm:rounded-[2rem] sm:border-[3px] sm:border-slate-900 sm:p-10 sm:shadow-soft"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="max-w-3xl font-serif text-3xl text-foreground sm:text-5xl">
              {assignment.title}
            </h2>
            <p className="mt-3 text-lg text-foreground">
              {getModuleLabel(assignment.module)} -{' '}
              {formatSwissDateAndTime(assignment.dueDate, assignment.dueTime)}
            </p>
          </div>

          <div className="flex items-center gap-2 text-slate-900">
            {canEdit ? (
              <button
                type="button"
                className="rounded-full p-2 hover:bg-black/5"
                onClick={onEdit}
                aria-label="Edit assignment"
              >
                <Pencil className="h-7 w-7" />
              </button>
            ) : null}
            <button
              type="button"
              className="rounded-full p-2 hover:bg-black/5"
              onClick={onClose}
              aria-label="Close details"
            >
              <X className="h-9 w-9" />
            </button>
          </div>
        </div>

        <div className="mt-8 min-h-[16rem] space-y-5 text-lg leading-8 text-foreground">
          {assignment.note
            .split('\n')
            .filter(Boolean)
            .map((line) => (
              <p key={line}>{renderRichLine(line)}</p>
            ))}
        </div>

        <div className="mt-12 flex flex-col gap-6 text-base text-foreground sm:flex-row sm:items-end sm:justify-between">
          <div>Created by: {assignment.createdBy.displayName}</div>
          <div>Last edited: {assignment.updatedBy.displayName} - {formatDisplayDateTime(assignment.updatedAt)}</div>
        </div>
      </div>
    </div>
  )
}

function renderRichLine(line: string) {
  const urlPattern = /(https?:\/\/\S+)/g
  const parts = line.split(urlPattern)

  return parts.map((part, index) => {
    if (/^https?:\/\//.test(part)) {
      return (
        <a
          key={`${part}-${index}`}
          href={part}
          target="_blank"
          rel="noreferrer"
          className="text-blue-600 underline decoration-blue-300 underline-offset-4"
        >
          {part}
        </a>
      )
    }

    return <span key={`${part}-${index}`}>{part}</span>
  })
}
