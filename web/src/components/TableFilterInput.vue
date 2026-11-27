<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { CircleClose, Filter } from '@element-plus/icons-vue'
import { displayTimeZone, systemSettingsReady } from '../stores/systemSettings'
import { localTimeInput, localTimeToUtc } from '../utils/zonedTime'
import { rangeFilterError } from './tableFilters'
import type { ColumnFilterConfig, FilterDraft, FilterOption, RangeFilterDraft } from './tablePresentation'

const props = defineProps<{ label: string; modelValue: FilterDraft; config?: ColumnFilterConfig }>()
const emit = defineEmits<{ 'update:modelValue': [value: FilterDraft]; options: [options: FilterOption[]]; submit: [] }>()
const candidates = ref<FilterOption[]>([]), loading = ref(false), error = ref(''), keyword = ref('')
const options = computed(() => [...new Map([...(props.config?.options ?? []), ...candidates.value].map(o => [o.value, o])).values()])
const range = computed<RangeFilterDraft>(() => typeof props.modelValue === 'object' && !Array.isArray(props.modelValue) ? props.modelValue : { min: '', max: '' })
const bounds = [
  { key: 'min', invalidKey: 'invalidMin', label: '起始时间' },
  { key: 'max', invalidKey: 'invalidMax', label: '结束时间' },
] as const
let controller: AbortController | undefined, timer: ReturnType<typeof setTimeout> | undefined, sequence = 0
async function load(search: string) {
  if (!props.config?.loadOptions) return
  controller?.abort(); controller = new AbortController()
  const current = ++sequence
  loading.value = true; error.value = ''
  try {
    const result = await props.config.loadOptions(search, controller.signal)
    if (current !== sequence) return
    // Retain labels for selected candidates when the next remote search replaces the list.
    const selected = Array.isArray(props.modelValue) ? props.modelValue : []
    candidates.value = [...options.value.filter(o => selected.includes(o.value)), ...result]
    emit('options', options.value)
  } catch (e) {
    if (current === sequence && !controller.signal.aborted) error.value = e instanceof Error ? e.message : String(e)
  } finally { if (current === sequence) loading.value = false }
}
function search(value: string) {
  keyword.value = value
  clearTimeout(timer); controller?.abort(); sequence++
  timer = setTimeout(() => void load(value), 250)
}
watch(() => props.config?.loadOptions, () => {
  clearTimeout(timer); controller?.abort(); sequence++; loading.value = false; error.value = ''
  candidates.value = []; void load('')
}, { immediate: true })
onBeforeUnmount(() => { sequence++; clearTimeout(timer); controller?.abort() })
function changeRange(key: 'min' | 'max', value: string | null) { emit('update:modelValue', { ...range.value, [key]: value ?? '' }) }
function dateInputValue(bound: typeof bounds[number]): string {
  const invalid = range.value[bound.invalidKey]
  const utc = range.value[bound.key]
  return invalid ? invalid.value : utc ? localTimeInput(utc, displayTimeZone.value) : ''
}
function changeDate(bound: typeof bounds[number], value: string, badInput = false) {
  const next = { ...range.value, [bound.key]: '' }
  delete next[bound.invalidKey]
  if (badInput) next[bound.invalidKey] = { value, message: '请输入完整的日期和时间。' }
  else if (value) {
    try { next[bound.key] = localTimeToUtc(value, displayTimeZone.value) }
    catch { next[bound.invalidKey] = { value, message: '该时间在当前时区不存在、重复或超出范围，请选择其他时间。' } }
  }
  emit('update:modelValue', next)
}
function readDate(bound: typeof bounds[number], event: Event) {
  const input = event.target as HTMLInputElement
  changeDate(bound, input.value, input.validity.badInput)
}
const rangeError = computed(() => {
  if (props.config?.type !== 'numberRange' && props.config?.type !== 'dateRange') return ''
  return rangeFilterError(range.value, props.config.type)
})
const cannotSubmit = computed(() => !!rangeError.value || (props.config?.type === 'dateRange' && !systemSettingsReady.value))
function submit() { if (!cannotSubmit.value) emit('submit') }
</script>

<template>
  <div class="table-filter-input">
    <div class="table-filter-input__controls">
      <el-select v-if="config?.type === 'select'" :model-value="Array.isArray(modelValue) ? modelValue : []"
        multiple filterable clearable collapse-tags collapse-tags-tooltip :max-collapse-tags="2" :multiple-limit="100" :teleported="false"
        :remote="!!config.loadOptions" :remote-method="search" :loading="loading" :placeholder="`搜索并选择${label}`" :aria-label="`筛选${label}`"
        @update:model-value="emit('update:modelValue', $event)">
        <el-option v-for="option in options" :key="option.value" :value="option.value" :label="option.label" />
      </el-select>
      <div v-else-if="config?.type === 'numberRange' || config?.type === 'dateRange'" class="table-filter-input__range">
        <template v-if="config.type === 'dateRange'">
          <small>{{ systemSettingsReady ? `时区 ${displayTimeZone}` : '正在读取显示时区…' }}</small>
          <div v-for="bound in bounds" :key="bound.key" class="table-filter-input__date">
            <label>
              <span>{{ bound.label }}</span>
              <input type="datetime-local" step="1" min="0001-01-01T00:00:00" max="9999-12-31T23:59:59"
                :aria-label="`${label}${bound.label}`" :aria-invalid="!!range[bound.invalidKey]"
                :value="dateInputValue(bound)" :disabled="!systemSettingsReady"
                @input="readDate(bound, $event)" @keydown.enter.prevent="submit" />
            </label>
            <el-button v-if="range[bound.key] || range[bound.invalidKey]" :icon="CircleClose" text
              :aria-label="`清除${bound.label}`" @click="changeDate(bound, '')" />
          </div>
        </template>
        <template v-else>
          <el-input :model-value="range.min" type="number" clearable placeholder="最小值" :aria-label="`${label}最小值`" @update:model-value="changeRange('min', $event)" @keyup.enter="submit" />
          <el-input :model-value="range.max" type="number" clearable placeholder="最大值" :aria-label="`${label}最大值`" @update:model-value="changeRange('max', $event)" @keyup.enter="submit" />
        </template>
      </div>
      <el-input v-else :model-value="typeof modelValue === 'string' ? modelValue : ''" clearable :placeholder="`筛选${label}`" :aria-label="`筛选${label}的内容`"
        @update:model-value="emit('update:modelValue', $event)" @keyup.enter="submit" />
      <el-button :icon="Filter" :disabled="cannotSubmit" aria-label="提交全部列筛选" title="提交全部列筛选" @click="submit" />
    </div>
    <span v-if="rangeError" class="table-filter-input__error" role="alert">{{ rangeError }}</span>
    <div v-if="error" class="table-filter-input__error" role="alert">{{ error }} <el-button link type="primary" @click="load(keyword)">重试</el-button></div>
  </div>
</template>

<style scoped>
.table-filter-input__controls { display: flex; align-items: center; gap: 8px; }
.table-filter-input__controls > :first-child { flex: 1; min-width: 0; }
.table-filter-input__range { display: flex; flex-direction: column; gap: 8px; }
.table-filter-input__date { display: flex; align-items: flex-end; }
.table-filter-input__date label { display: grid; gap: 4px; flex: 1; min-width: 0; font-size: 12px; }
.table-filter-input__date input { width: 100%; box-sizing: border-box; height: 32px; padding: 0 8px; font: inherit; color: var(--el-text-color-regular); background: var(--el-fill-color-blank); border: 1px solid var(--el-border-color); border-radius: var(--el-border-radius-base); }
.table-filter-input__error { display: block; margin-top: 8px; color: var(--el-color-danger); font-size: 12px; }
</style>
