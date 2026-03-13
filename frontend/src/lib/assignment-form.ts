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
