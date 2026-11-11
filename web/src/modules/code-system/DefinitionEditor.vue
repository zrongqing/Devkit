<script setup lang="ts">
import {
  resetLabels,
  segmentLabels,
  type Definition,
  type Segment,
  type SegmentType,
} from '../../api/codeSystem'
const model = defineModel<Definition>({ required: true })
defineProps<{ lockedPolicy: boolean; segmentErrors: string[] }>()
const types = Object.keys(segmentLabels) as SegmentType[]
function segment(type: SegmentType): Segment {
  if (type === 'literal') return { type, text: '-' }
  if (type === 'sequence') return { type, width: 4 }
  return { type }
}
function move(index: number, offset: number) {
  const rows = model.value.segments
  rows.splice(index + offset, 0, rows.splice(index, 1)[0]!)
}
</script>
<template>
  <el-form label-position="top">
    <div class="policy">
      <el-form-item label="重置周期"
        ><el-select v-model="model.reset" :disabled="lockedPolicy"
          ><el-option
            v-for="(label, key) in resetLabels"
            :key="key"
            :label="label"
            :value="key" /></el-select
      ></el-form-item>
      <el-form-item label="起值"
        ><el-input-number
          v-model="model.start"
          :min="1"
          :max="999999999999"
          :precision="0"
          :disabled="lockedPolicy"
      /></el-form-item>
      <el-form-item label="步长"
        ><el-input-number
          v-model="model.step"
          :min="1"
          :max="999999999999"
          :precision="0"
          :disabled="lockedPolicy"
      /></el-form-item>
    </div>
    <el-form-item label="编码段（从左到右）">
      <div class="segments">
        <div v-for="(s, i) in model.segments" :key="i" class="segment">
          <el-select
            :model-value="s.type"
            aria-label="段类型"
            @update:model-value="
              (t: SegmentType) => model.segments.splice(i, 1, segment(t))
            "
          >
            <el-option
              v-for="t in types"
              :key="t"
              :label="segmentLabels[t]"
              :value="t"
            />
          </el-select>
          <el-input
            v-if="s.type === 'literal'"
            v-model="s.text"
            maxlength="32"
            placeholder="例如 RK-"
            aria-label="固定文本"
            :aria-invalid="!!segmentErrors[i]"
          />
          <el-input-number
            v-else-if="s.type === 'sequence'"
            v-model="s.width"
            :min="1"
            :max="12"
            :precision="0"
            aria-label="流水位宽"
            :aria-invalid="!!segmentErrors[i]"
          />
          <span v-else>{{
            s.type === 'year' ? 'YYYY' : s.type === 'month' ? 'MM' : 'DD'
          }}</span>
          <div>
            <el-button text :disabled="i === 0" @click="move(i, -1)"
              >上移</el-button
            ><el-button
              text
              :disabled="i === model.segments.length - 1"
              @click="move(i, 1)"
              >下移</el-button
            ><el-button text type="danger" @click="model.segments.splice(i, 1)"
              >移除</el-button
            >
          </div>
          <p v-if="segmentErrors[i]" class="segment-error">
            第 {{ i + 1 }} 段：{{ segmentErrors[i] }}
          </p>
        </div>
      </div>
      <el-button
        :disabled="model.segments.length >= 16"
        @click="model.segments.push(segment('literal'))"
        >添加编码段</el-button
      >
    </el-form-item>
  </el-form>
</template>
<style scoped>
.policy,
.segment {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}
.policy .el-select {
  width: 130px;
}
.segments {
  width: 100%;
}
.segment {
  margin-bottom: 8px;
}
.segment-error {
  flex-basis: 100%;
  margin: -6px 0 0;
  color: var(--el-color-danger);
  font-size: 12px;
  line-height: 1.5;
}
.segment > .el-select {
  width: 110px;
}
.segment > .el-input,
.segment > .el-input-number,
.segment > span {
  width: 150px;
}
.segment .el-button + .el-button {
  margin-left: 2px;
}
</style>
