<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { useRoute } from "vue-router";
import { ElMessage, ElMessageBox } from "element-plus";
import {
  fileApi,
  studyApi,
  type Location,
  type Job,
} from "../../api/examStudy";
import { bytes, date, label, useOperation } from "../exam-study/shared";
const { run, busy, error } = useOperation();
const route = useRoute();
const locations = ref<Location[]>([]);
const jobs = ref<Job[]>([]);
const add = ref(false);
const name = ref("");
const path = ref("");
const dialog = ref(false);
const source = ref("");
const target = ref("");
async function load() {
  [locations.value, jobs.value] = await Promise.all([
    fileApi.locations(),
    studyApi.jobs(),
  ]);
  jobs.value = jobs.value.filter((x) =>
    ["migrate", "cleanup"].includes(x.kind),
  );
}
async function addLocation() {
  await run(async () => {
    await fileApi.addLocation(name.value, path.value);
    add.value = false;
    await load();
  });
}
async function migrate() {
  try {
    await ElMessageBox.confirm(
      "开始后，新上传将写入目标位置，旧文件保持可读。文件逐个复制并校验后切换，旧副本单独清理。",
      "开始迁移",
    );
  } catch {
    return;
  }
  await run(async () => {
    await fileApi.migrate(source.value, target.value);
    dialog.value = false;
    await load();
    ElMessage.success("迁移任务已创建");
  });
}
async function cleanup(id: string) {
  try {
    await ElMessageBox.confirm(
      "重新校验有效副本后，永久清理本次迁移留下的旧副本。是否继续？",
      "清理旧副本",
      { type: "warning" },
    );
  } catch {
    return;
  }
  await run(async () => {
    await fileApi.cleanup(id);
    await load();
    ElMessage.success("清理任务已创建，可在记录中查看进度。");
  });
}
let timer: ReturnType<typeof setInterval> | undefined;
onMounted(() => {
  void run(load);
  timer = setInterval(() => {
    if (
      route.params.routeKey === "system-storage" &&
      jobs.value.some((x) => ["queued", "running"].includes(x.status))
    )
      void load().catch(() => {});
  }, 5000);
});
onUnmounted(() => clearInterval(timer));
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div>
        <h2>存储与迁移</h2>
        <p>文件位置可更换，文件 ID 和业务引用持续有效。</p>
      </div>
      <div class="study-actions">
        <el-button @click="run(load)">刷新</el-button
        ><el-button @click="add = true">登记存储位置</el-button
        ><el-button
          type="primary"
          @click="
            source = locations.find((x) => x.writable)?.id ?? '';
            target = '';
            dialog = true;
          "
          >迁移文件</el-button
        >
      </div>
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    />
    <div class="study-grid" v-loading="busy">
      <article v-for="l in locations" :key="l.id" class="study-card">
        <div class="study-toolbar">
          <strong>{{ l.name }}</strong
          ><el-tag v-if="l.writable" type="success">新上传位置</el-tag>
        </div>
        <p class="study-small-id">{{ l.rootPath }}</p>
        <p class="study-stat">{{ bytes(l.availableBytes) }}</p>
        <p class="study-muted">
          磁盘可用空间 · {{ l.fileCount }} 个有效文件 ·
          {{ bytes(l.totalBytes) }}
        </p>
      </article>
    </div>
    <div class="study-card">
      <h3>迁移与清理记录</h3>
      <el-table :data="jobs"
        ><el-table-column label="任务" width="120"
          ><template #default="{ row }">{{
            label(row.kind)
          }}</template></el-table-column
        ><el-table-column label="创建时间" width="190"
          ><template #default="{ row }">{{
            date(row.createdAtUtc)
          }}</template></el-table-column
        ><el-table-column label="状态" width="110"
          ><template #default="{ row }"
            ><el-tag>{{ label(row.status) }}</el-tag></template
          ></el-table-column
        ><el-table-column
          prop="error"
          label="说明"
          min-width="220"
        /><el-table-column label="操作" width="210"
          ><template #default="{ row }"
            ><el-button
              v-if="row.status === 'failed' || row.status === 'waiting'"
              link
              type="primary"
              @click="
                run(async () => {
                  await studyApi.retry(row.id);
                  await load();
                })
              "
              >重试任务</el-button
            ><el-button
              v-if="row.kind === 'migrate' && row.status === 'completed'"
              link
              type="danger"
              @click="cleanup(row.id)"
              >校验并清理旧副本</el-button
            ></template
          ></el-table-column
        ></el-table
      >
      <p class="study-muted">
        应用文件迁移支持中断恢复。Docker
        虚拟磁盘和数据库使用各自的离线迁移流程。
      </p>
    </div>
    <el-dialog
      v-model="add"
      title="登记存储位置"
      width="min(620px,94vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="名称"
          ><el-input
            v-model="name"
            placeholder="例如：扩容磁盘" /></el-form-item
        ><el-form-item label="服务端绝对路径"
          ><el-input v-model="path" placeholder="例如：E:\server data\devkit"
        /></el-form-item>
        <p class="study-muted">
          系统将检查路径、写入权限以及是否与现有位置重叠。登记不会自动移动已有文件。
        </p></el-form
      ><template #footer
        ><el-button @click="add = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="addLocation"
          >登记</el-button
        ></template
      ></el-dialog
    >
    <el-dialog
      v-model="dialog"
      title="迁移文件"
      width="min(620px,94vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="源位置"
          ><el-select v-model="source" style="width: 100%"
            ><el-option
              v-for="l in locations"
              :key="l.id"
              :label="l.name + ' · ' + l.rootPath"
              :value="l.id" /></el-select></el-form-item
        ><el-form-item label="目标位置"
          ><el-select v-model="target" style="width: 100%"
            ><el-option
              v-for="l in locations.filter((x) => x.id !== source)"
              :key="l.id"
              :label="l.name + ' · ' + l.rootPath"
              :value="l.id" /></el-select
        ></el-form-item>
        <p>复制 → SHA-256 校验 → 切换读取位置 → 单独清理旧副本。</p></el-form
      ><template #footer
        ><el-button @click="dialog = false">取消</el-button
        ><el-button
          type="primary"
          :loading="busy"
          :disabled="!source || !target"
          @click="migrate"
          >开始迁移</el-button
        ></template
      ></el-dialog
    >
  </main>
</template>
