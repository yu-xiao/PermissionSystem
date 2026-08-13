import { readFile, readdir } from 'node:fs/promises'
import { join } from 'node:path'

const dist = process.argv[2] ?? 'dist'
const budget = JSON.parse(await readFile(join(process.cwd(), 'bundle-budget.json'), 'utf8'))
const files = (await readdir(join(process.cwd(), dist, 'assets'))).filter((file) => file.endsWith('.js'))
const sizes = await Promise.all(files.map(async (file) => ({ file, size: (await readFile(join(process.cwd(), dist, 'assets', file))).byteLength })))
const total = sizes.reduce((sum, item) => sum + item.size, 0)
const largest = sizes.reduce((current, item) => item.size > current.size ? item : current, { file: '', size: 0 })

if (total > budget.maxTotalBytes || largest.size > budget.maxChunkBytes) {
  throw new Error(`Frontend bundle budget exceeded: total=${total}, largest=${largest.file}:${largest.size}`)
}

console.log(`Frontend bundle budget passed: total=${total}, largest=${largest.file}:${largest.size}`)
