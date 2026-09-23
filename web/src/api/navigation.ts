import type { ApiResponse } from './types'
import { authorizedFetch } from './session'
import type { WebNavigationMenuItem } from '../types/navigation'

export async function getWebNavigationMenus(signal?: AbortSignal): Promise<WebNavigationMenuItem[]> {
  const response = await authorizedFetch('/api/v1/web/navigation/menus', { signal })
  if (!response.ok) {
    throw new Error(`菜单加载失败（${response.status}）`)
  }

  const body = (await response.json()) as ApiResponse<WebNavigationMenuItem[]>
  return body.data
}
