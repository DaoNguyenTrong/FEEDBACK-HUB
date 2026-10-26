import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useLayoutStore } from '@/stores/layout-store'
import { setupTestPinia } from '../../helpers/pinia'

describe('useLayoutStore', () => {
  beforeEach(() => {
    localStorage.clear()
    setupTestPinia()
  })

  it('starts every panel closed and touches no storage', () => {
    const reads = vi.spyOn(Storage.prototype, 'getItem')
    const writes = vi.spyOn(Storage.prototype, 'setItem')

    const layout = useLayoutStore()
    layout.openMobile()
    layout.closeMobile()

    expect(reads).not.toHaveBeenCalled()
    expect(writes).not.toHaveBeenCalled()
    expect([
      layout.mobileOpen,
      layout.settingsOpen,
      layout.paletteOpen,
      layout.profileOpen,
      layout.createOrganizationOpen,
    ]).toEqual([false, false, false, false, false])
    vi.restoreAllMocks()
  })

  it('opens and closes the session-only surfaces', () => {
    const layout = useLayoutStore()

    layout.openMobile()
    layout.openSettings()
    layout.openPalette()
    layout.openProfile()
    layout.openCreateOrganization()
    expect([
      layout.mobileOpen,
      layout.settingsOpen,
      layout.paletteOpen,
      layout.profileOpen,
      layout.createOrganizationOpen,
    ]).toEqual([true, true, true, true, true])

    layout.closeMobile()
    layout.closeSettings()
    layout.closePalette()
    layout.closeProfile()
    layout.closeCreateOrganization()
    expect([
      layout.mobileOpen,
      layout.settingsOpen,
      layout.paletteOpen,
      layout.profileOpen,
      layout.createOrganizationOpen,
    ]).toEqual([false, false, false, false, false])
  })
})
