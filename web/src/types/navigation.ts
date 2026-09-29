export interface WebNavigationMenuItem {
  id: string
  menuCode: string
  type: "module" | "directory"
  parentId: string | null
  title: string
  routeKey: string
  iconKey: string | null
  order: number
  isClosable: boolean
}

export interface NavigationMenuNode extends WebNavigationMenuItem {
  children: NavigationMenuNode[]
}

export interface OpenTab {
  id: string
  title: string
  routeKey: string
  isClosable: boolean
}

export interface MenuDeclaration {
  menuCode: string
  type: 'module' | 'directory'
  parentCode: string | null
  title: string
  routeKey: string | null
  iconKey: string | null
  order: number
  requiredPermissions: string[]
  isClosable: boolean
}
export interface MenuView extends MenuDeclaration {
  enabled: boolean
  isPublic: boolean
  source: 'frontend' | 'manual'
  revision: number
  lastDeclaration: MenuDeclaration | null
}
export interface MenuTree { version: number; menus: MenuView[] }
export interface MenuDifference {
  menuCode: string
  status: 'new' | 'synced' | 'changed' | 'missing' | 'manual'
  local: MenuDeclaration | null
  current: MenuView | null
}
export interface MenuComparison { version: number; items: MenuDifference[] }
