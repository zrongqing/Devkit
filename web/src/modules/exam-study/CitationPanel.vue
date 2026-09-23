<script setup lang="ts">
import { ref } from "vue";
import {
  studyApi,
  fileApi,
  type Citation,
  type SourceDetail,
} from "../../api/examStudy";
import { useOperation } from "./shared";
const props = defineProps<{ citations: Citation[] }>();
const open = ref(false);
const selected = ref<Citation>();
const detail = ref<SourceDetail>();
const { run, busy } = useOperation();
async function show(c: Citation) {
  selected.value = c;
  detail.value = undefined;
  open.value = true;
  await run(async () => {
    detail.value = await studyApi.source(c.sourceId);
  });
}
</script>
<template>
  <div class="study-actions" style="margin-top: 12px">
    <el-button
      v-for="(c, i) in props.citations"
      :key="c.chunkId"
      link
      type="primary"
      @click="show(c)"
      >[{{ i + 1 }}] {{ c.title }} · {{ c.location }}</el-button
    >
  </div>
  <el-drawer
    v-model="open"
    :title="selected?.title"
    size="min(780px, 96vw)"
    append-to-body
    ><template v-if="selected"
      ><p class="study-muted">
        {{ selected.location }} · 原文版本 {{ selected.versionId.slice(0, 8) }}
      </p>
      <div class="study-text study-card">{{ selected.text }}</div>
      <el-button
        v-if="selected.fileId"
        :loading="busy"
        @click="run(() => fileApi.download(selected!.fileId!, selected!.title))"
        >下载原文件</el-button
      ><el-divider>完整提取内容</el-divider>
      <div v-loading="busy" class="study-text">
        {{
          detail?.versions.find((v) => v.id === selected!.versionId)?.text ??
          "原文暂时不可访问，以上保留引用时的证据片段。"
        }}
      </div></template
    ></el-drawer
  >
</template>
