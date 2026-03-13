import { useEffect, useMemo, useState } from 'react'
import { ArrowLeft, Shield, UserCog, UserRoundPlus } from 'lucide-react'

import { ApiError, apiClient } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { SidebarNav } from '@/components/ui/sidebar-nav'
import { formatDisplayDateTime } from '@/lib/date'
import { formatSwissDateAndTime } from '@/lib/date'
import type { AccessControlState, AuditLogItem } from '@/lib/types'

type AdminPanelProps = {
  isVisible: boolean
}

type AdminSection = 'whitelist' | 'admins' | 'logs'

const initialState: AccessControlState = {
  admins: [],
  editors: [],
}

export function AdminPanel({ isVisible }: AdminPanelProps) {
  const [section, setSection] = useState<AdminSection>('whitelist')
  const [accessControl, setAccessControl] = useState<AccessControlState>(initialState)
  const [logs, setLogs] = useState<AuditLogItem[]>([])
  const [newEditor, setNewEditor] = useState('')
  const [newAdmin, setNewAdmin] = useState('')
  const [error, setError] = useState('')
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    if (!isVisible) {
      return
    }

    Promise.all([apiClient.getAccessControl(), apiClient.getAuditLogs()])
      .then(([accessControlData, auditLogData]) => {
        setAccessControl(accessControlData)
        setLogs(auditLogData.items)
        setError('')
      })
      .catch(() => {
        setError('Could not load admin data.')
      })
  }, [isVisible])

  const visibleEditors = useMemo(() => {
    return accessControl.editors.filter((githubLogin) => !accessControl.admins.includes(githubLogin))
  }, [accessControl])

  const sidebarItems = [
    { key: 'whitelist', label: 'Whitelist', icon: <UserRoundPlus className="h-4 w-4" /> },
    { key: 'admins', label: 'Admins', icon: <UserCog className="h-4 w-4" /> },
    { key: 'logs', label: 'Edit Log', icon: <Shield className="h-4 w-4" /> },
  ] as const

  if (!isVisible) {
    return null
  }

  async function handleAddEditor() {
    if (!newEditor.trim()) {
      return
    }

    setIsSaving(true)
    setError('')

    try {
      const data = await apiClient.addWhitelistEntry(newEditor.trim())
      setAccessControl(data)
      setNewEditor('')
    } catch (errorValue) {
      setError(extractErrorMessage(errorValue, 'Could not add the whitelist entry.'))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleRemoveEditor(githubLogin: string) {
    setIsSaving(true)
    setError('')

    try {
      const data = await apiClient.removeWhitelistEntry(githubLogin)
      setAccessControl(data)
    } catch (errorValue) {
      setError(extractErrorMessage(errorValue, 'Could not remove the whitelist entry.'))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleAddAdmin() {
    if (!newAdmin.trim()) {
      return
    }

    setIsSaving(true)
    setError('')

    try {
      const data = await apiClient.addAdminEntry(newAdmin.trim())
      setAccessControl(data)
      setNewAdmin('')
    } catch (errorValue) {
      setError(extractErrorMessage(errorValue, 'Could not add the admin.'))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="sm:px-6 sm:py-5">
      <main className="min-h-screen w-full overflow-x-hidden bg-[#efefef] p-2.5 sm:mx-auto sm:min-h-0 sm:max-w-7xl sm:rounded-[1.5rem] sm:border-[3px] sm:border-slate-900 sm:p-4">
        <div className="mb-4 flex items-center justify-between gap-3">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Admin Area
            </div>
            <h1 className="font-serif text-[1.8rem] text-foreground sm:text-[2.4rem]">Studue Admin</h1>
          </div>
          <Button variant="outline" asChild>
            <a href="/">
              <ArrowLeft className="h-4 w-4" />
              Back
            </a>
          </Button>
        </div>

        <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
          <Card className="sm:sticky sm:top-4 sm:w-60 sm:shrink-0">
            <CardContent>
              <div className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-[0.18em] text-foreground">
                <Shield className="h-4 w-4" />
                Admin
              </div>
              <SidebarNav
                items={sidebarItems.map((item) => ({ ...item }))}
                activeKey={section}
                onChange={(key) => setSection(key as AdminSection)}
              />
            </CardContent>
          </Card>

          <Card className="min-w-0 sm:flex-1">
            <CardContent>
            {error ? <p className="mb-4 text-sm text-red-700">{error}</p> : null}

          {section === 'whitelist' ? (
            <div>
              <h2 className="text-lg font-semibold text-foreground">Whitelist</h2>
              <div className="mt-3 flex gap-2">
                <input
                  className="h-10 flex-1 rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 text-sm"
                  placeholder="github login"
                  value={newEditor}
                  onChange={(event) => setNewEditor(event.target.value)}
                />
                <Button onClick={handleAddEditor} disabled={isSaving}>
                  Add
                </Button>
              </div>

              <div className="mt-4 space-y-2">
                {visibleEditors.map((githubLogin) => (
                  <div
                    key={githubLogin}
                    className="flex items-center justify-between rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-2 text-sm"
                  >
                    <span>{githubLogin}</span>
                    <button
                      type="button"
                      className="rounded-full px-2 py-1 text-xs font-semibold text-red-700 hover:bg-red-50"
                      onClick={() => handleRemoveEditor(githubLogin)}
                    >
                      Remove
                    </button>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {section === 'admins' ? (
            <div>
              <h2 className="text-lg font-semibold text-foreground">Admins</h2>
              <div className="mt-3 flex gap-2">
                <input
                  className="h-10 flex-1 rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 text-sm"
                  placeholder="github login"
                  value={newAdmin}
                  onChange={(event) => setNewAdmin(event.target.value)}
                />
                <Button onClick={handleAddAdmin} disabled={isSaving}>
                  Add
                </Button>
              </div>

              <div className="mt-4 space-y-2">
                {accessControl.admins.map((githubLogin) => (
                  <div
                    key={githubLogin}
                    className="rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-2 text-sm"
                  >
                    {githubLogin}
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {section === 'logs' ? (
            <div>
              <h2 className="text-lg font-semibold text-foreground">Edit Log</h2>
              <div className="mt-4 space-y-2">
                {logs.map((item) => (
                  <div
                    key={`${item.timestamp}-${item.assignmentId}-${item.action}`}
                    className={`rounded-[0.8rem] border-2 px-3 py-2 text-sm ${logColors[item.action] ?? logColors.edit}`}
                  >
                    <div className="font-semibold">
                      {item.action.toUpperCase()} - {item.title}
                    </div>
                    <div className="mt-1 text-xs opacity-80">
                      {item.actorDisplayName} ({item.actorLogin}) - {formatDisplayDateTime(item.timestamp)}
                    </div>
                    <div className="mt-1 text-xs opacity-80">
                      {formatSwissDateAndTime(item.dueDate, item.dueTime)}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  )
}

const logColors: Record<string, string> = {
  add: 'border-green-700 bg-green-50 text-green-900',
  edit: 'border-blue-700 bg-blue-50 text-blue-900',
  delete: 'border-red-700 bg-red-50 text-red-900',
}

function extractErrorMessage(errorValue: unknown, fallback: string): string {
  return errorValue instanceof ApiError && errorValue.payload && 'error' in errorValue.payload
    ? String((errorValue.payload as { error?: { message?: string } }).error?.message ?? fallback)
    : fallback
}
