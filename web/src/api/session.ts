import { ref } from "vue";
import { canonicalMenuCode } from "./menuCodeCompatibility";
import type { TokenPair } from "./types";

const key = "devkit.session";
const permissionKey = "devkit.permissions";
type PermissionSnapshot = {
  id: string;
  administrator: boolean;
  permissions: string[];
  menuCodes: string[];
};
function restore(): TokenPair | null {
  try {
    return JSON.parse(
      sessionStorage.getItem(key) ?? "null",
    ) as TokenPair | null;
  } catch {
    return null;
  }
}
function restorePermissions(currentSession: TokenPair | null): PermissionSnapshot | null {
  if (!currentSession) return null;
  try {
    const value = JSON.parse(sessionStorage.getItem(permissionKey) ?? "null");
    return value?.id === currentSession.user.id &&
      typeof value.administrator === "boolean" &&
      Array.isArray(value.menuCodes) &&
      value.menuCodes.every((code: unknown) => typeof code === "string") &&
      Array.isArray(value.permissions) &&
      value.permissions.every((permission: unknown) => typeof permission === "string")
      ? value as PermissionSnapshot
      : null;
  } catch {
    return null;
  }
}
const restoredSession = restore();
export const session = ref<TokenPair | null>(restoredSession);
export const permissions = ref<PermissionSnapshot | null>(restorePermissions(restoredSession));
export const permissionDenied = ref(false);
let sessionGeneration = 0;
export function saveSession(value: TokenPair | null) {
  sessionGeneration++;
  loadingPermissions = undefined;
  session.value = value;
  permissions.value = null;
  permissionDenied.value = false;
  sessionStorage.removeItem(permissionKey);
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
      session.value = body.data as TokenPair;
      sessionStorage.setItem(key, JSON.stringify(session.value));
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
  if (response.status === 403 && session.value) permissionDenied.value = true;
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
let loadingPermissions: Promise<PermissionSnapshot> | undefined;
export function loadPermissions(force = false): Promise<PermissionSnapshot> {
  if (!session.value) return Promise.reject(new Error("请先登录。"));
  if (loadingPermissions) return loadingPermissions;
  if (permissions.value && !force) return Promise.resolve(permissions.value);
  const generation = sessionGeneration;
  const userId = session.value.user.id;
  const pending = request<PermissionSnapshot>("/api/v1/auth/permissions")
    .then((value) => {
      if (sessionGeneration !== generation || session.value?.user.id !== userId)
        throw new Error("登录状态已变更，请重新加载权限。");
      if (value.id !== userId) throw new Error("权限数据与当前用户不匹配。");
      permissions.value = value;
      sessionStorage.setItem(permissionKey, JSON.stringify(value));
      permissionDenied.value = false;
      return value;
    })
    .finally(() => {
      if (loadingPermissions === pending) loadingPermissions = undefined;
    });
  loadingPermissions = pending;
  return pending;
}
export const hasPermission = (value: string) =>
  permissions.value?.administrator ||
  permissions.value?.permissions.includes(value) ||
  false;

export const hasMenuGrant = (code: string) => permissions.value?.administrator ||
  permissions.value?.menuCodes.some(grant => canonicalMenuCode(grant) === canonicalMenuCode(code)) || false;
