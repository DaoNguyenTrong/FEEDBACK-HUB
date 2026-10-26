// Keyboard access (WAI-ARIA menu pattern): the popover traps no focus and is teleported to the end of
// <body>, so focus is moved in on open, roved with the arrow keys, and handed back to the trigger on close.
export function useMenuKeyboard(
  menuEl: Readonly<Ref<HTMLElement | null>>,
  triggerEl: Readonly<Ref<HTMLElement | null>>,
  open: Ref<boolean>,
) {
  function menuItems() {
    return Array.from(
      menuEl.value?.querySelectorAll<HTMLElement>('[role="menuitem"], [role="menuitemradio"]') ??
        [],
    )
  }

  watch(open, async (isOpen) => {
    if (isOpen) {
      await nextTick()
      menuItems()[0]?.focus()
    } else if (menuEl.value?.contains(document.activeElement)) {
      // Closed by an item (profile, logout, an organization): focus would otherwise fall to <body>.
      // A click elsewhere has already moved focus out, so it is left alone.
      triggerEl.value?.focus()
    }
  })

  function closeToTrigger() {
    triggerEl.value?.focus()
    open.value = false
  }

  function onKeydown(event: KeyboardEvent) {
    const items = menuItems()
    const index = items.indexOf(document.activeElement as HTMLElement)
    let next: HTMLElement | undefined

    switch (event.key) {
      case 'ArrowDown':
        next = items[(index + 1) % items.length]
        break
      case 'ArrowUp':
        next = items[index <= 0 ? items.length - 1 : index - 1]
        break
      case 'Home':
        next = items[0]
        break
      case 'End':
        next = items[items.length - 1]
        break
      case 'Escape':
        // Prevented, so the drawer's window-level Esc leaves its own state alone.
        event.preventDefault()
        closeToTrigger()
        return
      case 'Tab':
        // Not prevented: focus is back on the trigger before the browser acts, so Tab moves on from there.
        closeToTrigger()
        return
      default:
        return
    }

    event.preventDefault()
    next?.focus()
  }

  return { onKeydown }
}
