import type { AssignmentFormState, AssignmentInput } from '@/lib/types'

export const emptyAssignmentForm: AssignmentFormState = {
  module: '',
  title: '',
  dueDate: '',
  dueTime: '',
  note: '',
  mandatory: true,
}

export function toAssignmentInput(form: AssignmentFormState): AssignmentInput {
  return {
    module: form.module.trim(),
    title: form.title.trim(),
    dueDate: form.dueDate,
    dueTime: form.dueTime,
    note: form.note.trim(),
    mandatory: form.mandatory,
  }
}

export function getTodayDateInputValue(now: Date = new Date()): string {
  const year = String(now.getFullYear())
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

export function validateAssignmentNotPast(form: AssignmentFormState, now: Date = new Date()): string | null {
  if (!form.dueDate) {
    return null
  }

  const today = getTodayDateInputValue(now)
  if (form.dueDate < today) {
    return 'Due date cannot be in the past.'
  }

  if (form.dueDate > today || !form.dueTime) {
    return null
  }

  const [year, month, day] = form.dueDate.split('-').map(Number)
  const [hours, minutes] = form.dueTime.split(':').map(Number)

  if (
    !Number.isInteger(year) ||
    !Number.isInteger(month) ||
    !Number.isInteger(day) ||
    !Number.isInteger(hours) ||
    !Number.isInteger(minutes)
  ) {
    return null
  }

  const dueAt = new Date(year, month - 1, day, hours, minutes, 0, 0)
  if (Number.isNaN(dueAt.getTime())) {
    return null
  }

  return dueAt < now ? 'Due time cannot be in the past for today.' : null
}
