import { cn } from '@/helpers/lib/utils'

interface ConnectEdgeLogoProps {
  variant?: 'full' | 'icon'
  className?: string
  textClassName?: string
}

function LogoIcon({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 32 32"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={cn('h-8 w-8 shrink-0', className)}
      aria-hidden
    >
      <defs>
        <linearGradient id="ce-icon-grad" x1="4" y1="28" x2="28" y2="4" gradientUnits="userSpaceOnUse">
          <stop stopColor="#06b6d4" />
          <stop offset="1" stopColor="#6366f1" />
        </linearGradient>
      </defs>
      <circle cx="9" cy="23" r="3.5" fill="url(#ce-icon-grad)" />
      <circle cx="23" cy="9" r="3.5" fill="url(#ce-icon-grad)" />
      <path d="M11.5 20.5L20.5 11.5" stroke="url(#ce-icon-grad)" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="16" cy="16" r="2" fill="#22d3ee" />
    </svg>
  )
}

export function ConnectEdgeLogo({ variant = 'full', className, textClassName }: ConnectEdgeLogoProps) {
  if (variant === 'icon') {
    return <LogoIcon className={className} />
  }

  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <LogoIcon />
      <span className={cn('text-lg font-semibold tracking-tight text-white', textClassName)}>
        Connect<span className="bg-gradient-to-r from-cyan-400 to-indigo-400 bg-clip-text text-transparent">Edge</span>
      </span>
    </div>
  )
}
