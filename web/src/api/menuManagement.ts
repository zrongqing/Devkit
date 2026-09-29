import { request } from './session'
import type { MenuDeclaration, MenuComparison, MenuTree } from '../types/navigation'
const base = '/api/v1/web/menu-management'
const json = (body: unknown, method = 'POST') => ({ method, body: JSON.stringify(body) })
export const menuApi = {
  list: () => request<MenuTree>(`${base}/menus`),
  compare: (menus: MenuDeclaration[]) => request<MenuComparison>(`${base}/compare`, json({ menus })),
  sync: (version: number, menus: MenuDeclaration[], items: { menuCode: string; applyFields: string[] }[]) => request<MenuTree>(`${base}/sync`, json({ version, menus, items })),
  createDirectory: (body: { version: number; menuCode: string; title: string; parentCode: string | null; iconKey: string | null; order: number }) => request<MenuTree>(`${base}/menus`, json(body)),
  edit: (code: string, body: { version: number; title: string; parentCode: string | null; iconKey: string | null; order: number; enabled: boolean }) => request<MenuTree>(`${base}/menus/${encodeURIComponent(code)}`, json(body, 'PUT')),
  removeDirectory: (code: string, version: number) => request<MenuTree>(`${base}/menus/${encodeURIComponent(code)}?version=${version}`, { method: 'DELETE' }),
  userMenus: (id: string, menuCodes: string[]) => request(`/api/v1/identity/users/${id}/menus`, json({ menuCodes }, 'PUT')),
  roleMenus: (id: string, menuCodes: string[]) => request(`/api/v1/identity/roles/${id}/menus`, json({ menuCodes }, 'PUT')),
}
