<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { session, saveSession, hasPermission } from "../../api/session";
import { identityApi } from "../../api/examStudy";
import { useOperation } from "../../modules/exam-study/shared";

const currentPassword = ref("");
const newPassword = ref("");
const { run, busy, error } = useOperation();
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
        <p class="page-kicker">系统管理</p>
        <h2>基础参数</h2>
      </div>
      <p>配置系统文件存储位置，维护当前账号的登录安全。</p>
    </div>
    <el-alert v-if="error" :title="error" type="error" :closable="false" class="study-error" />
    <el-card shadow="never" class="settings-card">
      <template #header>文件存储参数</template>
      <p>维护文件保存位置，配置新存储目录并执行存量文件迁移。</p>
      <el-button v-if="hasPermission('system.storage.manage')" type="primary" @click="router.push('/system/system-storage')">配置存储位置</el-button>
      <p v-else class="study-muted">需要存储与迁移权限才能配置。</p>
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
