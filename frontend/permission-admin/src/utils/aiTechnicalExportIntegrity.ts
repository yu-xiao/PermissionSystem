export const maxTechnicalExportBytes = 16 * 1024 * 1024

export function supportsLocalExportIntegrity() {
  return (
    globalThis.isSecureContext === true && typeof globalThis.crypto?.subtle?.digest === 'function'
  )
}

export async function hashTechnicalExportFile(file: Pick<File, 'size' | 'arrayBuffer'>) {
  if (!supportsLocalExportIntegrity())
    throw new Error('当前环境无法进行本地核对，请使用 HTTPS 安全环境。')
  if (file.size <= 0 || file.size > maxTechnicalExportBytes)
    throw new Error('请选择非空且不超过 16 MiB 的文件。')
  const bytes = await file.arrayBuffer()
  if (bytes.byteLength !== file.size) throw new Error('文件字节数已变化，请重新选择。')
  const digest = await globalThis.crypto.subtle.digest('SHA-256', bytes)
  return {
    bytes: bytes.byteLength,
    sha256: Array.from(new Uint8Array(digest), (value) => value.toString(16).padStart(2, '0'))
      .join('')
      .toUpperCase(),
  }
}
