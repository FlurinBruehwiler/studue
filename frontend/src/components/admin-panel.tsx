import { useEffect, useState } from 'react'
import { Shield, Trash2 } from 'lucide-react'

import { ApiError, apiClient } from '@/api/client'
import { Button } from '@/components/ui/button'
import type { AccessControlState } from '@/lib/types'

type AdminPanelProps = {
  isVisible: boolean
}

const initialState: AccessControlState = {
  admins: [],
  editors: [],
}

export function AdminPanel({ isVisible }: AdminPanelProps) {
  const [whitelist, setWhitelist] = useState<AccessControlState>(initialState)
  const [newLogin, setNewLogin] = useState('')
  const [error, setError] = useState('')
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    if (!isVisible) {
      return
    }

    apiClient
      .getWhitelist()
      .then(setWhitelist)
      .catch(() => {
        setError('Could not load the whitelist.')
      })
  }, [isVisible])

  if (!isVisible) {
    return null
  }

  async function handleAdd() {
    if (!newLogin.trim()) {
      return
    }

    setIsSaving(true)
    setError('')

    try {
      const data = await apiClient.addWhitelistEntry(newLogin.trim())
      setWhitelist(data)
      setNewLogin('')
    } catch (errorValue) {
      const message =
        errorValue instanceof ApiError && errorValue.payload && 'error' in errorValue.payload
          ? String((errorValue.payload as { error?: { message?: string } }).error?.message ?? '')
          : ''
      setError(message || 'Could not add the whitelist entry.')
    } finally {
      setIsSaving(false)
    }
  }

  async function handleRemove(githubLogin: string) {
    setIsSaving(true)
    setError('')

    try {
      const data = await apiClient.removeWhitelistEntry(githubLogin)
      setWhitelist(data)
    } catch (errorValue) {
      const message =
        errorValue instanceof ApiError && errorValue.payload && 'error' in errorValue.payload
          ? String((errorValue.payload as { error?: { message?: string } }).error?.message ?? '')
          : ''
      setError(message || 'Could not remove the whitelist entry.')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <section className="mt-4 rounded-[1rem] border-[3px] border-slate-900 bg-[#f8f6f2] p-3 sm:mt-6 sm:rounded-[1.25rem] sm:p-4">
      <div className="flex items-center gap-2 text-sm font-semibold uppercase tracking-[0.18em] text-foreground">
        <Shield className="h-4 w-4" />
        Admin
      </div>

      <div className="mt-3 grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div>
          <h2 className="text-lg font-semibold text-foreground">Whitelist</h2>
          <div className="mt-3 flex gap-2">
            <input
              className="h-10 flex-1 rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 text-sm"
              placeholder="github login"
              value={newLogin}
              onChange={(event) => setNewLogin(event.target.value)}
            />
            <Button onClick={handleAdd} disabled={isSaving}>
              Add
            </Button>
          </div>

          {error ? <p className="mt-2 text-sm text-red-700">{error}</p> : null}

          <div className="mt-4 space-y-2">
            {whitelist.editors.map((githubLogin) => (
              <div
                key={githubLogin}
                className="flex items-center justify-between rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-2 text-sm"
              >
                <span>{githubLogin}</span>
                {whitelist.admins.includes(githubLogin) ? (
                  <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                    Admin
                  </span>
                ) : (
                  <button
                    type="button"
                    className="rounded-full p-1 text-slate-700 hover:bg-slate-100"
                    onClick={() => handleRemove(githubLogin)}
                    aria-label={`Remove ${githubLogin}`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                )}
              </div>
            ))}
          </div>
        </div>

        <div>
          <h2 className="text-lg font-semibold text-foreground">Admins</h2>
          <div className="mt-4 space-y-2">
            {whitelist.admins.map((githubLogin) => (
              <div
                key={githubLogin}
                className="rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-2 text-sm"
              >
                {githubLogin}
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}
