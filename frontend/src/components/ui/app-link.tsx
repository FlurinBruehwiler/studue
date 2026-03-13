import type { AnchorHTMLAttributes, MouseEvent, ReactNode } from 'react'

import { navigateTo } from '@/lib/router'

type AppLinkProps = AnchorHTMLAttributes<HTMLAnchorElement> & {
  to: string
  children: ReactNode
}

export function AppLink({ to, onClick, target, children, ...props }: AppLinkProps) {
  function handleClick(event: MouseEvent<HTMLAnchorElement>) {
    onClick?.(event)

    if (
      event.defaultPrevented ||
      target === '_blank' ||
      event.button !== 0 ||
      event.metaKey ||
      event.ctrlKey ||
      event.shiftKey ||
      event.altKey
    ) {
      return
    }

    event.preventDefault()
    navigateTo(to)
  }

  return (
    <a href={to} target={target} onClick={handleClick} {...props}>
      {children}
    </a>
  )
}
