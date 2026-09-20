export interface WebNavigationMenuItem {
  id: string
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
