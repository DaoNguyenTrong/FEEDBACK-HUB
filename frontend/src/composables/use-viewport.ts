export function useViewport() {
  return {
    isPhone: useMediaQuery(PHONE_QUERY),
    isDesktop: useMediaQuery(DESKTOP_QUERY),
  }
}
