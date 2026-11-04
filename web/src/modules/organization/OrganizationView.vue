<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { directoryApi, type Directory, type Department, type Position } from '../../api/organization'
import { useOperation } from '../exam-study/shared'

const directory = ref<Directory>({ departments: [], positions: [], users: [], members: [] })
const selected = ref('')
const department = computed(() => directory.value.departments.find(d => d.id === selected.value))
const positions = computed(() => directory.value.positions.filter(p => p.departmentId === selected.value))
const names = computed(() => new Map(directory.value.users.map(u => [u.id, u.name + (u.disabled ? '（已停用）' : '')])))
interface TreeNode extends Department { children: TreeNode[] }
const tree = computed(() => {
  const nodes = new Map(directory.value.departments.map(d => [d.id, { ...d, children: [] } as TreeNode]))
  const roots: TreeNode[] = []
  for (const node of nodes.values()) (nodes.get(node.parentId ?? '')?.children ?? roots).push(node)
  return roots
})
const dialog = ref<'department' | 'position' | 'member' | ''>('')
const form = ref({ id: '', name: '', departmentId: '', userId: '' })
const available = computed(() => directory.value.users.filter(u => !u.disabled &&
  !directory.value.members.some(m => m.positionId === form.value.id && m.userId === u.id)))
const { busy, error, run } = useOperation()
async function refresh() {
  directory.value = await directoryApi.read()
  if (!department.value) selected.value = tree.value[0]?.id ?? ''
}
function editDepartment(d?: Department) {
  form.value = { id: d?.id ?? '', name: d?.name ?? '', departmentId: d ? d.parentId ?? '' : selected.value, userId: '' }
  dialog.value = 'department'
}
function editPosition(p?: Position) {
  form.value = { id: p?.id ?? '', name: p?.name ?? '', departmentId: selected.value, userId: '' }
  dialog.value = 'position'
}
function assign(p: Position) {
  form.value = { id: p.id, name: p.name, departmentId: p.departmentId, userId: '' }
  dialog.value = 'member'
}
async function save() {
  await run(async () => {
    const f = form.value
    if (dialog.value === 'department') selected.value = await directoryApi.saveDepartment(f.id, f.name, f.departmentId || null)
    else if (dialog.value === 'position') await directoryApi.savePosition(f.id, f.name, f.departmentId)
    else await directoryApi.member(f.id, f.userId, true)
    dialog.value = ''
    await refresh()
    ElMessage.success('已保存')
  })
}
async function remove(label: string, action: () => Promise<void>) {
  if (!await ElMessageBox.confirm(label, '确认操作', { type: 'warning' }).then(() => true, () => false)) return
  await run(async () => { await action(); await refresh() })
}
onMounted(() => run(refresh))
</script>
<template>
  <section class="page-shell" v-loading="busy">
    <div class="page-heading"><div><p class="page-kicker">系统管理</p><h2>组织架构</h2></div>
      <el-button @click="run(refresh)">刷新</el-button></div>
    <el-alert v-if="error" :title="error" type="error" />
    <div class="org-layout">
      <el-card shadow="never"><template #header>部门目录</template>
        <el-button type="primary" plain @click="editDepartment()">新增部门</el-button>
        <el-tree :data="tree" node-key="id" :current-node-key="selected" highlight-current default-expand-all
          :props="{ label: 'name', children: 'children' }" empty-text="尚未创建部门" @node-click="(node: TreeNode) => selected = node.id" />
      </el-card>
      <el-card shadow="never">
        <template #header><strong>{{ department?.name ?? '部门详情' }}</strong></template>
        <el-empty v-if="!department" description="创建部门后即可设置职位和分配人员" />
        <template v-else>
          <div class="org-actions">
            <el-button type="primary" @click="editPosition()">新增职位</el-button>
            <el-button @click="editDepartment(department)">编辑部门</el-button>
            <el-button type="danger" plain @click="remove('删除当前部门？', () => directoryApi.remove('departments', selected))">删除部门</el-button>
          </div>
          <el-table :data="positions" empty-text="本部门暂无职位">
            <el-table-column prop="name" label="职位" min-width="110" />
            <el-table-column label="任职人员" min-width="160"><template #default="{row}">
              <el-tag v-for="m in directory.members.filter(m => m.positionId === row.id)" :key="m.userId" closable
                @close="remove('移除该人员的任职？', () => directoryApi.member(row.id, m.userId, false))">{{ names.get(m.userId) }}</el-tag>
            </template></el-table-column>
            <el-table-column label="操作" width="170"><template #default="{row}">
              <el-button link type="primary" @click="assign(row)">添加人员</el-button>
              <el-button link @click="editPosition(row)">编辑</el-button>
              <el-button link type="danger" @click="remove('删除该职位？', () => directoryApi.remove('positions', row.id))">删除</el-button>
            </template></el-table-column>
          </el-table>
        </template>
      </el-card>
    </div>
    <el-dialog :model-value="!!dialog" @close="dialog = ''" :title="dialog === 'member' ? '添加任职人员' : dialog === 'position' ? '职位信息' : '部门信息'" width="min(480px, 95vw)">
      <el-form label-position="top" @submit.prevent="save">
        <el-form-item v-if="dialog === 'member'" :label="form.name"><el-select v-model="form.userId" filterable placeholder="选择人员">
          <el-option v-for="u in available" :key="u.id" :value="u.id" :label="u.name" />
        </el-select></el-form-item>
        <template v-else>
          <el-form-item label="名称"><el-input v-model="form.name" maxlength="100" /></el-form-item>
          <el-form-item :label="dialog === 'department' ? '上级部门（留空为顶级）' : '所属部门'">
            <el-tree-select v-model="form.departmentId" :data="tree" node-key="id" :props="{ label: 'name' }"
              check-strictly :clearable="dialog === 'department'" placeholder="选择部门" />
          </el-form-item>
        </template>
      </el-form>
      <template #footer><el-button @click="dialog = ''">取消</el-button>
        <el-button type="primary" :loading="busy" :disabled="dialog === 'member' ? !form.userId : !form.name.trim()" @click="save">保存</el-button></template>
    </el-dialog>
  </section>
</template>
<style scoped>
.org-layout { display: grid; grid-template-columns: minmax(190px, 1fr) minmax(0, 3fr); gap: 16px; }
.org-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.el-tree, .el-table { margin-top: 12px; }
.el-tag { margin: 2px; }
@media (max-width: 800px) { .org-layout { grid-template-columns: 1fr; } }
</style>
