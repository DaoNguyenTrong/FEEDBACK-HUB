export function useControlSize(): ComputedRef<'medium' | 'large'> {
  const { isPhone } = useViewport()
  return computed(() => (isPhone.value ? 'large' : 'medium'))
}
