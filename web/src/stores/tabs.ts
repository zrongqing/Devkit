import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { OpenTab, WebNavigationMenuItem } from '../types/navigation'
import { fallbackHome } from './navigation'

export const useTabsStore = defineStore('tabs', () => {
  const tabs = ref<OpenTab[]>([toTab(fallbackHome)])
  const activeId = ref('home')

  function reset() {
    tabs.value = [toTab(fallbackHome)]
    activeId.value = 'home'
  }

  function open(menu: WebNavigationMenuItem) {
    let tab = tabs.value.find((item) => item.id === menu.id)
    if (!tab) {
      tab = toTab(menu)
      tabs.value.push(tab)
    } else {
      tab.title = menu.title
      tab.routeKey = menu.routeKey
    }
    activeId.value = tab.id
    return tab
  }

  function openUnavailable(routeKey: string) {
    return open({
      id: `unavailable:${routeKey}`,
      parentId: null,
      title: '页面尚未部署',
      routeKey,
      iconKey: null,
      order: Number.MAX_SAFE_INTEGER,
      isClosable: true,
    })
  }

  function activate(id: string) {
    if (tabs.value.some((item) => item.id === id)) {
      activeId.value = id
    }
  }

  function close(id: string) {
    const index = tabs.value.findIndex((item) => item.id === id)
    if (index < 0 || !tabs.value[index].isClosable) {
      return tabs.value.find((item) => item.id === activeId.value) ?? tabs.value[0]
    }

    const wasActive = activeId.value === id
    tabs.value.splice(index, 1)
    if (wasActive) {
      const next = tabs.value[Math.max(0, index - 1)] ?? tabs.value[0]
      activeId.value = next.id
      return next
    }
    return tabs.value.find((item) => item.id === activeId.value) ?? tabs.value[0]
  }

  function find(id: string) {
    return tabs.value.find((item) => item.id === id)
  }

  return { tabs, activeId, reset, open, openUnavailable, activate, close, find }
})

function toTab(menu: WebNavigationMenuItem): OpenTab {
  return {
    id: menu.id,
    title: menu.title,
    routeKey: menu.routeKey,
    isClosable: menu.id === 'home' ? false : menu.isClosable,
  }
}
