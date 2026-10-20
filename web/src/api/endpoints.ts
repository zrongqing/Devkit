// A single gateway remains the default. Direct deployments can override the identity origin.
export const apiBase = (import.meta.env.VITE_API_BASE_URL ?? '/').replace(/\/$/, '')
export const authBase = (import.meta.env.VITE_AUTH_BASE_URL ?? apiBase).replace(/\/$/, '')

export function endpoint(path: string): string {
  const identity = path === '/api/v1/auth' || path.startsWith('/api/v1/auth/') ||
    path === '/api/v1/identity' || path.startsWith('/api/v1/identity/') ||
    path === '/api/v1/web' || path.startsWith('/api/v1/web/') ||
    path === '/api/v1/client/navigation' || path.startsWith('/api/v1/client/navigation/')
  return `${identity ? authBase : apiBase}${path}`
}
