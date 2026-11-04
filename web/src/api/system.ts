import { request } from "./session";
import type { ApiResponse, SystemInfo } from './types'
import { apiBase } from './endpoints'

export async function getSystemInfo(signal?: AbortSignal): Promise<SystemInfo> {
  const response = await fetch(`${apiBase}/api/v1/system/info`, { signal })
  if (!response.ok) {
    throw new Error(`System info request failed (${response.status})`)
  }

  const body = (await response.json()) as ApiResponse<SystemInfo>
  return body.data
}

export interface SystemRuntime {
  info: SystemInfo;
  startedAtUtc: string;
  uptimeSeconds: number;
  workingSetBytes: number;
  managedMemoryBytes: number;
  threadCount: number;
  processorCount: number;
  totalProcessorSeconds: number;
}
export async function getSystemRuntime(signal?: AbortSignal, service: 'application' | 'business' = 'business'): Promise<SystemRuntime> {
  return request<SystemRuntime>(service === "application" ? "/api/v1/identity/runtime" : "/api/v1/system/runtime", { signal });
}
