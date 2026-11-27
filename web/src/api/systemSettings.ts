import { endpoint } from './endpoints'
import { request } from './session'
import type { ApiResponse } from './types'

export interface SystemSettings {
  timeZone: string | null
}

const path = '/api/v1/identity/system-settings'
export const manageSystemSettings = 'system.settings.manage'

export async function getSystemSettings(signal: AbortSignal): Promise<SystemSettings> {
  const response = await fetch(endpoint(path), { signal, cache: 'no-store' })
  if (!response.ok) throw new Error(`显示时区加载失败（${response.status}）。`)
  const body: ApiResponse<SystemSettings> = await response.json()
  return body.data
}

export function updateSystemSettings(settings: SystemSettings): Promise<SystemSettings> {
  return request(path, { method: 'PUT', body: JSON.stringify(settings) })
}
