import type { ReactNode } from 'react'

import { Button } from '@/components/ui/button'

type ConfirmDialogProps = {
  isOpen: boolean
  title: string
  description: ReactNode
  confirmLabel?: string
  cancelLabel?: string
  isLoading?: boolean
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({
  isOpen,
  title,
  description,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  isLoading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  if (!isOpen) {
    return null
  }

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/30 p-4 backdrop-blur-sm" onClick={onCancel}>
      <div
        className="w-full max-w-md rounded-[1.25rem] border-[3px] border-slate-900 bg-[#f8f6f2] p-5 shadow-soft"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 className="text-xl font-semibold text-foreground">{title}</h2>
        <div className="mt-3 text-sm text-muted-foreground">{description}</div>

        <div className="mt-5 flex justify-end gap-3">
          <Button type="button" variant="outline" onClick={onCancel} disabled={isLoading}>
            {cancelLabel}
          </Button>
          <Button type="button" onClick={onConfirm} disabled={isLoading}>
            {isLoading ? 'Deleting...' : confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  )
}
