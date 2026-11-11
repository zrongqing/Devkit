import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type { OpenTab, WebNavigationMenuItem } from '../types/navigation'
import { fallbackHome } from './navigation'

export function canCloseTab(tab: OpenTab) {
  return tab.isClosable && !tab.locked
}

export const useTabsStore = defineStore('tabs', () => {
  const opened = ref<OpenTab[]>([toTab(fallbackHome)])
  const tabs = computed(() => [
    ...opened.value.filter(tab => tab.locked),
    ...opened.value.filter(tab => !tab.locked),
  ])
  const activeId = ref('home')

  function reset() {
    opened.value = [toTab(fallbackHome)]
    activeId.value = 'home'
  }

  function open(menu: WebNavigationMenuItem) {
    let tab = find(menu.id)
    if (!tab) {
      tab = toTab(menu)
      opened.value.push(tab)
    } else {
      tab.title = menu.title
      tab.routeKey = menu.routeKey
      tab.menuCode = menu.menuCode
      tab.isClosable = menu.routeKey !== 'home' && menu.isClosable
    }
    activeId.value = tab.id
    return tab
  }

  function openUnavailable(routeKey: string) {
    return open({
      id: `unavailable:${routeKey}`,
      menuCode: routeKey,
      type: "module",
      parentId: null,
      title: '页面尚未部署',
      routeKey,
      iconKey: null,
      order: Number.MAX_SAFE_INTEGER,
      isClosable: true,
    })
  }

  function activate(id: string) {
    if (find(id)) {
      activeId.value = id
    }
  }

  function toggleLock(id: string) {
    const tab = find(id)
    if (tab && tab.routeKey !== 'home') tab.locked = !tab.locked
  }

  function close(id: string) {
    const tab = find(id)
    if (tab && canCloseTab(tab)) remove([id])
  }

  function tabsOnSide(id: string, side: 'left' | 'right') {
    const index = tabs.value.findIndex(tab => tab.id === id)
    if (index < 0) return []
    return (side === 'left' ? tabs.value.slice(0, index) : tabs.value.slice(index + 1))
      .filter(canCloseTab)
  }

  function closeSide(id: string, side: 'left' | 'right') {
    remove(tabsOnSide(id, side).map(tab => tab.id))
  }

  // Access changes can remove a locked tab as well.
  function remove(ids: string[]) {
    const keep = (tab: OpenTab) => tab.routeKey === 'home' || !ids.includes(tab.id)
    const previous = tabs.value
    const activeIndex = previous.findIndex(tab => tab.id === activeId.value)
    opened.value = opened.value.filter(keep)
    if (!find(activeId.value)) {
      const next = previous.slice(0, activeIndex).reverse().find(keep)
        ?? previous.slice(activeIndex + 1).find(keep)
      activeId.value = next?.id ?? 'home'
    }
  }

  function find(id: string) {
    return opened.value.find((item) => item.id === id)
  }

  return { tabs, activeId, reset, open, openUnavailable, activate, toggleLock, close, tabsOnSide, closeSide, remove, find }
})

function toTab(menu: WebNavigationMenuItem): OpenTab {
  return {
    id: menu.id,
    menuCode: menu.menuCode,
    title: menu.title,
    routeKey: menu.routeKey,
    isClosable: menu.routeKey !== 'home' && menu.isClosable,
    locked: menu.routeKey === 'home',
  }
}
