export function createSingleFlight<T>(factory: () => Promise<T>) {
  let inFlight: Promise<T> | undefined

  return () => {
    if (!inFlight) {
      inFlight = factory().finally(() => {
        inFlight = undefined
      })
    }

    return inFlight
  }
}
