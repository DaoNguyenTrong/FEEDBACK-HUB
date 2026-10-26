import type { Component } from 'vue'

export interface SidebarMenuItem {
  labelKey: string
  icon: Component
  routeName?: string
  items?: SidebarMenuItem[]
}

export interface SidebarMenuGroup {
  kind: 'group'
  id: string
  labelKey: string
  icon: Component
  items: SidebarMenuItem[]
}

export type SidebarMenuLink = SidebarMenuItem & { kind: 'item' }

export type SidebarMenuEntry = SidebarMenuGroup | SidebarMenuLink

export interface FlatMenuLink {
  labelKey: string
  icon: Component
  routeName: string
  trailKeys: string[]
}

export function flattenMenuLinks(entries: SidebarMenuEntry[]): FlatMenuLink[] {
  const links: FlatMenuLink[] = []

  function visit(item: SidebarMenuItem, trailKeys: string[]) {
    if (item.items?.length) {
      item.items.forEach((child) => visit(child, [...trailKeys, item.labelKey]))
    } else if (item.routeName) {
      links.push({
        labelKey: item.labelKey,
        icon: item.icon,
        routeName: item.routeName,
        trailKeys,
      })
    }
  }

  for (const entry of entries) {
    if (entry.kind === 'group') {
      entry.items.forEach((item) => visit(item, [entry.labelKey]))
    } else {
      visit(entry, [])
    }
  }

  return links
}

export interface NavSection {
  key: string
  headingKey?: string
  links: FlatMenuLink[]
}

// Loose items next to each other share one section; each group is a section headed by its name.
export function toNavSections(entries: SidebarMenuEntry[]): NavSection[] {
  const sections: NavSection[] = []
  let openLoose: NavSection | null = null

  for (const entry of entries) {
    const links = flattenMenuLinks([entry])

    if (entry.kind === 'group') {
      sections.push({ key: entry.id, headingKey: entry.labelKey, links })
      openLoose = null
    } else if (openLoose) {
      openLoose.links.push(...links)
    } else {
      openLoose = { key: `loose-${sections.length}`, links }
      sections.push(openLoose)
    }
  }

  return sections
}

export function containsRoute(
  entry: SidebarMenuEntry | SidebarMenuItem,
  routeName: string | undefined,
): boolean {
  if (!routeName) {
    return false
  }

  if ('kind' in entry && entry.kind === 'group') {
    return entry.items.some((item) => containsRoute(item, routeName))
  }

  const item = entry as SidebarMenuItem

  return (
    item.routeName === routeName || Boolean(item.items?.some((c) => containsRoute(c, routeName)))
  )
}
