import type { ApiResponse, SystemInfo } from './types'

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/$/, '')

export async function getSystemInfo(signal?: AbortSignal): Promise<SystemInfo> {
  const response = await fetch(`${baseUrl}/api/v1/system/info`, { signal })
  if (!response.ok) {
    throw new Error(`System info request failed (${response.status})`)
  }

  const body = (await response.json()) as ApiResponse<SystemInfo>
  return body.data
}
