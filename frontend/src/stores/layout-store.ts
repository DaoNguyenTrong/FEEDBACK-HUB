export const useLayoutStore = defineStore('layout', () => {
  const mobileOpen = ref(false)
  const settingsOpen = ref(false)
  const paletteOpen = ref(false)
  const profileOpen = ref(false)
  const createOrganizationOpen = ref(false)

  function openMobile() {
    mobileOpen.value = true
  }
  function closeMobile() {
    mobileOpen.value = false
  }
  function openSettings() {
    settingsOpen.value = true
  }
  function closeSettings() {
    settingsOpen.value = false
  }
  function openPalette() {
    paletteOpen.value = true
  }
  function closePalette() {
    paletteOpen.value = false
  }
  function openProfile() {
    profileOpen.value = true
  }
  function closeProfile() {
    profileOpen.value = false
  }
  function openCreateOrganization() {
    createOrganizationOpen.value = true
  }
  function closeCreateOrganization() {
    createOrganizationOpen.value = false
  }

  return {
    mobileOpen,
    settingsOpen,
    paletteOpen,
    profileOpen,
    createOrganizationOpen,
    openMobile,
    closeMobile,
    openSettings,
    closeSettings,
    openPalette,
    closePalette,
    openProfile,
    closeProfile,
    openCreateOrganization,
    closeCreateOrganization,
  }
})
