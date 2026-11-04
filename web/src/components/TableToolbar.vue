<script setup lang="ts">
import { ref } from 'vue'
import { ArrowDown, Refresh, Setting } from '@element-plus/icons-vue'
import type { ColumnOption, ColumnPin, TableFilterTag } from './tablePresentation'
defineProps<{
  columns: ColumnOption[]; tags: TableFilterTag[]
  loading: boolean; querying: boolean
}>()
const emit = defineEmits<{
  refresh: []; resetFilters: []; resetTable: []; autoFit: []
  removeFilter: [key: string, external: boolean]; visibility: [key: string, visible: boolean]; pin: [key: string, pin: ColumnPin]
}>()
const columnsOpen = ref(false)
function command(value: string) {
  if (value === 'columns') columnsOpen.value = true
  else if (value === 'fit') emit('autoFit')
  else emit('resetTable')
}
</script>

<template>
  <div class="data-table-tools">
    <div class="data-table-toolbar">
      <div v-if="$slots.filters || $slots.query" class="data-table-toolbar__query">
        <slot name="filters" />
        <slot name="query" />
      </div>
      <div class="data-table-toolbar__actions">
        <span class="data-table-query-status" role="status" aria-live="polite">{{ loading ? '正在查询…' : '' }}</span>
        <el-button :icon="Refresh" aria-label="刷新表格" @click="emit('refresh')">刷新</el-button>
        <el-dropdown trigger="click" @command="command">
          <el-button :icon="Setting">表格设置 <el-icon class="data-table-dropdown-arrow"><ArrowDown /></el-icon></el-button>
          <template #dropdown><el-dropdown-menu>
            <el-dropdown-item command="columns">列设置</el-dropdown-item>
            <el-dropdown-item command="fit" :disabled="querying">自动适应列宽</el-dropdown-item>
            <el-dropdown-item command="reset" divided>重置表格</el-dropdown-item>
          </el-dropdown-menu></template>
        </el-dropdown>
        <slot name="actions" />
      </div>
    </div>
    <div v-if="tags.length" class="data-table-filter-tags">
      <el-tag v-for="tag in tags" :key="`${tag.external ? 'page' : 'column'}:${tag.key}`" closable effect="light"
        @close="emit('removeFilter', tag.key, !!tag.external)">{{ tag.label }}：{{ tag.value }}</el-tag>
      <el-button link type="primary" @click="emit('resetFilters')">清空筛选</el-button>
    </div>
  </div>
  <el-drawer v-model="columnsOpen" title="列设置" size="min(440px, 95vw)" append-to-body>
    <p class="data-table-settings-hint">选择显示的列与固定位置。拖动列间边界调整宽度，设置会自动保存。</p>
    <div class="data-table-column-options">
      <div v-for="column in columns" :key="column.key" class="data-table-column-option">
        <el-checkbox :model-value="column.visible" :disabled="!column.field || column.visible && columns.filter(c => c.field && c.visible).length === 1"
          @update:model-value="emit('visibility', column.key, Boolean($event))">{{ column.label }}</el-checkbox>
        <el-select :model-value="column.pin || 'none'" size="small" :aria-label="`${column.label}固定位置`" :disabled="!column.visible"
          @update:model-value="emit('pin', column.key, $event === 'none' ? false : $event)">
          <el-option value="none" label="不固定" /><el-option value="left" label="固定在左侧" /><el-option value="right" label="固定在右侧" />
        </el-select>
      </div>
    </div>
    <template #footer><el-button @click="emit('autoFit')" :disabled="querying">自动适应列宽</el-button><el-button type="primary" @click="columnsOpen = false">完成</el-button></template>
  </el-drawer>
</template>
