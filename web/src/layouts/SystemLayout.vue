<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import {
  ArrowLeft,
  Fold,
  Grid,
  Menu as MenuIcon,
  Refresh,
} from "@element-plus/icons-vue";
import { useRoute, useRouter } from "vue-router";
import RecursiveMenuItem from "../components/navigation/RecursiveMenuItem.vue";
import { resolvePageComponent } from "../router/pageRegistry";
import { useNavigationStore } from "../stores/navigation";
import { useTabsStore } from "../stores/tabs";
import { session, saveSession, loadPermissions } from "../api/session";
import { logout } from "../api/auth";

const route = useRoute();
const router = useRouter();
const navigation = useNavigationStore();
const tabs = useTabsStore();
const collapsed = ref(false);
const mobileMenuOpen = ref(false);
const initialized = ref(false);

const openedDirectories = computed(() =>
  navigation.items.filter((item) => !item.routeKey).map((item) => item.id),
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
    for (const tab of [...tabs.tabs])
      if (
        tab.routeKey &&
        tab.routeKey !== "home" &&
        !navigation.findByRouteKey(tab.routeKey)
      )
        tabs.close(tab.id);
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
  if (session.value) await loadPermissions();
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
      :class="{ 'is-collapsed': collapsed, 'is-mobile-open': mobileMenuOpen }"
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

      <button
        class="sidebar-collapse"
        type="button"
        @click="collapsed = !collapsed"
      >
        <el-icon><Fold /></el-icon><span v-show="!collapsed">收起导航</span>
      </button>
    </aside>

    <button
      v-if="mobileMenuOpen"
      class="sidebar-backdrop"
      aria-label="关闭导航"
      @click="mobileMenuOpen = false"
    />

    <section class="system-main">
      <header class="system-header">
        <div class="header-left">
          <el-button
            class="mobile-menu-button"
            circle
            :icon="MenuIcon"
            @click="mobileMenuOpen = true"
          />
          <div>
            <strong>Devkit 工作台</strong><span>模块化业务开发框架</span>
          </div>
        </div>
        <div>
          <span v-if="session" style="margin-right: 12px">{{
            session.user.userName
          }}</span
          ><el-button v-if="session" @click="signOut">退出登录</el-button
          ><el-button v-else type="primary" @click="router.push('/login')"
            >登录</el-button
          ><el-button :icon="ArrowLeft" @click="router.push('/')"
            >返回欢迎页</el-button
          >
        </div>
      </header>

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
          <component
            :is="resolvePageComponent(tab.routeKey)"
            :route-key="tab.routeKey"
          />
        </el-tab-pane>
      </el-tabs>
    </section>
  </div>
</template>
