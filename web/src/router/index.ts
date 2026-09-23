import { createRouter, createWebHashHistory } from "vue-router";
import WelcomeView from "../views/WelcomeView.vue";
import SystemLayout from "../layouts/SystemLayout.vue";
import LoginView from "../views/LoginView.vue";
import { session, loadPermissions, hasPermission } from "../api/session";

export const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: "/login", name: "login", component: LoginView },
    {
      path: "/",
      name: "welcome",
      component: WelcomeView,
    },
    {
      path: "/system",
      redirect: "/system/home",
    },
    {
      path: "/system/:routeKey",
      name: "system",
      component: SystemLayout,
      props: true,
    },
    {
      path: "/:pathMatch(.*)*",
      redirect: "/",
    },
  ],
});

export function routePermission(key: string): string | undefined {
  if (key.startsWith("study-")) return "exam-study.access";
  return (
    {
      "system-files": "system.files.manage",
      "system-storage": "system.storage.manage",
      "system-identity": "system.identity.manage",
    } as Record<string, string>
  )[key];
}
router.beforeEach(async (to) => {
  const permission = routePermission(String(to.params.routeKey ?? ""));
  if (!permission) return true;
  if (!session.value)
    return { path: "/login", query: { redirect: to.fullPath } };
  try {
    await loadPermissions();
    return hasPermission(permission) ? true : "/system/home";
  } catch {
    return { path: "/login", query: { redirect: to.fullPath } };
  }
});
window.addEventListener("devkit-session-expired", () => {
  if (routePermission(String(router.currentRoute.value.params.routeKey ?? "")))
    void router.replace("/login");
});
