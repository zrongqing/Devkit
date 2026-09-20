import type { Component } from 'vue'
import HomeView from '../views/system/HomeView.vue'
import SystemStatusView from '../views/system/SystemStatusView.vue'
import SettingsView from '../views/system/SettingsView.vue'
import AboutView from '../views/system/AboutView.vue'
import UnavailablePageView from '../views/system/UnavailablePageView.vue'

const pages: Record<string, Component> = {
  home: HomeView,
  'system-status': SystemStatusView,
  settings: SettingsView,
  about: AboutView,
}

export function resolvePageComponent(routeKey: string): Component {
  return pages[routeKey] ?? UnavailablePageView
}

export function hasRegisteredPage(routeKey: string): boolean {
  return routeKey in pages
}
