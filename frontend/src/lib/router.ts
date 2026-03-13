import { useEffect, useState } from 'react'

const NAVIGATION_EVENT = 'studue:navigate'

export function getCurrentPathname(): string {
  return window.location.pathname
}

export function navigateTo(pathname: string, options: { replace?: boolean } = {}): void {
  const currentPathname = getCurrentPathname()
  if (currentPathname === pathname) {
    return
  }

  if (options.replace) {
    window.history.replaceState({}, '', pathname)
  } else {
    window.history.pushState({}, '', pathname)
  }

  window.dispatchEvent(new Event(NAVIGATION_EVENT))
}

export function usePathname(): string {
  const [pathname, setPathname] = useState(getCurrentPathname)

  useEffect(() => {
    const syncPathname = () => {
      setPathname(getCurrentPathname())
    }

    window.addEventListener('popstate', syncPathname)
    window.addEventListener(NAVIGATION_EVENT, syncPathname)

    return () => {
      window.removeEventListener('popstate', syncPathname)
      window.removeEventListener(NAVIGATION_EVENT, syncPathname)
    }
  }, [])

  return pathname
}
