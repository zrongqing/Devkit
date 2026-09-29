import { defineAsyncComponent, type Component } from 'vue'
import type { MenuDefinition } from './menuDefinition'
import type { MenuDeclaration } from '../types/navigation'
import UnavailablePageView from '../views/system/UnavailablePageView.vue'

const modules = import.meta.glob<{ default: MenuDefinition[] }>('/src/**/menu.ts', { eager: true })
const definitions = Object.values(modules).flatMap(x => x.default)
const byCode = new Map<string, MenuDefinition>()
const byRoute = new Map<string, MenuDefinition>()
const pages = new Map<string, Component>()
for (const menu of definitions) {
  if ((menu.menuCode !== menu.menuCode.trim() || menu.menuCode.length > 120 || !/^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*$/.test(menu.menuCode)) || byCode.has(menu.menuCode)) throw new Error(`菜单编码无效或重复：${menu.menuCode}`)
  if (menu.type === 'directory' && (menu.routeKey || menu.loader)) throw new Error(`目录不能关联页面：${menu.menuCode}`)
  if (menu.type === 'module') {
    if (!menu.routeKey || !menu.loader || byRoute.has(menu.routeKey)) throw new Error(`页面声明无效或重复：${menu.menuCode}`)
    byRoute.set(menu.routeKey, menu)
    pages.set(menu.routeKey, defineAsyncComponent(menu.loader))
  }
  byCode.set(menu.menuCode, menu)
}
for (const menu of definitions) {
  const seen = new Set([menu.menuCode])
  let parent = menu.parentCode
  while (parent) {
    const node = byCode.get(parent)
    if (!node || node.type !== 'directory' || seen.has(parent) || seen.size >= 32) throw new Error(`菜单层级无效：${menu.menuCode}`)
    seen.add(parent)
    parent = node.parentCode
  }
}
export const menuManifest: MenuDeclaration[] = definitions.map(({ loader: _loader, ...declaration }) => declaration)
export const getPageDefinition = (routeKey: string) => byRoute.get(routeKey)
export const hasRegisteredPage = (routeKey: string) => byRoute.has(routeKey)
export const resolvePageComponent = (routeKey: string): Component => pages.get(routeKey) ?? UnavailablePageView
