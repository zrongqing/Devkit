import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getWebNavigationMenus } from '../api/navigation'
import type { NavigationMenuNode, WebNavigationMenuItem } from '../types/navigation'

export const fallbackHome: WebNavigationMenuItem = {
  id: 'home',
  parentId: null,
  title: '首页',
  routeKey: 'home',
  iconKey: 'home',
  order: 0,
  isClosable: false,
}

export const useNavigationStore = defineStore('navigation', () => {
  const items = ref<WebNavigationMenuItem[]>([fallbackHome])
  const loading = ref(false)
  const loaded = ref(false)
  const error = ref<string>()

  const nodes = computed(() => buildTree(items.value))

  async function load(force = false) {
    if ((loaded.value && !force) || loading.value) {
      return
    }

    loading.value = true
    error.value = undefined
    try {
      const remoteItems = await getWebNavigationMenus()
      const remoteHome = remoteItems.find((item) => item.id.toLowerCase() === 'home')
      const normalizedHome = remoteHome
        ? { ...remoteHome, id: 'home', routeKey: 'home', parentId: null, isClosable: false }
        : fallbackHome
      items.value = [
        normalizedHome,
        ...remoteItems.filter((item) => item.id.toLowerCase() !== 'home'),
      ]
      loaded.value = true
    } catch (reason) {
      items.value = [fallbackHome]
      error.value = reason instanceof Error ? reason.message : '菜单加载失败，请稍后重试。'
    } finally {
      loading.value = false
    }
  }

  function findById(id: string) {
    return items.value.find((item) => item.id === id)
  }

  function findByRouteKey(routeKey: string) {
    return items.value.find((item) => item.routeKey === routeKey && item.routeKey.length > 0)
  }

  return { items, nodes, loading, loaded, error, load, findById, findByRouteKey }
})

function buildTree(items: WebNavigationMenuItem[]): NavigationMenuNode[] {
  const nodes = new Map<string, NavigationMenuNode>()
  for (const item of items) {
    nodes.set(item.id, { ...item, children: [] })
  }

  const roots: NavigationMenuNode[] = []
  for (const node of nodes.values()) {
    const parent = node.parentId ? nodes.get(node.parentId) : undefined
    if (parent) {
      parent.children.push(node)
    } else {
      roots.push(node)
    }
  }

  const sortNodes = (entries: NavigationMenuNode[]) => {
    entries.sort((left, right) => left.order - right.order || left.title.localeCompare(right.title, 'zh-CN'))
    entries.forEach((entry) => sortNodes(entry.children))
  }
  sortNodes(roots)
  return roots
}
