<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ElMessage, ElMessageBox } from "element-plus";
import {
  studyApi,
  fileApi,
  type KnowledgeBase,
  type Source,
  type SourceDetail,
} from "../../api/examStudy";
import { permissions } from "../../api/session";
import { label, date, useOperation } from "./shared";
const route = useRoute();
const router = useRouter();
const { run, busy, error } = useOperation();
const bases = ref<KnowledgeBase[]>([]);
const all = ref(false);
const selected = ref<KnowledgeBase>();
const sources = ref<Source[]>([]);
const baseDialog = ref(false);
const baseForm = ref({ name: "", description: "", tags: "" });
const baseEditing = ref<KnowledgeBase>();
const sourceDialog = ref(false);
const sourceEditing = ref<Source>();
const sourceForm = ref({ title: "", text: "", fileId: null as string | null });
const upload = ref<File>();
const sourceMode = ref("file");
const preview = ref<SourceDetail>();
const previewOpen = ref(false);
async function load() {
  bases.value = await studyApi.bases(all.value);
  if (selected.value)
    selected.value = bases.value.find((x) => x.id === selected.value!.id);
}
async function select(kb: KnowledgeBase) {
  selected.value = kb;
  await router.replace({ query: { knowledgeBaseId: kb.id } });
  sources.value = await studyApi.sources(kb.id);
}
function editBase(kb?: KnowledgeBase) {
  baseEditing.value = kb;
  baseForm.value = {
    name: kb?.name ?? "",
    description: kb?.description ?? "",
    tags: kb?.tags ?? "",
  };
  baseDialog.value = true;
}
async function saveBase() {
  await run(async () => {
    const kb = await studyApi.saveBase(
      { ...baseForm.value, revision: baseEditing.value?.revision },
      baseEditing.value?.id,
    );
    baseDialog.value = false;
    await load();
    await select(kb);
  });
}
async function removeBase() {
  if (!selected.value) return;
  try {
    await ElMessageBox.confirm(
      "归档此知识库及资料？原文件会继续保留，题目停止用于新练习。",
      "归档知识库",
      { type: "warning" },
    );
  } catch {
    return;
  }
  await run(async () => {
    await studyApi.deleteBase(selected.value!.id);
    selected.value = undefined;
    sources.value = [];
    await load();
  });
}
async function editSource(source?: Source) {
  sourceEditing.value = source;
  sourceForm.value = {
    title: source?.title ?? "",
    text: source?.text ?? "",
    fileId: source?.fileId ?? null,
  };
  upload.value = undefined;
  sourceMode.value = source ? "manual" : "file";
  if (source) {
    const detail = await studyApi.source(source.id);
    sourceForm.value.text =
      detail.versions.find((x) => x.revision === source.revision)?.text ??
      source.text;
  }
  sourceDialog.value = true;
}
function chooseFile(e: Event) {
  upload.value = (e.target as HTMLInputElement).files?.[0];
  if (upload.value && !sourceForm.value.title)
    sourceForm.value.title = upload.value.name;
}
async function saveSource() {
  if (!selected.value) return;
  await run(async () => {
    let fileId = sourceForm.value.fileId;
    if (sourceMode.value === "file") {
      if (!upload.value) throw new Error("请选择 PDF 或 DOCX 文件");
      fileId = (await fileApi.upload(upload.value)).id;
    }
    await studyApi.saveSource(
      selected.value!.id,
      {
        title: sourceForm.value.title,
        fileId,
        text: sourceMode.value === "manual" ? sourceForm.value.text : "",
        revision: sourceEditing.value?.revision,
      },
      sourceEditing.value?.id,
    );
    sourceDialog.value = false;
    sources.value = await studyApi.sources(selected.value!.id);
    ElMessage.success("已保存原件，后台正在处理。");
  });
}
async function show(source: Source) {
  await run(async () => {
    preview.value = await studyApi.source(source.id);
    previewOpen.value = true;
  });
}
async function removeSource(source: Source) {
  try {
    await ElMessageBox.confirm(
      "归档此资料？原文件继续保留，引用题目将标记待复核。",
      "归档资料",
      { type: "warning" },
    );
  } catch {
    return;
  }
  await run(async () => {
    await studyApi.deleteSource(source.id);
    sources.value = await studyApi.sources(selected.value!.id);
  });
}
let timer: ReturnType<typeof setInterval> | undefined;
onMounted(() => {
  void run(async () => {
    await load();
    const id = String(route.query.knowledgeBaseId ?? "");
    const kb = bases.value.find((x) => x.id === id);
    if (kb) await select(kb);
  });
  timer = setInterval(() => {
    if (
      route.params.routeKey === "study-knowledge" &&
      selected.value &&
      sources.value.some((s) => s.status !== "ready")
    )
      void studyApi
        .sources(selected.value.id)
        .then((x) => {
          sources.value = x;
        })
        .catch(() => {});
  }, 5000);
});
onUnmounted(() => clearInterval(timer));
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div>
        <h2>知识库</h2>
        <p>按制度领域整理资料，让每次查询和每道题都有出处。</p>
      </div>
      <div class="study-actions">
        <el-switch
          v-if="permissions?.administrator"
          v-model="all"
          active-text="全部用户"
          @change="run(load)"
        /><el-button type="primary" @click="editBase()">新建知识库</el-button>
      </div>
    </header>
    <el-alert
      v-if="error"
      class="study-error"
      :title="error"
      type="error"
      :closable="false"
    />
    <div class="study-split" v-loading="busy">
      <aside>
        <button
          v-for="kb in bases"
          :key="kb.id"
          class="study-list-item"
          :class="{ active: selected?.id === kb.id }"
          @click="run(() => select(kb))"
        >
          <strong>{{ kb.name }}</strong
          ><small>{{ kb.description || "暂无说明" }}</small
          ><small v-if="kb.tags" style="margin-top: 8px">{{ kb.tags }}</small
          ><small v-if="all" class="study-small-id"
            >所属用户 {{ kb.ownerId }}</small
          ></button
        ><el-empty
          v-if="!bases.length"
          description="先建立一个知识库"
          :image-size="70"
        />
      </aside>
      <section v-if="selected">
        <div class="study-card">
          <div class="study-heading">
            <div>
              <h2>{{ selected.name }}</h2>
              <p>{{ selected.description }}</p>
            </div>
            <div class="study-actions">
              <el-button @click="editBase(selected)">编辑</el-button
              ><el-button type="danger" text @click="removeBase"
                >归档</el-button
              >
            </div>
          </div>
          <div class="study-toolbar">
            <el-button type="primary" @click="run(() => editSource())"
              >上传资料 / 手动补充</el-button
            ><el-button
              @click="
                run(async () => {
                  sources = await studyApi.sources(selected!.id);
                })
              "
              >刷新状态</el-button
            ><span class="study-muted">原文件和历史版本持续保留</span>
          </div>
          <el-table :data="sources"
            ><el-table-column
              prop="title"
              label="资料名称"
              min-width="190"
            /><el-table-column label="状态" width="100"
              ><template #default="{ row }"
                ><el-tag :type="row.status === 'ready' ? 'success' : 'info'">{{
                  label(row.status)
                }}</el-tag></template
              ></el-table-column
            ><el-table-column label="版本" width="75"
              ><template #default="{ row }"
                >v{{ row.revision }}</template
              ></el-table-column
            ><el-table-column label="操作" width="235"
              ><template #default="{ row }"
                ><el-button link type="primary" @click="show(row)"
                  >预览</el-button
                ><el-button link @click="run(() => editSource(row))"
                  >修改</el-button
                ><el-button
                  link
                  @click="
                    run(async () => {
                      await studyApi.reindex(row.id);
                      await load();
                      ElMessage.success('重建任务已创建');
                    })
                  "
                  >重建索引</el-button
                ><el-button link type="danger" @click="removeSource(row)"
                  >归档</el-button
                ></template
              ></el-table-column
            ></el-table
          >
          <p class="study-muted">
            若处理失败，可到“处理任务”查看原因并重试；扫描文件需先做 OCR。
          </p>
        </div>
      </section>
      <div v-else class="study-card study-empty">
        <el-empty description="选择知识库，开始整理你的制度资料" />
      </div>
    </div>
    <el-dialog
      v-model="baseDialog"
      title="知识库信息"
      width="min(540px,94vw)"
      append-to-body
      ><el-form label-position="top" class="study-form"
        ><el-form-item label="名称"
          ><el-input v-model="baseForm.name" maxlength="200" /></el-form-item
        ><el-form-item label="说明"
          ><el-input
            v-model="baseForm.description"
            type="textarea"
            :rows="3" /></el-form-item
        ><el-form-item label="标签"
          ><el-input
            v-model="baseForm.tags"
            placeholder="例如：人事制度、财务报销" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="baseDialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="saveBase"
          >保存</el-button
        ></template
      ></el-dialog
    >
    <el-dialog
      v-model="sourceDialog"
      :title="sourceEditing ? '更新资料 · 保留旧版本' : '丰富知识库'"
      width="min(720px,96vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="标题"
          ><el-input v-model="sourceForm.title" maxlength="200" /></el-form-item
        ><el-radio-group v-model="sourceMode"
          ><el-radio-button value="file">{{
            sourceEditing ? "替换文件" : "上传文件"
          }}</el-radio-button
          ><el-radio-button value="manual">{{
            sourceEditing ? "校正提取内容" : "手动条目"
          }}</el-radio-button></el-radio-group
        >
        <div
          v-if="sourceMode === 'file'"
          class="study-card"
          style="margin-top: 18px"
        >
          <input type="file" accept=".pdf,.docx" @change="chooseFile" />
          <p class="study-muted">
            文本 PDF、DOCX，默认最大 20 MiB。上传文件会永久保留。
          </p>
        </div>
        <el-form-item v-else label="正文" style="margin-top: 20px"
          ><el-input
            v-model="sourceForm.text"
            type="textarea"
            :rows="13"
            placeholder="填写制度条款、补充说明或校正后的文本" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="sourceDialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="saveSource"
          >保存并建立索引</el-button
        ></template
      ></el-dialog
    >
    <el-drawer
      v-model="previewOpen"
      :title="preview?.source.title"
      size="min(850px,96vw)"
      append-to-body
      ><template v-if="preview"
        ><el-alert
          v-if="preview.source.warning"
          :title="preview.source.warning"
          type="warning"
          :closable="false"
        /><el-tabs
          ><el-tab-pane label="知识片段"
            ><div
              v-for="c in preview.chunks.filter((x) => x.ordinal < 100000)"
              :key="c.id"
              class="study-card"
            >
              <p class="study-muted">{{ c.location }} · {{ c.heading }}</p>
              <div class="study-text">{{ c.text }}</div>
            </div></el-tab-pane
          ><el-tab-pane label="历史版本"
            ><div v-for="v in preview.versions" :key="v.id" class="study-card">
              <div class="study-toolbar">
                <strong>v{{ v.revision }} · {{ v.title }}</strong
                ><span class="study-muted">{{ date(v.createdAtUtc) }}</span
                ><el-button
                  v-if="v.fileId"
                  link
                  type="primary"
                  @click="run(() => fileApi.download(v.fileId!, v.title))"
                  >下载原件</el-button
                >
              </div>
              <el-collapse
                ><el-collapse-item title="查看提取内容"
                  ><div class="study-text">{{ v.text }}</div></el-collapse-item
                ></el-collapse
              >
            </div></el-tab-pane
          ></el-tabs
        ></template
      ></el-drawer
    >
  </main>
</template>
