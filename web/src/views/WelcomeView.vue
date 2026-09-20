<script setup lang="ts">
import { ArrowRight, Grid } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <main class="welcome-page">
    <section class="welcome-panel">
      <div class="welcome-mark"><el-icon><Grid /></el-icon></div>
      <p class="welcome-eyebrow">DEVKIT WORKSPACE</p>
      <h1>欢迎使用 Devkit</h1>
      <p class="welcome-copy">
        一个面向模块化业务的统一工作台。菜单由服务端组织，页面由各客户端安全注册。
      </p>
      <el-button type="primary" size="large" round @click="router.push('/system/home')">
        进入系统
        <el-icon class="el-icon--right"><ArrowRight /></el-icon>
      </el-button>
      <div class="welcome-features">
        <span>多级导航</span>
        <span>唯一页签</span>
        <span>分端配置</span>
      </div>
    </section>
  </main>
</template>
