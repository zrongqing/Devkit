<script setup lang="ts">
import { computed } from 'vue'
import {
  DataAnalysis,
  FolderOpened,
  HomeFilled,
  InfoFilled,
  Monitor,
  Setting,
} from '@element-plus/icons-vue'

const props = defineProps<{
  iconKey: string | null
  directory?: boolean
}>()

const icon = computed(() => {
  const icons = {
    home: HomeFilled,
    system: DataAnalysis,
    monitor: Monitor,
    pulse: DataAnalysis,
    settings: Setting,
    info: InfoFilled,
  }
  return icons[props.iconKey as keyof typeof icons] ?? (props.directory ? FolderOpened : DataAnalysis)
})
</script>

<template>
  <el-icon><component :is="icon" /></el-icon>
</template>
