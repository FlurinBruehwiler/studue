import { useState } from 'react'
import { X } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { emptyAssignmentForm, toAssignmentInput } from '@/lib/assignment-form'
import { MODULE_OPTIONS } from '@/lib/modules'

export function AssignmentFormDialog({ assignment, isOpen, isSaving, error, onClose, onSubmit }) {
  const [form, setForm] = useState(() => {
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

  if (!isOpen) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/20 p-4 backdrop-blur-sm">
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

          {error ? (
            <div className="rounded-2xl border-2 border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          ) : null}

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSaving}>
              {isSaving ? 'Saving...' : assignment ? 'Save changes' : 'Create assignment'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}

function FormField({ label, children }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-semibold text-foreground">{label}</span>
      {children}
    </label>
  )
}

function ToggleButton({ active, tone, onClick, children }) {
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
