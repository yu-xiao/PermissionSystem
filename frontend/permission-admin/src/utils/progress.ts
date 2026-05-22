import NProgress from 'nprogress'
import 'nprogress/nprogress.css'

NProgress.configure({
  showSpinner: false,
  trickleSpeed: 120,
  minimum: 0.12,
})

let progressCount = 0

export function startProgress() {
  progressCount += 1
  NProgress.start()
}

export function doneProgress() {
  progressCount = Math.max(0, progressCount - 1)

  if (progressCount === 0) {
    NProgress.done()
  }
}

export function resetProgress() {
  progressCount = 0
  NProgress.done(true)
}
