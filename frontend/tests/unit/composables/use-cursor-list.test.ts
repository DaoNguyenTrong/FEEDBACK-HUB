import { describe, expect, it, vi } from 'vitest'
import { useCursorList } from '@/composables/use-cursor-list'

type Item = { id: number }

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((res) => {
    resolve = res
  })
  return { promise, resolve }
}

describe('useCursorList', () => {
  it('loads the first page, then the next one with nextCursor', async () => {
    const fetchPage = vi
      .fn<(cursor: string | undefined) => Promise<{ items: Item[]; nextCursor: string | null }>>()
      .mockResolvedValueOnce({ items: [{ id: 3 }, { id: 2 }], nextCursor: 'c1' })
      .mockResolvedValueOnce({ items: [{ id: 1 }], nextCursor: null })
    const list = useCursorList({ fetchPage })

    await list.loadMore()
    expect(fetchPage).toHaveBeenLastCalledWith(undefined)
    expect(list.items.value.map((i) => i.id)).toEqual([3, 2])
    expect(list.hasMore.value).toBe(true)

    await list.loadMore()
    expect(fetchPage).toHaveBeenLastCalledWith('c1')
    expect(list.items.value.map((i) => i.id)).toEqual([3, 2, 1])
  })

  it('stops when nextCursor is null and skips duplicated ids', async () => {
    const fetchPage = vi
      .fn<(cursor: string | undefined) => Promise<{ items: Item[]; nextCursor: string | null }>>()
      .mockResolvedValueOnce({ items: [{ id: 2 }, { id: 1 }], nextCursor: 'c1' })
      .mockResolvedValueOnce({ items: [{ id: 1 }, { id: 0 }], nextCursor: null })
    const list = useCursorList({ fetchPage })

    await list.loadMore()
    await list.loadMore()
    expect(list.items.value.map((i) => i.id)).toEqual([2, 1, 0])
    expect(list.hasMore.value).toBe(false)

    await list.loadMore()
    expect(fetchPage).toHaveBeenCalledTimes(2)
  })

  it('separates loading (first page) from loadingMore', async () => {
    const first = deferred<{ items: Item[]; nextCursor: string | null }>()
    const second = deferred<{ items: Item[]; nextCursor: string | null }>()
    const fetchPage = vi.fn().mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise)
    const list = useCursorList<Item>({ fetchPage })

    const pendingFirst = list.loadMore()
    expect(list.loading.value).toBe(true)
    expect(list.loadingMore.value).toBe(false)
    first.resolve({ items: [{ id: 2 }], nextCursor: 'c1' })
    await pendingFirst

    const pendingSecond = list.loadMore()
    expect(list.loading.value).toBe(false)
    expect(list.loadingMore.value).toBe(true)
    second.resolve({ items: [{ id: 1 }], nextCursor: null })
    await pendingSecond
    expect(list.loadingMore.value).toBe(false)
  })

  it('keeps the error, keeps the items and retries the same page', async () => {
    const failure = new Error('boom')
    const fetchPage = vi
      .fn<(cursor: string | undefined) => Promise<{ items: Item[]; nextCursor: string | null }>>()
      .mockResolvedValueOnce({ items: [{ id: 2 }], nextCursor: 'c1' })
      .mockRejectedValueOnce(failure)
      .mockResolvedValueOnce({ items: [{ id: 1 }], nextCursor: null })
    const list = useCursorList({ fetchPage })

    await list.loadMore()
    await list.loadMore()
    expect(list.error.value).toBe(failure)
    expect(list.items.value.map((i) => i.id)).toEqual([2])
    expect(list.hasMore.value).toBe(true)

    await list.loadMore()
    expect(fetchPage).toHaveBeenLastCalledWith('c1')
    expect(list.error.value).toBeNull()
    expect(list.items.value.map((i) => i.id)).toEqual([2, 1])
  })

  it('has no more pages after a failed first load', async () => {
    const fetchPage = vi.fn().mockRejectedValueOnce(new Error('boom'))
    const list = useCursorList<Item>({ fetchPage })

    await list.loadMore()
    expect(list.error.value).not.toBeNull()
    expect(list.hasMore.value).toBe(false)
  })

  it('reset drops the items and the cursor, then loads the first page again', async () => {
    const fetchPage = vi
      .fn<(cursor: string | undefined) => Promise<{ items: Item[]; nextCursor: string | null }>>()
      .mockResolvedValueOnce({ items: [{ id: 2 }], nextCursor: 'c1' })
      .mockResolvedValueOnce({ items: [{ id: 9 }], nextCursor: null })
    const list = useCursorList({ fetchPage })

    await list.loadMore()
    await list.reset()

    expect(fetchPage).toHaveBeenLastCalledWith(undefined)
    expect(list.items.value.map((i) => i.id)).toEqual([9])
    expect(list.hasMore.value).toBe(false)
  })

  it('ignores a page that answers after reset', async () => {
    const stale = deferred<{ items: Item[]; nextCursor: string | null }>()
    const fetchPage = vi
      .fn()
      .mockReturnValueOnce(stale.promise)
      .mockResolvedValueOnce({ items: [{ id: 9 }], nextCursor: null })
    const list = useCursorList<Item>({ fetchPage })

    const pending = list.loadMore()
    await list.reset()
    stale.resolve({ items: [{ id: 1 }], nextCursor: 'old' })
    await pending

    expect(list.items.value.map((i) => i.id)).toEqual([9])
    expect(list.hasMore.value).toBe(false)
  })
})
