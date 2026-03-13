import { useState, type ReactNode } from 'react'
import { Trash2, X } from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  emptyAssignmentForm,
  getTodayDateInputValue,
  toAssignmentInput,
  validateAssignmentNotPast,
} from '@/lib/assignment-form'
import { MODULE_OPTIONS } from '@/lib/modules'
import type { Assignment, AssignmentFormState, AssignmentInput } from '@/lib/types'

type AssignmentFormDialogProps = {
  assignment: Assignment | null
  isOpen: boolean
  isSaving: boolean
  isDeleting?: boolean
  error: string
  onClose: () => void
  onSubmit: (input: AssignmentInput) => void
  onDelete?: () => void
}

export function AssignmentFormDialog({
  assignment,
  isOpen,
  isSaving,
  isDeleting = false,
  error,
  onClose,
  onSubmit,
  onDelete,
}: AssignmentFormDialogProps) {
  const [form, setForm] = useState<AssignmentFormState>(() => {
    if (!assignment) {
      return emptyAssignmentForm
    }

    return {
      module: assignment.module,
      title: assignment.title,
      dueDate: assignment.dueDate,
      dueTime: assignment.dueTime ?? '',
      note: assignment.note,
      mandatory: assignment.mandatory,
    }
  })

  const clientValidationError = validateAssignmentNotPast(form)
  const displayedError = clientValidationError ?? error

  if (!isOpen) {
    return null
  }

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/20 p-4 backdrop-blur-sm">
      <div className="w-full max-w-2xl rounded-[2rem] border-[3px] border-slate-900 bg-[#f8f6f2] p-6 shadow-soft sm:p-8">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="font-serif text-3xl text-foreground">
              {assignment ? 'Edit assignment' : 'New assignment'}
            </h2>
          </div>
          <button
            type="button"
            className="rounded-full border-2 border-slate-900 p-2 text-slate-900"
            onClick={onClose}
            aria-label="Close form"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form
          className="mt-6 space-y-4"
          onSubmit={(event) => {
            event.preventDefault()

            if (clientValidationError) {
              return
            }

            onSubmit(toAssignmentInput(form))
          }}
        >
          <div className="grid gap-4 sm:grid-cols-3">
            <FormField label="Module">
              <select
                className="h-12 w-full rounded-2xl border-2 border-slate-900 bg-white px-4 text-sm"
                value={form.module}
                onChange={(event) => setForm((current) => ({ ...current, module: event.target.value }))}
                required
              >
                <option value="">Choose a module</option>
                {MODULE_OPTIONS.map((module) => (
                  <option key={module.value} value={module.value}>
                    {module.label}
                  </option>
                ))}
              </select>
            </FormField>

            <FormField label="Due date">
              <input
                type="date"
                className="h-12 w-full rounded-2xl border-2 border-slate-900 bg-white px-4 text-sm"
                value={form.dueDate}
                onChange={(event) => setForm((current) => ({ ...current, dueDate: event.target.value }))}
                min={getTodayDateInputValue()}
                required
              />
            </FormField>

            <FormField label="Time (optional)">
              <input
                type="time"
                className="h-12 w-full rounded-2xl border-2 border-slate-900 bg-white px-4 text-sm"
                value={form.dueTime}
                onChange={(event) => setForm((current) => ({ ...current, dueTime: event.target.value }))}
              />
            </FormField>
          </div>

          <FormField label="Title">
            <input
              className="h-12 w-full rounded-2xl border-2 border-slate-900 bg-white px-4 text-sm"
              value={form.title}
              onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))}
              required
            />
          </FormField>

          <FormField label="Details">
            <textarea
              className="min-h-40 w-full rounded-[1.5rem] border-2 border-slate-900 bg-white px-4 py-3 text-sm"
              value={form.note}
              onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))}
            />
          </FormField>

          <FormField label="Type">
            <div className="flex flex-wrap gap-3">
              <ToggleButton
                active={!form.mandatory}
                tone="green"
                onClick={() => setForm((current) => ({ ...current, mandatory: false }))}
              >
                Optional
              </ToggleButton>
              <ToggleButton
                active={form.mandatory}
                tone="red"
                onClick={() => setForm((current) => ({ ...current, mandatory: true }))}
              >
                Mandatory
              </ToggleButton>
            </div>
          </FormField>

          {displayedError ? (
            <div className="rounded-2xl border-2 border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700">
              {displayedError}
            </div>
          ) : null}

          <div className="flex items-center justify-between gap-3 pt-2">
            <div>
              {assignment && onDelete ? (
                <Button type="button" variant="ghost" onClick={onDelete} disabled={isDeleting || isSaving}>
                  <Trash2 className="h-4 w-4" />
                  {isDeleting ? 'Deleting...' : 'Delete'}
                </Button>
              ) : null}
            </div>

            <div className="flex justify-end gap-3">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSaving || Boolean(clientValidationError)}>
              {isSaving ? 'Saving...' : assignment ? 'Save changes' : 'Create assignment'}
            </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  )
}

type FormFieldProps = {
  label: string
  children: ReactNode
}

function FormField({ label, children }: FormFieldProps) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-semibold text-foreground">{label}</span>
      {children}
    </label>
  )
}

type ToggleButtonProps = {
  active: boolean
  tone: 'green' | 'red'
  onClick: () => void
  children: ReactNode
}

function ToggleButton({ active, tone, onClick, children }: ToggleButtonProps) {
  const colors = {
    green: active ? 'bg-green-100 text-green-900' : 'bg-white text-green-700',
    red: active ? 'bg-red-100 text-red-900' : 'bg-white text-red-700',
  }

  return (
    <button
      type="button"
      className={`rounded-2xl border-2 border-slate-900 px-4 py-2 text-sm font-semibold ${colors[tone]}`}
      onClick={onClick}
    >
      {children}
    </button>
  )
}
