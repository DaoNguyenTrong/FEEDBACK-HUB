import { vi } from 'vitest'

type Listener = (event: { matches: boolean }) => void

const WIDTH_QUERY = /^\(\s*(min|max)-width:\s*([\d.]+)rem\s*\)$/

function matchesWidth(query: string, width: number): boolean | null {
  const parsed = WIDTH_QUERY.exec(query)
  if (!parsed) return null
  const limit = Number(parsed[2]) * 16
  return parsed[1] === 'min' ? width >= limit : width <= limit
}

// Answers every min-width/max-width query for `width` and notifies listeners on change; other queries
// (colour scheme, hover) get an inert list so code reading them does not throw.
export function stubViewport(width: number): { setWidth(next: number): void } {
  let current = width
  const lists = new Set<{ query: string; matches: boolean; listeners: Set<Listener> }>()

  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => {
      const listeners = new Set<Listener>()
      const entry = {
        query,
        listeners,
        get matches() {
          return matchesWidth(query, current) ?? false
        },
      }
      lists.add(entry)
      return {
        media: query,
        get matches() {
          return entry.matches
        },
        addEventListener: (_: string, listener: Listener) => listeners.add(listener),
        removeEventListener: (_: string, listener: Listener) => listeners.delete(listener),
      }
    }),
  )

  return {
    setWidth(next: number) {
      current = next
      lists.forEach((entry) => {
        if (matchesWidth(entry.query, next) === null) return
        entry.listeners.forEach((listener) => listener({ matches: entry.matches }))
      })
    },
  }
}
