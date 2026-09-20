import type { ApiResponse } from './types'
import type { WebNavigationMenuItem } from '../types/navigation'

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:12511').replace(/\/$/, '')

export async function getWebNavigationMenus(signal?: AbortSignal): Promise<WebNavigationMenuItem[]> {
  const response = await fetch(`${baseUrl}/api/v1/web/navigation/menus`, { signal })
  if (!response.ok) {
    throw new Error(`菜单加载失败（${response.status}）`)
  }

  const body = (await response.json()) as ApiResponse<WebNavigationMenuItem[]>
  return body.data
}
