import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useNotificationsStore } from '../notifications'

describe('notifications store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
  })

  it('merges notifications with the same group key', () => {
    const store = useNotificationsStore()

    store.success('Saved', { groupKey: 'save' })
    store.success('Saved', { groupKey: 'save' })

    expect(store.notifications).toHaveLength(1)
    expect(store.notifications[0]?.count).toBe(2)
  })

  it('removes notifications after the timeout', () => {
    const store = useNotificationsStore()

    store.info('Hello', { duration: 1000 })
    expect(store.notifications).toHaveLength(1)

    vi.advanceTimersByTime(1000)

    expect(store.notifications).toHaveLength(0)
  })
})
