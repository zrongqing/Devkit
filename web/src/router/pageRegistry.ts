import type { Component } from 'vue'
import HomeView from '../views/system/HomeView.vue'
import SystemStatusView from '../views/system/SystemStatusView.vue'
import SettingsView from '../views/system/SettingsView.vue'
import AboutView from '../views/system/AboutView.vue'
import UnavailablePageView from '../views/system/UnavailablePageView.vue'
import { defineAsyncComponent } from 'vue'

const pages: Record<string, Component> = {
  home: HomeView,
  'system-status': SystemStatusView,
  settings: SettingsView,
  about: AboutView,
  'study-knowledge': defineAsyncComponent(() => import('../modules/exam-study/KnowledgeView.vue')),
  'study-projects': defineAsyncComponent(() => import('../modules/exam-study/ProjectsView.vue')),
  'study-questions': defineAsyncComponent(() => import('../modules/exam-study/QuestionsView.vue')),
  'study-jobs': defineAsyncComponent(() => import('../modules/exam-study/JobsView.vue')),
  'system-files': defineAsyncComponent(() => import('../modules/file-storage/FilesView.vue')),
  'system-storage': defineAsyncComponent(() => import('../modules/file-storage/StorageView.vue')),
  'system-identity': defineAsyncComponent(() => import('../modules/identity/IdentityView.vue')),
}

export function resolvePageComponent(routeKey: string): Component {
  return pages[routeKey] ?? UnavailablePageView
}

export function hasRegisteredPage(routeKey: string): boolean {
  return routeKey in pages
}
