export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
}

export interface UseCursorListOptions<T> {
  fetchPage: (cursor: string | undefined) => Promise<CursorPage<T>>
}

/**
 * A list read page by page through an opaque cursor. A failed page does not throw: it is kept in
 * `error` (cleared by the next call), the items already loaded stay, and `loadMore` retries the
 * same page. `hasMore` is false until the first page has loaded.
 */
export function useCursorList<T extends { id: string | number }>(options: UseCursorListOptions<T>) {
  const items = ref<T[]>([]) as Ref<T[]>
  const nextCursor = ref<string | null>(null)
  const loaded = ref(false)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref<unknown>(null)
  let generation = 0

  const hasMore = computed(() => loaded.value && nextCursor.value !== null)

  async function loadMore() {
    if (loading.value || loadingMore.value) return
    if (loaded.value && nextCursor.value === null) return

    const isFirstPage = !loaded.value
    const startedIn = generation
    if (isFirstPage) loading.value = true
    else loadingMore.value = true
    error.value = null

    try {
      const page = await options.fetchPage(nextCursor.value ?? undefined)
      if (startedIn !== generation) return

      const knownIds = new Set(items.value.map((item) => item.id))
      items.value = [...items.value, ...page.items.filter((item) => !knownIds.has(item.id))]
      nextCursor.value = page.nextCursor
      loaded.value = true
    } catch (caught) {
      if (startedIn === generation) error.value = caught
    } finally {
      if (startedIn === generation) {
        loading.value = false
        loadingMore.value = false
      }
    }
  }

  async function reset() {
    generation += 1
    items.value = []
    nextCursor.value = null
    loaded.value = false
    loading.value = false
    loadingMore.value = false
    error.value = null
    await loadMore()
  }

  return { items, loading, loadingMore, hasMore, error, loadMore, reset }
}
