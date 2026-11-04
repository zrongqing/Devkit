<script setup lang="ts">
import { ArrowDown, Document, Upload } from "@element-plus/icons-vue";
import DataTable from "../../components/DataTable";
import { tableQueries } from "../../api/tableQueries";
import { computed, onMounted, ref } from "vue";
import { permissions } from "../../api/session";
import { ElMessageBox } from "element-plus";
import { fileApi, type StoredFile } from "../../api/examStudy";
import { bytes, date, label, useOperation } from "../exam-study/shared";
const { run, busy, error } = useOperation();
const files = ref<StoredFile[]>([]);
const archived = ref(false);
const uploadInput = ref<HTMLInputElement>();
const externalFilters = computed(() => archived.value ? [{ key: 'unreferenced', label: '引用', value: '无引用' }] : []);
function resetTableFilters() {
  archived.value = false
}
const references = ref<Awaited<ReturnType<typeof fileApi.references>>>([]);
const referenceDialog = ref(false);
const referenceFileId = ref("");

async function load() {
  files.value = [];
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

const tableValues = {
  status: ({ status }: { status: string }) => label(status),
  uploadedAt: ({ createdAtUtc }: { createdAtUtc: string }) => date(createdAtUtc),
};
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div>
        <h2>文件管理</h2>
        <p>统一管理全服务的上传文件。业务资料归档后，原件仍保留在这里。</p>
      </div>
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    />
    <div class="study-card" v-loading="busy">
      <DataTable table-key="files" @reset-filters="resetTableFilters" :external-filters="externalFilters" @remove-filter="archived = false" :load-page="tableQueries.files('', archived)" :query-context="String(archived)" :data="files"
        >
        <template #filters><el-switch v-model="archived" active-text="仅无引用文件" /></template>
        <template #actions><el-button type="primary" :icon="Upload" @click="uploadInput?.click()">上传文件</el-button><input ref="uploadInput" type="file" hidden @change="upload" /></template>
        <el-table-column type="expand"
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
                    referenceFileId = row.id;
                    references = [];
                    referenceDialog = true;
                  })
                "
                >查看引用记录</el-button
              >
            </div></template
          ></el-table-column
        ><el-table-column
          prop="name"
          label="文件名称"
          min-width="240"
        ><template #default="{ row }"><span class="file-name-cell"><el-icon><Document /></el-icon><span>{{ row.name }}</span></span></template></el-table-column><el-table-column prop="length" label="大小" width="105"
          ><template #default="{ row }">{{
            bytes(row.length)
          }}</template></el-table-column
        ><el-table-column :table-value="tableValues.status" prop="status" label="状态" width="110"
          ><template #default="{ row }"
            ><el-tag :type="row.status === 'ready' ? 'success' : 'info'">{{
              label(row.status)
            }}</el-tag></template
          ></el-table-column
        ><el-table-column
          prop="referenceCount"
          label="引用"
          width="70"
        /><el-table-column :table-value="tableValues.uploadedAt" prop="createdAtUtc" label="上传时间" width="190"
          ><template #default="{ row }">{{
            date(row.createdAtUtc)
          }}</template></el-table-column
        ><el-table-column column-key="actions" label="操作" width="170"
          ><template #default="{ row }"
            ><el-button
              v-if="row.status === 'ready'"
              link
              type="primary"
              @click="run(() => fileApi.download(row.id, row.name))"
              >下载</el-button
            ><el-dropdown v-if="permissions?.administrator && row.referenceCount === 0 && row.status !== 'purged'" trigger="click" @command="purge(row)">
              <el-button link>更多<el-icon><ArrowDown /></el-icon></el-button>
              <template #dropdown><el-dropdown-menu><el-dropdown-item command="purge" class="file-purge-action">永久删除</el-dropdown-item></el-dropdown-menu></template>
            </el-dropdown>
            </template
          ></el-table-column
        ></DataTable
      >
    </div>
    <el-dialog
      v-model="referenceDialog"
      title="业务引用"
      width="min(760px,94vw)"
      append-to-body
      ><DataTable v-if="referenceDialog" table-key="file-references" :load-page="tableQueries.references(referenceFileId)" :query-context="referenceFileId" :data="references"
        ><el-table-column
          prop="module"
          label="模块"
          width="130" /><el-table-column
          prop="entityId"
          label="资料版本 ID"
          min-width="280" /></DataTable
    ></el-dialog>
  </main>
</template>

<style scoped>
.file-name-cell { display: inline-flex; align-items: center; gap: 10px; max-width: 100%; vertical-align: middle; }
.file-name-cell > .el-icon { flex: none; color: var(--el-color-primary); font-size: 20px; }
.file-name-cell > span { overflow: hidden; text-overflow: ellipsis; }
.file-purge-action { color: var(--el-color-danger); }
</style>
