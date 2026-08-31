import { describe, expect, it } from 'vitest'
import { createSingleFlight } from './singleFlight'

describe('createSingleFlight', () => {
  it('shares concurrent work and allows a later retry', async () => {
    let calls = 0
    const flight = createSingleFlight(async () => {
      calls += 1
      await Promise.resolve()
      return calls
    })

    const first = await Promise.all([flight(), flight(), flight()])
    expect(first).toEqual([1, 1, 1])
    expect(await flight()).toBe(2)
  })
})
