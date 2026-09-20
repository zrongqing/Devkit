<script setup lang="ts">
import { WarningFilled } from '@element-plus/icons-vue'

defineProps<{
  routeKey?: string
}>()
</script>

<template>
  <section class="page-shell unavailable-view">
    <el-icon><WarningFilled /></el-icon>
    <h2>页面尚未部署</h2>
    <p>当前客户端尚未注册页面键“{{ routeKey || 'unknown' }}”。</p>
  </section>
</template>
