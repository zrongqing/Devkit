import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getPageDefinition } from '../router/pageRegistry'
import { session } from '../api/session'
import { getWebNavigationMenus } from '../api/navigation'
import type { NavigationMenuNode, WebNavigationMenuItem } from '../types/navigation'

export const fallbackHome: WebNavigationMenuItem = {
  id: 'home',
  menuCode: 'home',
  type: 'module',
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

  let generation = 0
  let loadedUser: string | undefined
  let pending: Promise<void> | undefined
  async function load(force = false): Promise<void> {
    const user = session.value?.user.id ?? ''
    if (pending && !force) return pending
    if (loaded.value && !force && loadedUser === user) return
    const sequence = ++generation
    loading.value = true
    error.value = undefined
    const operation = (async () => {
      try {
        const remote = await getWebNavigationMenus()
        if (sequence !== generation || user !== (session.value?.user.id ?? '')) return
        let visible = remote.filter(item => item.type === 'directory' || getPageDefinition(item.routeKey)?.menuCode === item.menuCode)
        let changed = true
        while (changed) {
          const next = visible.filter(item => (!item.parentId || visible.some(p => p.id === item.parentId))
            && (item.type === 'module' || visible.some(child => child.parentId === item.id)))
          changed = next.length !== visible.length
          visible = next
        }
        items.value = visible.map(item => item.menuCode === 'home' ? { ...item, isClosable: false } : item)
        loaded.value = true
        loadedUser = user
      } catch (reason) {
        if (sequence !== generation) return
        items.value = [fallbackHome]
        loaded.value = false
        error.value = reason instanceof Error ? reason.message : '菜单加载失败，请稍后重试。'
      } finally {
        if (sequence === generation) { loading.value = false; pending = undefined }
      }
    })()
    pending = operation
    await operation
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
