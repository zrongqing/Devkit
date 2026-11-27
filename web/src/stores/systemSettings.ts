import { computed, readonly, ref } from 'vue'
import { getSystemSettings, updateSystemSettings } from '../api/systemSettings'
import { formatTime, supportsTimeZone } from '../utils/time'

const savedTimeZone = ref<string | null>(null)
const deviceTimeZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'UTC'
const localZone = ref(deviceTimeZone())
const loading = ref(false)
const loaded = ref(false)
const ready = ref(false)
const error = ref('')
const saving = ref(false)
let activeLoad: AbortController | undefined

export const systemTimeZone = readonly(savedTimeZone)
export const browserTimeZone = readonly(localZone)
export const systemSettingsLoading = readonly(loading)
export const systemSettingsLoaded = readonly(loaded)
export const systemSettingsReady = readonly(ready)
export const systemSettingsError = readonly(error)
export const systemSettingsSaving = readonly(saving)

export const unsupportedTimeZone = computed(() => savedTimeZone.value !== null && !supportsTimeZone(savedTimeZone.value))
export const displayTimeZone = computed(() => unsupportedTimeZone.value ? localZone.value : savedTimeZone.value ?? localZone.value)

export function formatDisplayTime(value?: string | number | null): string {
  return formatTime(value, displayTimeZone.value)
}

export async function loadSystemSettings() {
  localZone.value = deviceTimeZone()
  if (activeLoad || saving.value) return
  const controller = new AbortController()
  activeLoad = controller
  loading.value = true
  const timeout = window.setTimeout(() => controller.abort(), 8000)
  try {
    const settings = await getSystemSettings(controller.signal)
    if (activeLoad !== controller) return
    savedTimeZone.value = settings.timeZone
    loaded.value = true
    error.value = ''
  } catch {
    if (activeLoad === controller) error.value = '无法读取系统显示时区。'
  } finally {
    window.clearTimeout(timeout)
    if (activeLoad === controller) {
      activeLoad = undefined
      loading.value = false
      ready.value = true
    }
  }
}

export async function saveSystemTimeZone(timeZone: string | null) {
  saving.value = true
  activeLoad?.abort()
  activeLoad = undefined
  loading.value = false
  try {
    savedTimeZone.value = (await updateSystemSettings({ timeZone })).timeZone
    error.value = ''
  } finally {
    saving.value = false
  }
}
