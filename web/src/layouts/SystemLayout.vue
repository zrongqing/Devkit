<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { ElMessage } from "element-plus";
import {
  ArrowLeft,
  Fold,
  Expand,
  Hide,
  Grid,
  Menu as MenuIcon,
  Refresh,
} from "@element-plus/icons-vue";
import { useRoute, useRouter } from "vue-router";
import RecursiveMenuItem from "../components/navigation/RecursiveMenuItem.vue";
import PageAccessView from "../components/PageAccessView.vue";
import { useNavigationStore } from "../stores/navigation";
import { useTabsStore } from "../stores/tabs";
import { session, saveSession, loadPermissions, permissionDenied } from "../api/session";
import { logout } from "../api/auth";

const route = useRoute();
const router = useRouter();
const navigation = useNavigationStore();
const tabs = useTabsStore();
type NavigationMode = "expanded" | "icons" | "hidden";
function restoreNavigation(): NavigationMode {
  try { const value = localStorage.getItem("devkit.navigation-mode"); return value === "icons" || value === "hidden" ? value : "expanded"; }
  catch { return "expanded"; }
}
const navigationMode = ref<NavigationMode>(restoreNavigation());
const collapsed = computed(() => navigationMode.value === "icons" && !mobileMenuOpen.value);
function setNavigationMode(mode: NavigationMode) {
  navigationMode.value = mode;
  mobileMenuOpen.value = false;
  try { localStorage.setItem("devkit.navigation-mode", mode); } catch { /* Storage can be unavailable. */ }
}
const mobileMenuOpen = ref(false);
const initialized = ref(false);
const refreshingPermissions = ref(false);

const openedDirectories = computed(() =>
  navigation.items.filter((item) => item.type === "directory").map((item) => item.id),
);

onMounted(async () => {
  tabs.reset();
  if (session.value) {
    try {
      await loadPermissions();
    } catch {
      saveSession(null);
    }
  }
  await navigation.load(true);
  const home = navigation.findByRouteKey("home");
  if (home) {
    tabs.open(home);
  }
  syncRoute(String(route.params.routeKey ?? "home"));
  initialized.value = true;
});

async function signOut() {
  const token = session.value?.refreshToken;
  saveSession(null);
  tabs.reset();
  await navigation.load(true);
  if (token) {
    try {
      await logout(token);
    } catch {
      /* Local session is already removed. */
    }
  }
  await router.replace("/login");
}

async function refreshPermissionCache() {
  if (!session.value || refreshingPermissions.value) return;
  refreshingPermissions.value = true;
  try {
    await loadPermissions(true);
    await navigation.load(true);
    const current = String(route.params.routeKey ?? "home");
    if (current !== "home" && !navigation.findByRouteKey(current)) await router.replace("/system/home");
    ElMessage.success("权限已更新");
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : "刷新权限失败，请稍后重试");
  } finally {
    refreshingPermissions.value = false;
  }
}

watch(
  () => route.params.routeKey,
  (routeKey) => {
    if (initialized.value) {
      syncRoute(String(routeKey ?? "home"));
    }
  },
);

watch(
  () => navigation.items,
  () => {
    if (!initialized.value) return;
    for (const tab of [...tabs.tabs]) {
      const menu = navigation.findByRouteKey(tab.routeKey);
      if (menu) tab.title = menu.title;
      else if (tab.routeKey !== "home") tabs.close(tab.id);
    }
    const active = tabs.find(tabs.activeId);
    if (active && !navigation.findByRouteKey(String(route.params.routeKey ?? "home")))
      navigateTo(active.routeKey, true);
  },
);
watch(
  () => session.value?.user.id,
  (id) => {
    if (!id) tabs.reset();
  },
);

function syncRoute(routeKey: string) {
  const menu = navigation.findByRouteKey(routeKey);
  if (menu) {
    tabs.open(menu);
  } else if (routeKey === "home") {
    tabs.open({
      id: "home",
      menuCode: "home",
      type: "module",
      parentId: null,
      title: "首页",
      routeKey: "home",
      iconKey: "home",
      order: 0,
      isClosable: false,
    });
  } else {
    tabs.openUnavailable(routeKey);
  }
}

function selectMenu(id: string) {
  const menu = navigation.findById(id);
  if (!menu?.routeKey) return;
  mobileMenuOpen.value = false;
  navigateTo(menu.routeKey);
}

function changeTab(name: string | number) {
  const tab = tabs.find(String(name));
  if (!tab) return;
  tabs.activate(tab.id);
  navigateTo(tab.routeKey);
}

function removeTab(name: string | number) {
  const next = tabs.close(String(name));
  navigateTo(next.routeKey, true);
}

async function retryMenus() {
  if (session.value) await loadPermissions(true);
  await navigation.load(true);
  syncRoute(String(route.params.routeKey ?? "home"));
}

function navigateTo(routeKey: string, replace = false) {
  const path = `/system/${encodeURIComponent(routeKey)}`;
  if (route.path === path) return;
  void (replace ? router.replace(path) : router.push(path));
}
</script>

<template>
  <div class="system-shell">
    <aside
      class="system-sidebar"
      :class="{ 'is-collapsed': collapsed, 'is-hidden': navigationMode === 'hidden' && !mobileMenuOpen, 'is-mobile-open': mobileMenuOpen }"
    >
      <div class="sidebar-brand">
        <span class="brand-icon"
          ><el-icon><Grid /></el-icon
        ></span>
        <div v-show="!collapsed">
          <strong>Devkit</strong><small>Workspace</small>
        </div>
      </div>

      <el-scrollbar class="sidebar-scroll">
        <el-menu
          :default-active="tabs.activeId"
          :default-openeds="openedDirectories"
          :collapse="collapsed"
          :collapse-transition="false"
          class="navigation-menu"
          @select="selectMenu"
        >
          <RecursiveMenuItem
            v-for="item in navigation.nodes"
            :key="item.id"
            :item="item"
          />
        </el-menu>
        <div v-if="navigation.loading" class="menu-state">
          <el-skeleton :rows="3" animated />
        </div>
        <div v-else-if="navigation.error" class="menu-error">
          <p>{{ navigation.error }}</p>
          <el-button text type="primary" :icon="Refresh" @click="retryMenus"
            >重新加载</el-button
          >
        </div>
      </el-scrollbar>

    </aside>

    <button
      v-if="mobileMenuOpen"
      class="sidebar-backdrop"
      aria-label="关闭导航"
      @click="mobileMenuOpen = false"
    />

    <button v-if="navigationMode === 'hidden'" type="button" class="sidebar-restore" aria-label="展开导航" title="展开导航" @click="setNavigationMode('expanded')">
      <el-icon><Expand /></el-icon>
    </button>
    <section class="system-main">
      <header class="system-header">
        <div class="header-left">
          <el-button
            class="mobile-menu-button"
            circle
            aria-label="展开导航"
            :icon="MenuIcon"
            @click="mobileMenuOpen = true"
          />
          <div>
            <strong>Devkit 工作台</strong><span>模块化业务开发框架</span>
          </div>
        </div>
        <div class="header-actions">
          <el-dropdown class="navigation-control" trigger="click" @command="setNavigationMode">
            <el-button :icon="Fold" aria-label="导航显示方式">导航显示</el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="expanded" :icon="Expand" :disabled="navigationMode === 'expanded'">展开导航</el-dropdown-item>
                <el-dropdown-item command="icons" :icon="Fold" :disabled="navigationMode === 'icons'">收起为图标</el-dropdown-item>
                <el-dropdown-item command="hidden" :icon="Hide" :disabled="navigationMode === 'hidden'">完全隐藏</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
          <span v-if="session" class="session-username" style="margin-right: 12px">{{
            session.user.userName
          }}</span
          ><el-button v-if="session" :icon="Refresh" :loading="refreshingPermissions" title="刷新权限" @click="refreshPermissionCache"
            ><span class="refresh-permission-label">刷新权限</span></el-button
          ><el-button v-if="session" @click="signOut">退出登录</el-button
          ><el-button v-else type="primary" @click="router.push('/login')"
            >登录</el-button
          ><el-button :icon="ArrowLeft" @click="router.push('/')"
            >返回欢迎页</el-button
          >
        </div>
      </header>

      <div v-if="session && permissionDenied" class="permission-warning">
        <span>服务端拒绝了该操作，当前权限可能已变化。请刷新权限或重新登录。</span>
        <el-button size="small" :loading="refreshingPermissions" @click="refreshPermissionCache">刷新权限</el-button>
        <el-button size="small" @click="signOut">重新登录</el-button>
      </div>

      <el-tabs
        :model-value="tabs.activeId"
        type="card"
        class="workspace-tabs"
        @tab-change="changeTab"
        @tab-remove="removeTab"
      >
        <el-tab-pane
          v-for="tab in tabs.tabs"
          :key="tab.id"
          :name="tab.id"
          :label="tab.title"
          :closable="tab.isClosable"
          lazy
        >
          <PageAccessView
            :route-key="tab.routeKey"
          />
        </el-tab-pane>
      </el-tabs>
    </section>
  </div>
</template>
