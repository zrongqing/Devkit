<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { ElMessage } from "element-plus";
import PageDialog from "../../components/PageDialog.vue";
import { session, saveSession, hasPermission } from "../../api/session";
import { identityApi } from "../../api/examStudy";
import { manageSystemSettings } from "../../api/systemSettings";
import { useOperation } from "../../modules/exam-study/shared";
import {
  browserTimeZone, displayTimeZone, loadSystemSettings, saveSystemTimeZone,
  systemSettingsError, systemSettingsLoaded, systemSettingsLoading, systemSettingsSaving,
  systemTimeZone, unsupportedTimeZone,
} from "../../stores/systemSettings";
import { formatTime, supportsTimeZone } from "../../utils/time";

const timeZoneNames: Record<string, string> = {
  "Asia/Shanghai": "中国标准时间",
  "Asia/Tokyo": "东京",
  "Asia/Singapore": "新加坡",
  "Asia/Kolkata": "印度",
  "Asia/Calcutta": "印度",
  "Europe/London": "伦敦",
  "Europe/Paris": "巴黎",
  "America/New_York": "纽约",
  "America/Los_Angeles": "洛杉矶",
  "Australia/Sydney": "悉尼",
};
const availableZones = [...new Set([
  ...Object.keys(timeZoneNames).filter(supportsTimeZone),
  ...(Intl.supportedValuesOf?.("timeZone") ?? []),
])].filter(zone => zone.includes("/") && !zone.startsWith("Etc/")).sort();
const selectableZones = computed(() => [...new Set([
  ...availableZones, ...(systemTimeZone.value && systemTimeZone.value !== "UTC" ? [systemTimeZone.value] : []),
])]);
const followBrowser = "browser";
const timeZoneDraft = ref<string>();
const timeZoneSaveError = ref("");
const selectedTimeZone = computed(() => timeZoneDraft.value ?? systemTimeZone.value ?? followBrowser);
const canSaveTimeZone = computed(() => hasPermission(manageSystemSettings) && systemSettingsLoaded.value &&
  selectedTimeZone.value !== (systemTimeZone.value ?? followBrowser));
const timePreview = computed(() => {
  const zone = selectedTimeZone.value === followBrowser ? browserTimeZone.value : selectedTimeZone.value;
  return formatTime(Date.now(), supportsTimeZone(zone) ? zone : displayTimeZone.value);
});

function selectTimeZone(value: string) {
  timeZoneDraft.value = value;
  timeZoneSaveError.value = "";
}
async function saveTimeZone() {
  if (!canSaveTimeZone.value || systemSettingsSaving.value) return;
  timeZoneSaveError.value = "";
  try {
    await saveSystemTimeZone(selectedTimeZone.value === followBrowser ? null : selectedTimeZone.value);
    timeZoneDraft.value = undefined;
    ElMessage.success("显示时区已保存。");
  } catch (reason) {
    timeZoneSaveError.value = reason instanceof Error ? reason.message : "显示时区保存失败，请重试。";
  }
}

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
    <el-card shadow="never" class="settings-card time-zone-card">
      <template #header>显示时区</template>
      <div v-if="systemSettingsError" class="study-error">
        <el-alert :title="systemSettingsError" type="warning" show-icon :closable="false" />
        <el-button link type="primary" :loading="systemSettingsLoading" @click="loadSystemSettings">重试</el-button>
      </div>
      <el-alert v-if="unsupportedTimeZone" class="study-error" type="warning" :closable="false"
        :title="`当前浏览器不支持 ${systemTimeZone}，暂时使用浏览器时区。`" />
      <el-form v-if="hasPermission(manageSystemSettings)" label-position="top" @submit.prevent="saveTimeZone">
        <el-form-item label="地区时区" :error="timeZoneSaveError">
          <el-select :model-value="selectedTimeZone" :disabled="!systemSettingsLoaded || systemSettingsSaving"
            filterable aria-label="地区时区" @update:model-value="selectTimeZone">
            <el-option label="跟随浏览器" :value="followBrowser" />
            <el-option label="UTC" value="UTC" />
            <el-option v-for="zone in selectableZones" :key="zone" :value="zone"
              :label="timeZoneNames[zone] ? `${timeZoneNames[zone]}（${zone}）` : zone" />
          </el-select>
        </el-form-item>
        <el-button type="primary" native-type="submit" :disabled="!canSaveTimeZone" :loading="systemSettingsSaving">保存时区</el-button>
      </el-form>
      <p class="study-muted">当前生效：{{ systemTimeZone ? displayTimeZone : `跟随浏览器（${displayTimeZone}）` }}</p>
      <p class="study-muted">时间示例：{{ timePreview }}</p>
    </el-card>
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

<style scoped>
.time-zone-card { margin-bottom: 16px; }
.time-zone-card .el-select { width: 340px; max-width: 100%; }
</style>
