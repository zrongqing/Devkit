<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import {
  studyApi,
  type KnowledgeBase,
  type Project,
} from "../../api/examStudy";
import { permissions, session } from "../../api/session";
import { parse, useOperation } from "./shared";
import ProjectWorkspace from "./ProjectWorkspace.vue";
const route = useRoute();
const router = useRouter();
const { run, busy, error } = useOperation();
const projects = ref<Project[]>([]);
const bases = ref<KnowledgeBase[]>([]);
const all = ref(false);
const selected = ref<Project>();
const dialog = ref(false);
const editing = ref<Project>();
const workspace = ref<InstanceType<typeof ProjectWorkspace>>();
const form = ref({
  name: "",
  description: "",
  targetDate: null as string | null,
  knowledgeBaseIds: [] as string[],
});
async function load() {
  [projects.value, bases.value] = await Promise.all([
    studyApi.projects(all.value),
    studyApi.bases(all.value),
  ]);
  if (selected.value)
    selected.value = projects.value.find((x) => x.id === selected.value!.id);
}
async function select(p: Project) {
  await workspace.value?.flush();
  selected.value = p;
  await router.replace({ query: { projectId: p.id } });
}
function edit(p?: Project) {
  editing.value = p;
  form.value = {
    name: p?.name ?? "",
    description: p?.description ?? "",
    targetDate: p?.targetDate ?? null,
    knowledgeBaseIds: p ? parse(p.knowledgeBaseIdsJson) : [],
  };
  dialog.value = true;
}
async function save() {
  await run(async () => {
    await workspace.value?.flush();
    const p = await studyApi.saveProject(
      { ...form.value, revision: editing.value?.revision },
      editing.value?.id,
    );
    dialog.value = false;
    await load();
    await select(p);
  });
}
onMounted(() => {
  void run(async () => {
    await load();
    selected.value = projects.value.find(
      (x) => x.id === String(route.query.projectId ?? ""),
    );
  });
});
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div>
        <h2>备考项目 · 快速查询</h2>
        <p>为每场考试关联资料，在同一范围内查询、练习和复习。</p>
      </div>
      <div class="study-actions">
        <el-switch
          v-if="permissions?.administrator"
          v-model="all"
          active-text="全部用户"
          @change="run(load)"
        /><el-button type="primary" @click="edit()">新建项目</el-button>
      </div>
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    />
    <div class="study-toolbar">
      <el-select
        :model-value="selected?.id ?? ''"
        placeholder="选择备考项目"
        style="min-width: 280px"
        @change="
          (id: string) => run(() => select(projects.find((x) => x.id === id)!))
        "
        ><el-option
          v-for="p in projects"
          :key="p.id"
          :value="p.id"
          :label="p.name" /></el-select
      ><el-button v-if="selected" @click="edit(selected)"
        >项目设置 / 关联知识库</el-button
      ><el-button @click="run(load)">刷新</el-button>
    </div>
    <ProjectWorkspace
      ref="workspace"
      v-if="selected"
      :key="selected.id + ':' + selected.revision"
      :project="selected"
      :bases="bases"
    />
    <div v-else class="study-card study-empty" v-loading="busy">
      <el-empty description="选择或创建项目，关联知识库后即可快速查询" />
    </div>
    <el-dialog
      v-model="dialog"
      title="备考项目"
      width="min(620px,94vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="考试 / 项目名称"
          ><el-input v-model="form.name" maxlength="200" /></el-form-item
        ><el-form-item label="说明"
          ><el-input
            v-model="form.description"
            type="textarea"
            :rows="3" /></el-form-item
        ><el-form-item label="目标日期"
          ><el-date-picker
            v-model="form.targetDate"
            type="date"
            value-format="YYYY-MM-DDTHH:mm:ss"
            clearable /></el-form-item
        ><el-form-item label="关联知识库"
          ><el-select
            v-model="form.knowledgeBaseIds"
            multiple
            style="width: 100%"
            placeholder="可以关联多个知识库"
            ><el-option
              v-for="b in bases.filter(
                (x) => x.ownerId === (editing?.ownerId ?? session?.user.id),
              )"
              :key="b.id"
              :label="b.name"
              :value="b.id" /></el-select
        ></el-form-item>
        <p class="study-muted">
          查询和抽题仅覆盖关联的知识库，取消关联后立即从后续查询与组卷范围移除。
        </p></el-form
      ><template #footer
        ><el-button @click="dialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="save"
          >保存</el-button
        ></template
      ></el-dialog
    >
  </main>
</template>
