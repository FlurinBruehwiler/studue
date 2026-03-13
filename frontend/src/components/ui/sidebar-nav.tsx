import type { ReactNode } from 'react'

import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type SidebarNavItem = {
  key: string
  label: string
  icon: ReactNode
  href: string
}

type SidebarNavProps = {
  items: SidebarNavItem[]
  activeKey: string
}

export function SidebarNav({ items, activeKey }: SidebarNavProps) {
  return (
    <nav className="flex flex-col gap-2">
      {items.map((item) => {
        const isActive = item.key === activeKey

        return (
          <Button
            key={item.key}
            type="button"
            variant={isActive ? 'secondary' : 'ghost'}
            className={cn(
              'w-full justify-start rounded-[0.8rem] border-2 border-transparent px-3 py-2 text-sm font-medium',
              isActive ? 'border-slate-900 bg-white text-foreground hover:bg-white' : 'text-muted-foreground hover:bg-white/70',
            )}
            asChild
          >
            <a href={item.href}>
              {item.icon}
              {item.label}
            </a>
          </Button>
        )
      })}
    </nav>
  )
}
