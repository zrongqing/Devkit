<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { session, saveSession } from "../../api/session";
import { identityApi } from "../../api/examStudy";
import { useOperation } from "../../modules/exam-study/shared";

const compactMode = ref(false);
const notifications = ref(true);
const currentPassword = ref("");
const newPassword = ref("");
const { run, busy } = useOperation();
const router = useRouter();
async function changePassword() {
  await run(async () => {
    await identityApi.password(currentPassword.value, newPassword.value);
    saveSession(null);
    await router.replace("/login");
  });
}
</script>

<template>
  <section class="page-shell">
    <div class="page-heading">
      <div>
        <p class="page-kicker">系统</p>
        <h2>设置</h2>
      </div>
      <p>用于展示后续模块可以采用的表单布局。</p>
    </div>
    <el-card shadow="never" class="settings-card">
      <el-form label-position="top">
        <el-form-item label="紧凑模式">
          <el-switch v-model="compactMode" />
          <span class="form-hint"
            >缩小页面内容间距（演示设置，不会持久化）。</span
          >
        </el-form-item>
        <el-form-item label="状态通知">
          <el-switch v-model="notifications" />
          <span class="form-hint">在重要任务完成时显示通知。</span>
        </el-form-item>
      </el-form>
    </el-card>
    <el-card v-if="session" shadow="never" style="margin-top: 20px"
      ><template #header>修改登录密码</template
      ><el-form label-position="top" style="max-width: 450px"
        ><el-form-item label="当前密码"
          ><el-input
            v-model="currentPassword"
            type="password"
            show-password
            autocomplete="current-password" /></el-form-item
        ><el-form-item label="新密码"
          ><el-input
            v-model="newPassword"
            type="password"
            show-password
            autocomplete="new-password"
            placeholder="至少12位，包含大写、小写和数字" /></el-form-item
        ><el-button type="primary" :loading="busy" @click="changePassword"
          >修改并重新登录</el-button
        ></el-form
      ></el-card
    >
  </section>
</template>
