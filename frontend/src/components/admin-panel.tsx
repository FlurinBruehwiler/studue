import { useEffect, useMemo, useState } from 'react'
import { ArrowLeft, RotateCcw, Shield, UserCog, UserRoundPlus, X } from 'lucide-react'

import { ApiError, apiClient } from '@/api/client'
import { AppLink } from '@/components/ui/app-link'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { SidebarNav } from '@/components/ui/sidebar-nav'
import { formatDisplayDateTime, formatSwissDateAndTime } from '@/lib/date'
import { usePathname } from '@/lib/router'
import type { AccessControlState, AuditLogItem } from '@/lib/types'

type AdminPanelProps = {
  isVisible: boolean
}

type AdminSection = 'whitelist' | 'admins' | 'logs'

function getSectionFromPath(pathname: string): AdminSection {
  if (pathname === '/admin/admins') {
    return 'admins'
  }

  if (pathname === '/admin/editlog') {
    return 'logs'
  }

  return 'whitelist'
}

const initialState: AccessControlState = {
  admins: [],
  editors: [],
}

export function AdminPanel({ isVisible }: AdminPanelProps) {
  const pathname = usePathname()
  const section = getSectionFromPath(pathname)
  const [accessControl, setAccessControl] = useState<AccessControlState>(initialState)
  const [logs, setLogs] = useState<AuditLogItem[]>([])
  const [selectedLog, setSelectedLog] = useState<AuditLogItem | null>(null)
  const [newEditor, setNewEditor] = useState('')
  const [newAdmin, setNewAdmin] = useState('')
  const [error, setError] = useState('')
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    if (!isVisible) {
      return
    }

    void reloadAdminData()
  }, [isVisible])

  const visibleEditors = useMemo(() => {
    return accessControl.editors.filter((githubLogin) => !accessControl.admins.includes(githubLogin))
  }, [accessControl])

  const sidebarItems = [
    { key: 'whitelist', label: 'Whitelist', href: '/admin/whitelist', icon: <UserRoundPlus className="h-4 w-4" /> },
    { key: 'admins', label: 'Admins', href: '/admin/admins', icon: <UserCog className="h-4 w-4" /> },
    { key: 'logs', label: 'Edit Log', href: '/admin/editlog', icon: <Shield className="h-4 w-4" /> },
  ] as const

  if (!isVisible) {
    return null
  }

  async function reloadAdminData() {
    try {
      const [accessControlData, auditLogData] = await Promise.all([apiClient.getAccessControl(), apiClient.getAuditLogs()])
      setAccessControl(accessControlData)
      setLogs(auditLogData.items)
      setError('')
    } catch {
      setError('Could not load admin data.')
    }
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

  async function handleUndoDelete(item: AuditLogItem) {
    setIsSaving(true)
    setError('')

    try {
      await apiClient.undoDeleteLogEntry(item.assignmentId)
      await reloadAdminData()
    } catch (errorValue) {
      setError(extractErrorMessage(errorValue, 'Could not undo the delete action.'))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <>
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
              <AppLink to="/">
                <ArrowLeft className="h-4 w-4" />
                Back
              </AppLink>
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
                        <button
                          key={`${item.timestamp}-${item.assignmentId}-${item.action}`}
                          type="button"
                          className={`w-full rounded-[0.8rem] border-2 px-3 py-2 text-left text-sm ${logColors[item.action] ?? logColors.edit}`}
                          onClick={() => setSelectedLog(item)}
                        >
                          <div className="flex items-start justify-between gap-3">
                            <div>
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
                            {item.action === 'delete' ? (
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                disabled={isSaving}
                                onClick={(event) => {
                                  event.stopPropagation()
                                  void handleUndoDelete(item)
                                }}
                              >
                                <RotateCcw className="h-4 w-4" />
                                Undo
                              </Button>
                            ) : null}
                          </div>
                        </button>
                      ))}
                    </div>
                  </div>
                ) : null}
              </CardContent>
            </Card>
          </div>
        </main>
      </div>

      {selectedLog ? <LogDetailDialog item={selectedLog} onClose={() => setSelectedLog(null)} /> : null}
    </>
  )
}

type LogDetailDialogProps = {
  item: AuditLogItem
  onClose: () => void
}

function LogDetailDialog({ item, onClose }: LogDetailDialogProps) {
  const changeEntries = Object.entries(item.changes)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/20 p-4 backdrop-blur-sm" onClick={onClose}>
      <div
        className="w-full max-w-3xl rounded-[1.5rem] border-[3px] border-slate-900 bg-[#f8f6f2] p-5 shadow-soft"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Log Detail</div>
            <h2 className="mt-1 text-2xl font-semibold text-foreground">{item.title}</h2>
            <p className="mt-2 text-sm text-muted-foreground">
              {item.action.toUpperCase()} by {item.actorDisplayName} ({item.actorLogin})
            </p>
          </div>
          <Button type="button" variant="ghost" onClick={onClose}>
            <X className="h-4 w-4" />
          </Button>
        </div>

        {item.action === 'edit' && changeEntries.length > 0 ? (
          <div className="mt-5 space-y-3">
            {changeEntries.map(([field, value]) => (
              <div key={field} className="rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-3 text-sm">
                <div className="font-semibold text-foreground">{field}</div>
                <div className="mt-2 grid gap-3 sm:grid-cols-2">
                  <div>
                    <div className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">Before</div>
                    <div className="mt-1 whitespace-pre-wrap text-slate-700">{value.before || '-'}</div>
                  </div>
                  <div>
                    <div className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">After</div>
                    <div className="mt-1 whitespace-pre-wrap text-slate-700">{value.after || '-'}</div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : null}

        {item.action === 'delete' ? (
          <div className="mt-5 rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-3 text-sm">
            <div className="font-semibold text-foreground">Deleted snapshot</div>
            <pre className="mt-3 overflow-x-auto whitespace-pre-wrap text-xs text-slate-700">
              {JSON.stringify(item.snapshot, null, 2)}
            </pre>
          </div>
        ) : null}

        {item.action === 'add' ? (
          <div className="mt-5 rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-3 text-sm text-slate-700">
            Added assignment due {formatSwissDateAndTime(item.dueDate, item.dueTime)}.
          </div>
        ) : null}

        {item.action === 'undo' ? (
          <div className="mt-5 rounded-[0.8rem] border-2 border-slate-900 bg-white px-3 py-3 text-sm text-slate-700">
            Restored assignment due {formatSwissDateAndTime(item.dueDate, item.dueTime)}.
          </div>
        ) : null}
      </div>
    </div>
  )
}

const logColors: Record<string, string> = {
  add: 'border-green-700 bg-green-50 text-green-900',
  edit: 'border-blue-700 bg-blue-50 text-blue-900',
  delete: 'border-red-700 bg-red-50 text-red-900',
  undo: 'border-emerald-700 bg-emerald-50 text-emerald-900',
}

function extractErrorMessage(errorValue: unknown, fallback: string): string {
  return errorValue instanceof ApiError && errorValue.payload && 'error' in errorValue.payload
    ? String((errorValue.payload as { error?: { message?: string } }).error?.message ?? fallback)
    : fallback
}
