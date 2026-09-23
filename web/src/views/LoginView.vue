<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { login } from "../api/auth";
import { loadPermissions, saveSession } from "../api/session";
const account = ref("");
const password = ref("");
const busy = ref(false);
const error = ref("");
const route = useRoute();
const router = useRouter();
async function submit() {
  busy.value = true;
  error.value = "";
  try {
    saveSession(await login(account.value, password.value));
    await loadPermissions();
    const target = String(route.query.redirect ?? "/system/home");
    await router.replace(
      target.startsWith("/system/") ? target : "/system/home",
    );
  } catch {
    error.value = "登录失败，请检查账号、密码及服务端状态。";
  } finally {
    busy.value = false;
  }
}
</script>
<template>
  <div class="welcome-page">
    <section class="welcome-panel login-panel">
      <p class="welcome-eyebrow">DEVKIT · WORKSPACE</p>
      <h1 style="font-size: 34px">登录工作台</h1>
      <p class="welcome-copy">制度知识 · 快速查询 · 个人备考</p>
      <el-form @submit.prevent="submit" label-position="top"
        ><el-form-item label="账号"
          ><el-input
            v-model="account"
            autocomplete="username"
            placeholder="用户名或邮箱"
            size="large" /></el-form-item
        ><el-form-item label="密码"
          ><el-input
            v-model="password"
            type="password"
            show-password
            autocomplete="current-password"
            size="large"
            @keyup.enter="submit"
        /></el-form-item>
        <el-alert
          v-if="error"
          :title="error"
          type="error"
          :closable="false"
        /><el-button
          type="primary"
          size="large"
          :loading="busy"
          style="width: 100%; margin-top: 20px"
          @click="submit"
          >登录</el-button
        ></el-form
      >
      <el-button
        text
        style="color: #ccd6f3; margin-top: 16px"
        @click="router.push('/')"
        >返回欢迎页</el-button
      >
    </section>
  </div>
</template>
<style scoped>
.login-panel {
  max-width: 480px;
  text-align: left;
  padding: 42px;
}
.login-panel :deep(.el-form-item__label) {
  color: #cbd5ee;
}
</style>
