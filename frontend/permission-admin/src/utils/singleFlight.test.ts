import { describe, expect, it, vi } from 'vitest'
import { createSingleFlight } from './singleFlight'

describe('createSingleFlight', () => {
  it('shares concurrent work and allows a later retry', async () => {
    let resolve!: (value: string) => void
    const factory = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise<string>((done) => {
            resolve = done
          }),
      )
      .mockResolvedValueOnce('next-token')
    const run = createSingleFlight(factory)

    const first = run()
    const second = run()
    expect(first).toBe(second)
    expect(factory).toHaveBeenCalledTimes(1)

    resolve('token')
    await expect(first).resolves.toBe('token')
    await expect(run()).resolves.toBe('next-token')
    expect(factory).toHaveBeenCalledTimes(2)
  })
})
