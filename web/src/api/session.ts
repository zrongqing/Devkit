import { ref } from "vue";
import type { TokenPair } from "./types";

const key = "devkit.session";
function restore(): TokenPair | null {
  try {
    return JSON.parse(
      sessionStorage.getItem(key) ?? "null",
    ) as TokenPair | null;
  } catch {
    return null;
  }
}
export const session = ref<TokenPair | null>(restore());
export const permissions = ref<{
  id: string;
  administrator: boolean;
  permissions: string[];
} | null>(null);
export function saveSession(value: TokenPair | null) {
  session.value = value;
  permissions.value = null;
  if (value) sessionStorage.setItem(key, JSON.stringify(value));
  else sessionStorage.removeItem(key);
}
export const apiBase = (
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:12511"
).replace(/\/$/, "");
let refreshing: Promise<boolean> | undefined;
async function renew() {
  if (!refreshing)
    refreshing = (async () => {
      if (!session.value) return false;
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: session.value.refreshToken }),
      });
      if (!response.ok) {
        saveSession(null);
        return false;
      }
      const body = await response.json();
      saveSession(body.data as TokenPair);
      return true;
    })().finally(() => {
      refreshing = undefined;
    });
  return refreshing;
}
export async function authorizedFetch(
  path: string,
  init: RequestInit = {},
  retry = true,
): Promise<Response> {
  const headers = new Headers(init.headers);
  if (session.value)
    headers.set("Authorization", `Bearer ${session.value.accessToken}`);
  if (init.body && !(init.body instanceof FormData))
    headers.set("Content-Type", "application/json");
  const response = await fetch(`${apiBase}${path}`, { ...init, headers });
  if (response.status === 401 && retry && session.value && (await renew()))
    return authorizedFetch(path, init, false);
  if (response.status === 401) {
    saveSession(null);
    window.dispatchEvent(new Event("devkit-session-expired"));
  }
  return response;
}
export async function request<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await authorizedFetch(path, init);
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.title ?? `请求失败（${response.status}）`);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()).data as T;
}
export async function loadPermissions() {
  permissions.value = await request<NonNullable<typeof permissions.value>>(
    "/api/v1/auth/permissions",
  );
  return permissions.value;
}
export const hasPermission = (value: string) =>
  permissions.value?.administrator ||
  permissions.value?.permissions.includes(value) ||
  false;
