import { expect, test } from '@playwright/test'

test('anonymous users are redirected from protected routes', async ({ page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL(/\/login\?redirect=%2Fdashboard/)
})

test('public login page is reachable', async ({ page }) => {
  await page.goto('/login')
  await expect(page.locator('body')).toBeVisible()
})
