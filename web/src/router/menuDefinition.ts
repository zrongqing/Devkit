import type { Component } from 'vue'
import type { MenuDeclaration } from '../types/navigation'
export type MenuDefinition = MenuDeclaration & { loader?: () => Promise<{ default: Component }> }
export function directory(menuCode: string, title: string, parentCode: string | null, order: number, iconKey: string | null = null): MenuDefinition {
  return { menuCode, title, parentCode, order, iconKey, type: 'directory', routeKey: null, requiredPermissions: [], isClosable: true }
}
export function page(menuCode: string, routeKey: string, title: string, parentCode: string | null, order: number, requiredPermissions: string[], loader: NonNullable<MenuDefinition['loader']>, iconKey: string | null = null): MenuDefinition {
  return { menuCode, title, parentCode, order, iconKey, type: 'module', routeKey, requiredPermissions, loader, isClosable: menuCode !== 'home' }
}
