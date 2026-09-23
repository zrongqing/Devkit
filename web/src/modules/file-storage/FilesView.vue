<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { permissions } from "../../api/session";
import { ElMessageBox } from "element-plus";
import { fileApi, type StoredFile } from "../../api/examStudy";
import { bytes, date, label, useOperation } from "../exam-study/shared";
const { run, busy, error } = useOperation();
const files = ref<StoredFile[]>([]);
const search = ref("");
const archived = ref(false);
const references = ref<Awaited<ReturnType<typeof fileApi.references>>>([]);
const referenceDialog = ref(false);
const visible = computed(() =>
  files.value.filter(
    (f) =>
      f.name.includes(search.value) &&
      (!archived.value || f.referenceCount === 0),
  ),
);
async function load() {
  files.value = await fileApi.list();
}
async function upload(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  await run(async () => {
    await fileApi.upload(file, "general");
    await load();
  });
  input.value = "";
}
async function purge(f: StoredFile) {
  try {
    await ElMessageBox.confirm(
      `永久删除“${f.name}”？此操作删除磁盘原件，无法恢复。`,
      "永久删除",
      { type: "warning", confirmButtonText: "永久删除" },
    );
  } catch {
    return;
  }
  await run(async () => {
    await fileApi.purge(f.id);
    await load();
  });
}
onMounted(() => {
  void run(load);
});
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div>
        <h2>文件管理</h2>
        <p>统一管理全服务的上传文件。业务资料归档后，原件仍保留在这里。</p>
      </div>
      <el-button @click="run(load)">刷新</el-button>
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    />
    <div class="study-card" v-loading="busy">
      <div class="study-toolbar">
        <el-input
          v-model="search"
          placeholder="查找文件名"
          clearable
          style="width: 260px"
        /><el-switch v-model="archived" active-text="仅无引用文件" /><label
          class="el-button el-button--primary"
          style="cursor: pointer"
          >上传通用文件<input type="file" hidden @change="upload"
        /></label>
      </div>
      <el-table :data="visible"
        ><el-table-column type="expand"
          ><template #default="{ row }"
            ><div style="padding: 15px 30px">
              <p class="study-small-id">文件 ID：{{ row.id }}</p>
              <p class="study-small-id">SHA-256：{{ row.sha256 }}</p>
              <p class="study-small-id">所属用户：{{ row.ownerId }}</p>
              <p>
                业务引用 {{ row.referenceCount }} 处 · 用途 {{ row.purpose }}
              </p>
              <el-button
                text
                @click="
                  run(async () => {
                    references = await fileApi.references(row.id);
                    referenceDialog = true;
                  })
                "
                >查看引用记录</el-button
              >
            </div></template
          ></el-table-column
        ><el-table-column
          prop="name"
          label="原文件名"
          min-width="220"
        /><el-table-column label="大小" width="105"
          ><template #default="{ row }">{{
            bytes(row.length)
          }}</template></el-table-column
        ><el-table-column label="状态" width="110"
          ><template #default="{ row }"
            ><el-tag :type="row.status === 'ready' ? 'success' : 'info'">{{
              label(row.status)
            }}</el-tag></template
          ></el-table-column
        ><el-table-column
          prop="referenceCount"
          label="引用"
          width="70"
        /><el-table-column label="上传时间" width="190"
          ><template #default="{ row }">{{
            date(row.createdAtUtc)
          }}</template></el-table-column
        ><el-table-column label="操作" width="170"
          ><template #default="{ row }"
            ><el-button
              v-if="row.status === 'ready'"
              link
              type="primary"
              @click="run(() => fileApi.download(row.id, row.name))"
              >下载</el-button
            ><el-button
              v-if="
                permissions?.administrator &&
                row.referenceCount === 0 &&
                row.status !== 'purged'
              "
              link
              type="danger"
              @click="purge(row)"
              >永久删除</el-button
            ></template
          ></el-table-column
        ></el-table
      >
    </div>
    <el-dialog
      v-model="referenceDialog"
      title="业务引用"
      width="min(760px,94vw)"
      append-to-body
      ><el-table :data="references"
        ><el-table-column
          prop="module"
          label="模块"
          width="130" /><el-table-column
          prop="entityId"
          label="资料版本 ID"
          min-width="280" /></el-table
    ></el-dialog>
  </main>
</template>
