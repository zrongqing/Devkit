<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import PageDialog from "../../components/PageDialog.vue";
import { session, saveSession, hasPermission } from "../../api/session";
import { identityApi } from "../../api/examStudy";
import { useOperation } from "../../modules/exam-study/shared";

const currentPassword = ref("");
const newPassword = ref("");
const passwordOpen = ref(false);
const { run, busy, error } = useOperation();
const router = useRouter();
function openPasswordDialog() {
  currentPassword.value = "";
  newPassword.value = "";
  error.value = "";
  passwordOpen.value = true;
}
async function changePassword() {
  await run(async () => {
    await identityApi.password(currentPassword.value, newPassword.value);
    passwordOpen.value = false;
    saveSession(null);
    await router.replace("/login");
  });
}
</script>

<template>
  <section class="page-shell">
    <div v-if="session" class="study-toolbar">
      <el-button @click="openPasswordDialog">修改登录密码</el-button>
    </div>
    <el-card shadow="never" class="settings-card">
      <template #header>文件存储参数</template>
      <p>维护文件保存位置，配置新存储目录并执行存量文件迁移。</p>
      <el-button v-if="hasPermission('system.storage.manage')" type="primary" @click="router.push('/system/system-storage')">配置存储位置</el-button>
      <p v-else class="study-muted">需要存储与迁移权限才能配置。</p>
    </el-card>
    <PageDialog v-model="passwordOpen" title="修改登录密码" :width="450" :closable="!busy">
      <el-alert v-if="error" :title="error" type="error" :closable="false" class="study-error" />
      <el-form label-position="top">
        <el-form-item label="当前密码">
          <el-input v-model="currentPassword" type="password" show-password autocomplete="current-password" />
        </el-form-item>
        <el-form-item label="新密码">
          <el-input v-model="newPassword" type="password" show-password autocomplete="new-password"
            placeholder="至少12位，包含大写、小写和数字" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="busy" @click="passwordOpen = false">取消</el-button>
        <el-button type="primary" :loading="busy" :disabled="!currentPassword || !newPassword" @click="changePassword">
          修改并重新登录
        </el-button>
      </template>
    </PageDialog>
  </section>
</template>
