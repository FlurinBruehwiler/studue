export const emptyAssignmentForm = {
  module: '',
  title: '',
  dueDate: '',
  dueTime: '',
  note: '',
  mandatory: true,
}

export function toAssignmentInput(form) {
  return {
    module: form.module.trim(),
    title: form.title.trim(),
    dueDate: form.dueDate,
    dueTime: form.dueTime,
    note: form.note.trim(),
    mandatory: form.mandatory,
  }
}
