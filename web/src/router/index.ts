import { createRouter, createWebHashHistory } from "vue-router";
import WelcomeView from "../views/WelcomeView.vue";
import SystemLayout from "../layouts/SystemLayout.vue";
import LoginView from "../views/LoginView.vue";
import { getPageDefinition } from "./pageRegistry";
import { useNavigationStore } from "../stores/navigation";
import { session, loadPermissions } from "../api/session";

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
  return getPageDefinition(key)?.requiredPermissions[0];
}
router.beforeEach(async (to) => {
  if (to.name !== "system") return true;
  const key = String(to.params.routeKey ?? "home");
  if (!getPageDefinition(key)) return "/system/home";
  const navigation = useNavigationStore();
  if (session.value) {
    try { await loadPermissions(); }
    catch { return { path: "/login", query: { redirect: to.fullPath } }; }
  }
  await navigation.load(true);
  if (key === "home" || navigation.findByRouteKey(key)) return true;
  return session.value ? "/system/home" : { path: "/login", query: { redirect: to.fullPath } };
});
window.addEventListener("devkit-session-expired", () => {
  if (router.currentRoute.value.name === "system") void router.replace("/login");
});
