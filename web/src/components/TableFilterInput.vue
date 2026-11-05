<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { Filter } from '@element-plus/icons-vue'
import type { ColumnFilterConfig, FilterDraft, FilterOption } from './tablePresentation'

const props = defineProps<{ label: string; modelValue: FilterDraft; config?: ColumnFilterConfig }>()
const emit = defineEmits<{ 'update:modelValue': [value: FilterDraft]; options: [options: FilterOption[]]; submit: [] }>()
const candidates = ref<FilterOption[]>([]), loading = ref(false), error = ref(''), keyword = ref('')
const options = computed(() => [...new Map([...(props.config?.options ?? []), ...candidates.value].map(o => [o.value, o])).values()])
const range = computed(() => typeof props.modelValue === 'object' && !Array.isArray(props.modelValue) ? props.modelValue : { min: '', max: '' })
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
const rangeError = computed(() => {
  if (props.config?.type !== 'numberRange' && props.config?.type !== 'dateRange') return ''
  const { min, max } = range.value
  const number = (v: string) => props.config?.type === 'dateRange' ? Date.parse(v) : Number(v)
  if ((min && !Number.isFinite(number(min))) || (max && !Number.isFinite(number(max)))) return '请输入有效范围'
  return min && max && number(min) > number(max) ? '起始值不能大于结束值' : ''
})
function submit() { if (!rangeError.value) emit('submit') }
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
          <el-date-picker :model-value="range.min" type="datetime" value-format="YYYY-MM-DDTHH:mm:ssZ" :teleported="false" placeholder="起始时间" :aria-label="`${label}起始时间`" @update:model-value="changeRange('min', $event)" />
          <el-date-picker :model-value="range.max" type="datetime" value-format="YYYY-MM-DDTHH:mm:ssZ" :teleported="false" placeholder="结束时间" :aria-label="`${label}结束时间`" @update:model-value="changeRange('max', $event)" />
        </template>
        <template v-else>
          <el-input :model-value="range.min" type="number" clearable placeholder="最小值" :aria-label="`${label}最小值`" @update:model-value="changeRange('min', $event)" @keyup.enter="submit" />
          <el-input :model-value="range.max" type="number" clearable placeholder="最大值" :aria-label="`${label}最大值`" @update:model-value="changeRange('max', $event)" @keyup.enter="submit" />
        </template>
      </div>
      <el-input v-else :model-value="typeof modelValue === 'string' ? modelValue : ''" clearable :placeholder="`筛选${label}`" :aria-label="`筛选${label}的内容`"
        @update:model-value="emit('update:modelValue', $event)" @keyup.enter="submit" />
      <el-button :icon="Filter" :disabled="!!rangeError" aria-label="提交全部列筛选" title="提交全部列筛选" @click="submit" />
    </div>
    <span v-if="rangeError" class="table-filter-input__error" role="alert">{{ rangeError }}</span>
    <div v-if="error" class="table-filter-input__error" role="alert">{{ error }} <el-button link type="primary" @click="load(keyword)">重试</el-button></div>
  </div>
</template>

<style scoped>
.table-filter-input__controls { display: flex; align-items: center; gap: 8px; }
.table-filter-input__controls > :first-child { flex: 1; min-width: 0; }
.table-filter-input__range { display: flex; flex-direction: column; gap: 8px; }
.table-filter-input__range :deep(.el-date-editor) { width: 100%; }
.table-filter-input__error { display: block; margin-top: 8px; color: var(--el-color-danger); font-size: 12px; }
</style>
