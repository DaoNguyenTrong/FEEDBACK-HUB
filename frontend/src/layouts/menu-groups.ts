import type { Component } from 'vue'
import { Building, Server } from '@vicons/tabler'

export interface MenuGroup {
  id: string
  labelKey: string
  icon: Component
  order: number
}

// Ungrouped items (order below 100) form the main section; groups follow it.
export const MENU_GROUPS: MenuGroup[] = [
  { id: 'organization', labelKey: 'nav.groups.organization', icon: Building, order: 100 },
  { id: 'system', labelKey: 'nav.groups.system', icon: Server, order: 200 },
]
