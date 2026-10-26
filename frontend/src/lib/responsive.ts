/**
 * Media queries for `useMediaQuery`, aligned with Tailwind's breakpoints — a hair below each so they never
 * overlap the matching `lg:`/`sm:` class.
 */

/** Below Tailwind's `lg` (64rem): the sidebar is a drawer. */
export const BELOW_LG_QUERY = '(max-width: 63.9999rem)'

/** Below 768 px: the phone layout. */
export const PHONE_QUERY = '(max-width: 47.9999rem)'

/** From 1200 px: the desktop layout; between the two is the tablet. */
export const DESKTOP_QUERY = '(min-width: 75rem)'
