<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Filter } from '@element-plus/icons-vue'
import TableFilterInput from './TableFilterInput.vue'
import type { ColumnFilterConfig, FilterDraft, FilterOption } from './tablePresentation'
defineProps<{ label: string; filter: FilterDraft; config?: ColumnFilterConfig; active: boolean; order: number; priority: number; searchable: boolean }>()
const emit = defineEmits<{ filter: [value: FilterDraft]; options: [options: FilterOption[]]; submit: []; sort: [direction: number]; minimumWidth: [width: number] }>()
const heading = ref<HTMLElement>()
const open = ref(false)
let observer: ResizeObserver | undefined
function measureMinimum() {
  const element = heading.value
  const cell = element?.closest<HTMLElement>('.cell')
  if (!element || !cell || !element.getClientRects().length) return
  const gap = parseFloat(getComputedStyle(element).columnGap) || 0
  const children = Array.from(element.children)
  const content = children.reduce((sum, child) => sum + child.getBoundingClientRect().width, 0) + gap * Math.max(0, children.length - 1)
  const style = getComputedStyle(cell)
  const border = cell.closest('th')
  const borderWidth = border ? parseFloat(getComputedStyle(border).borderRightWidth) || 0 : 0
  // Measure intrinsic label/control widths; unused space and auto margins do not
  // become part of the minimum when the user widens a column.
  emit('minimumWidth', Math.ceil(content + (parseFloat(style.paddingLeft) || 0) + (parseFloat(style.paddingRight) || 0) + borderWidth + 2))
}
onMounted(() => {
  measureMinimum()
  observer = new ResizeObserver(measureMinimum)
  if (heading.value) {
    observer.observe(heading.value)
    if (heading.value.firstElementChild) observer.observe(heading.value.firstElementChild)
  }
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <div ref="heading" class="data-table-heading">
    <span class="data-table-heading__label" :title="label">{{ label }}</span>
    <el-popover v-if="searchable" v-model:visible="open" trigger="click" placement="bottom" :width="320">
      <template #reference>
        <el-button class="data-table-heading__button" :class="{ 'is-active': active || open }" text :type="active ? 'primary' : undefined" :icon="Filter"
          :aria-label="`筛选${label}`" :title="`筛选${label}`" :aria-pressed="active" />
      </template>
      <TableFilterInput v-if="open" :label="label" :model-value="filter" :config="config"
        @update:model-value="$emit('filter', $event)" @options="$emit('options', $event)" @submit="$emit('submit')" />
    </el-popover>
    <span v-if="searchable" class="data-table-sort" :class="{ 'is-active': order !== 0 }">
      <span class="caret-wrapper">
        <button class="sort-caret ascending" :class="{ 'is-active': order === 1 }" :aria-label="`${label}升序`" :aria-pressed="order === 1" title="升序，再次点击取消" @click.stop="$emit('sort', 1)" />
        <button class="sort-caret descending" :class="{ 'is-active': order === -1 }" :aria-label="`${label}降序`" :aria-pressed="order === -1" title="降序，再次点击取消" @click.stop="$emit('sort', -1)" />
      </span><small v-if="priority" class="data-table-sort__priority" :title="`排序优先级 ${priority}`" :aria-label="`${label}排序优先级 ${priority}`">{{ priority }}</small>
    </span>
  </div>
</template>

<style>
.data-table-heading { display: flex; align-items: center; flex-wrap: nowrap; gap: 2px; min-width: 0; }
.data-table-heading__label { flex: 0 0 auto; margin-right: auto; white-space: nowrap; }
.data-table-heading .data-table-heading__button { flex: none; width: 24px; height: 28px; padding: 3px; margin: 0; }
.data-table-sort { position: relative; display: inline-flex; flex: 0 0 18px; align-items: center; width: 18px; }
.data-table-heading__button:focus-visible { outline: 2px solid var(--el-color-primary); outline-offset: -2px; }
.data-table-sort .caret-wrapper { width: 18px; height: 28px; margin: 0; }
.data-table-sort .sort-caret { left: 3px; padding: 0; background: transparent; cursor: pointer; }
.data-table-sort .sort-caret.ascending { top: 2px; }
.data-table-sort .sort-caret.descending { bottom: 2px; }
.el-table .data-table-sort .sort-caret.ascending.is-active { border-bottom-color: var(--el-color-primary); }
.el-table .data-table-sort .sort-caret.descending.is-active { border-top-color: var(--el-color-primary); }
.data-table-sort button:focus-visible { outline: 2px solid var(--el-color-primary); }
.data-table-sort__priority { position: absolute; top: -1px; right: -5px; min-width: 10px; height: 12px; line-height: 12px; text-align: center; color: var(--el-color-primary); background: var(--el-color-primary-light-9); border-radius: 3px; font-size: 10px; font-weight: 600; font-variant-numeric: tabular-nums; }
.data-table { min-width: 0; width: 100%; max-width: 100%; }
.data-table > .el-table { min-width: 0; width: 100%; max-width: 100%; --el-table-border-color: #edf0f5; --el-table-header-bg-color: #f7f9fc; }
.data-table .el-table th.el-table__cell { color: #475569; font-size: 13px; height: 46px; background: #f7f9fc; }
.data-table .el-table th.el-table__cell > .cell { padding-right: 8px; }
.data-table .el-table td.el-table__cell { padding: 12px 0; border-right-color: transparent; vertical-align: middle; }
.data-table .el-table__body td .cell { line-height: 24px; }
.data-table .el-table.el-table--border .el-table__header-wrapper th.el-table__cell { border-right: 1px solid var(--el-border-color); }
.data-table .el-table.is-scrolling-none td.el-table-fixed-column--left.is-last-column.el-table__cell { border-right-color: transparent; }
.data-table .el-table .is-right .data-table-heading { justify-content: flex-end; }
.data-table .el-table .is-right .data-table-heading__label { margin-right: 0; }
.data-table--pending .el-table__body-wrapper { pointer-events: none; }
.data-table-tools { padding-bottom: 16px; }
.data-table-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; }
.data-table-toolbar__query, .data-table-toolbar__actions { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; min-width: 0; }
.data-table-toolbar__actions { margin-left: auto; }
.data-table-toolbar .el-button + .el-button { margin-left: 0; }
.data-table-toolbar__query .el-select { width: 200px; max-width: 100%; }
.data-table-dropdown-arrow { margin-left: 6px; }
.data-table-filter-tags { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; padding-top: 12px; }
.data-table-filter-tags .el-tag { max-width: 100%; }
.data-table-filter-tags .el-tag__content { overflow: hidden; text-overflow: ellipsis; }
.data-table-query-status { color: var(--el-text-color-secondary); font-size: 12px; }
.data-table-settings-hint { margin: 0 0 20px; color: var(--el-text-color-secondary); font-size: 13px; line-height: 1.6; }
.data-table-column-option { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 0; border-bottom: 1px solid var(--el-border-color-lighter); }
.data-table-column-option .el-checkbox { min-width: 0; height: auto; }
.data-table-column-option .el-checkbox__label { white-space: normal; line-height: 20px; }
.data-table-column-option .el-select { flex: 0 0 122px; width: 122px; }
.data-table-footer { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; padding-top: 18px; }
.data-table-footer .el-pagination { max-width: 100%; overflow-x: auto; }
.data-table-total { color: var(--el-text-color-secondary); font-size: 13px; }
.data-table-measure { position: absolute; top: 0; left: 0; width: max-content; visibility: hidden; pointer-events: none; }
.data-table-measure * { white-space: nowrap !important; }
@media (max-width: 620px) {
  .data-table-toolbar__query { width: 100%; }
  .data-table-toolbar__actions { width: 100%; justify-content: flex-end; }
  .data-table-footer { align-items: flex-start; }
}
</style>
