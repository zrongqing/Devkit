<script setup lang="ts">
import { ref } from 'vue'

const compactMode = ref(false)
const notifications = ref(true)
</script>

<template>
  <section class="page-shell">
    <div class="page-heading">
      <div><p class="page-kicker">系统</p><h2>设置</h2></div>
      <p>用于展示后续模块可以采用的表单布局。</p>
    </div>
    <el-card shadow="never" class="settings-card">
      <el-form label-position="top">
        <el-form-item label="紧凑模式">
          <el-switch v-model="compactMode" />
          <span class="form-hint">缩小页面内容间距（演示设置，不会持久化）。</span>
        </el-form-item>
        <el-form-item label="状态通知">
          <el-switch v-model="notifications" />
          <span class="form-hint">在重要任务完成时显示通知。</span>
        </el-form-item>
      </el-form>
    </el-card>
  </section>
</template>
