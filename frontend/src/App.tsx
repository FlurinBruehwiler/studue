import { AdminPanel } from '@/components/admin-panel'
import { useAuth } from '@/hooks/use-auth'
import { usePathname } from '@/lib/router'
import { OverviewPage } from '@/pages/overview-page'

function App() {
  const auth = useAuth()
  const path = usePathname()

  if (path === '/admin' || path === '/admin/whitelist' || path === '/admin/admins' || path === '/admin/editlog') {
    return <AdminPanel isVisible={auth.authenticated && Boolean(auth.user?.isAdmin)} />
  }

  return <OverviewPage auth={auth} />
}

export default App
