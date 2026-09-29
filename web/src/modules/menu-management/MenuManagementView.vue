<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { menuApi } from '../../api/menuManagement'
import { canonicalMenuCode } from '../../api/menuCodeCompatibility'
import { menuManifest } from '../../router/pageRegistry'
import { useNavigationStore } from '../../stores/navigation'
import { useOperation } from '../exam-study/shared'
import type { MenuView, MenuTree, MenuDifference, MenuComparison } from '../../types/navigation'
const { busy, error, run } = useOperation()
const navigation = useNavigationStore()
const tab = ref('tree')
const tree = ref<MenuTree>({ version: 0, menus: [] })
const legacyDatabase = computed(() => tree.value.menus.some(m => canonicalMenuCode(m.menuCode) !== m.menuCode))
const comparison = ref<MenuComparison>({ version: 0, items: [] })
const filter = ref('')
const selected = ref<MenuDifference[]>([])
const applyFields = ref<string[]>([])
const details = ref<MenuDifference>()
const detailOpen = ref(false)
const editing = ref<MenuView>()
const editOpen = ref(false)
const editVersion = ref(0)
const form = ref({ menuCode: '', title: '', parentCode: null as string | null, iconKey: '', order: 0, enabled: true })
const statuses: Record<string, string> = { new: '未同步', synced: '已同步', changed: '前端声明变更', missing: '当前前端缺失', manual: '后台自建目录' }
const fields = [
  { key: 'title', name: '名称' }, { key: 'parentCode', name: '父目录' }, { key: 'iconKey', name: '图标' }, { key: 'order', name: '排序' },
  { key: 'type', name: '菜单类型' }, { key: 'routeKey', name: '页面路由' }, { key: 'requiredPermissions', name: '所需业务权限' },
  { key: 'isClosable', name: '页签可关闭' }, { key: 'enabled', name: '后台启用状态' },
]
type Node = MenuView & { children: Node[] }
const nodes = computed(() => {
  const map = new Map(tree.value.menus.map(m => [m.menuCode, { ...m, children: [] } as Node]))
  const roots: Node[] = []
  for (const m of map.values()) {
    const parent = m.parentCode ? map.get(m.parentCode) : undefined
    if (parent) parent.children.push(m); else roots.push(m)
  }
  const sort = (values: Node[]) => { values.sort((a, b) => a.order - b.order || a.title.localeCompare(b.title)); values.forEach(x => sort(x.children)) }
  sort(roots)
  return roots
})
const visibleDiffs = computed(() => comparison.value.items.filter(x => !filter.value || x.status === filter.value))
function ancestors(code: string): string[] {
  const result: string[] = []
  let parent = tree.value.menus.find(m => m.menuCode === code)?.parentCode
  while (parent) { result.push(parent); parent = tree.value.menus.find(m => m.menuCode === parent)?.parentCode }
  return result
}
const protectedCodes = computed(() => new Set(['home', 'system.menus', ...ancestors('home'), ...ancestors('system.menus')]))
const parents = computed(() => tree.value.menus.filter(m => m.type === 'directory' && m.menuCode !== editing.value?.menuCode && !ancestors(m.menuCode).includes(editing.value?.menuCode ?? '')))
async function load() {
  tree.value = await menuApi.list()
  comparison.value = legacyDatabase.value ? { version: tree.value.version, items: [] } : await menuApi.compare(menuManifest)
  selected.value = []
  if (legacyDatabase.value) { tab.value = 'tree'; editOpen.value = false }
}
async function updated() { await load(); await navigation.load(true) }
function edit(menu?: MenuView) {
  editing.value = menu; editVersion.value = tree.value.version
  form.value = menu ? { menuCode: menu.menuCode, title: menu.title, parentCode: menu.parentCode, iconKey: menu.iconKey ?? '', order: menu.order, enabled: menu.enabled }
    : { menuCode: '', title: '', parentCode: null, iconKey: '', order: 0, enabled: true }
  editOpen.value = true
}
async function save() {
  await run(async () => {
    const body = { ...form.value, parentCode: form.value.parentCode || null, iconKey: form.value.iconKey || null, version: editVersion.value }
    if (editing.value) await menuApi.edit(editing.value.menuCode, body)
    else await menuApi.createDirectory(body)
    editOpen.value = false; await updated(); ElMessage.success('菜单配置已保存')
  })
}
async function toggle(menu: MenuView, enabled: boolean) {
  await run(async () => { await menuApi.edit(menu.menuCode, { ...menu, enabled, version: tree.value.version }); await updated() })
}
async function remove(menu: MenuView) {
  try { await ElMessageBox.confirm(`删除空目录“${menu.title}”？`, '删除目录', { type: 'warning' }) } catch { return }
  await run(async () => { await menuApi.removeDirectory(menu.menuCode, tree.value.version); await updated() })
}
async function synchronize() {
  await run(async () => {
    await menuApi.sync(comparison.value.version, menuManifest, selected.value.map(x => ({ menuCode: x.menuCode, applyFields: [...applyFields.value] })))
    await updated(); ElMessage.success('菜单已同步；普通用户仍需单独授权')
  })
}
function inspect(row: MenuDifference) { details.value = row; detailOpen.value = true }
function display(value: unknown) { return value === null || value === undefined ? '—' : Array.isArray(value) ? value.join('、') || '无' : String(value) }
function fieldValue(source: object | null | undefined, key: string) { return display(source ? (source as Record<string, unknown>)[key] : undefined) }
const selectable = (row: MenuDifference) => row.local !== null
onMounted(() => { void run(load) })
</script>
<template>
  <main class="study-page">
    <header class="study-heading"><div><h2>菜单管理</h2><p>维护多级菜单，对比并同步当前 Web 提供的页面。页面编码不随名称或目录变化。</p></div><el-button :loading="busy" @click="run(load)">刷新 / 重新比较</el-button></header>
    <el-alert v-if="error" :title="error" type="error" :closable="false" class="study-error" />
    <el-alert v-if="legacyDatabase" title="菜单编码升级尚未完成，现有菜单仍可访问。请完成数据库迁移并重启新版服务后，再配置或同步菜单。" type="warning" :closable="false" class="study-error" />
    <el-tabs v-model="tab">
      <el-tab-pane label="菜单配置" name="tree">
        <div class="study-card" v-loading="busy">
          <div class="study-toolbar"><el-button type="primary" :disabled="legacyDatabase" @click="edit()">新增目录</el-button><span class="study-muted">模块页面从“前端菜单对比”同步；拖动源码文件不会改变菜单编码。</span></div>
          <el-table :data="nodes" row-key="menuCode" default-expand-all>
            <el-table-column prop="title" label="菜单名称" min-width="220" />
            <el-table-column prop="menuCode" label="唯一编码" min-width="200" />
            <el-table-column label="类型" width="90"><template #default="{ row }"><el-tag :type="row.type === 'directory' ? 'info' : 'primary'">{{ row.type === 'directory' ? '目录' : '模块' }}</el-tag></template></el-table-column>
            <el-table-column prop="order" label="排序" width="70" />
            <el-table-column label="启用" width="80"><template #default="{ row }"><el-switch :model-value="row.enabled" :disabled="busy || legacyDatabase || protectedCodes.has(row.menuCode)" @change="(v: string | number | boolean) => toggle(row, Boolean(v))" /></template></el-table-column>
            <el-table-column label="操作" width="140"><template #default="{ row }"><el-button link type="primary" :disabled="legacyDatabase" @click="edit(row)">编辑</el-button><el-button v-if="row.type === 'directory' && !row.children.length && !protectedCodes.has(row.menuCode)" link type="danger" :disabled="legacyDatabase" @click="remove(row)">删除</el-button></template></el-table-column>
          </el-table>
        </div>
      </el-tab-pane>
      <el-tab-pane label="前端菜单对比" name="compare" :disabled="legacyDatabase">
        <el-alert title="仅同步菜单信息，不上传页面代码。未同步的页面不会出现在业务导航；后台调整不会被默认覆盖。" type="info" :closable="false" class="study-error" />
        <div class="study-card" v-loading="busy">
          <div class="study-toolbar"><el-select v-model="filter" clearable placeholder="全部同步状态" style="width: 230px"><el-option v-for="(label, key) in statuses" :key="key" :value="key" :label="`${label}（${comparison.items.filter(x => x.status === key).length}）`" /></el-select><el-button type="primary" :disabled="!selected.length" :loading="busy" @click="synchronize">同步所选 {{ selected.length || '' }}</el-button></div>
          <div class="sync-options"><span>将前端默认值应用到已入库菜单：</span><el-checkbox-group v-model="applyFields"><el-checkbox v-for="field in fields.slice(0, 4)" :key="field.key" :value="field.key">{{ field.name }}</el-checkbox></el-checkbox-group><small class="study-muted">默认不勾选：仅更新声明快照，保留后台名称、层级、图标、排序及启用状态。缺失的父目录会自动创建。</small></div>
          <el-table :data="visibleDiffs" row-key="menuCode" @selection-change="(rows: MenuDifference[]) => selected = rows">
            <el-table-column type="selection" width="45" :selectable="selectable" />
            <el-table-column prop="menuCode" label="唯一编码" min-width="200" />
            <el-table-column label="前端默认名称" min-width="150"><template #default="{ row }">{{ row.local?.title ?? '—' }}</template></el-table-column>
            <el-table-column label="后台当前名称" min-width="150"><template #default="{ row }">{{ row.current?.title ?? '—' }}</template></el-table-column>
            <el-table-column label="状态" width="165"><template #default="{ row }"><el-tag :type="row.status === 'synced' ? 'success' : row.status === 'new' ? 'warning' : 'info'">{{ statuses[row.status] }}</el-tag></template></el-table-column>
            <el-table-column label="详情" width="90"><template #default="{ row }"><el-button link type="primary" @click="inspect(row)">比较</el-button></template></el-table-column>
          </el-table>
        </div>
      </el-tab-pane>
    </el-tabs>
    <el-dialog v-model="editOpen" :title="editing ? '编辑菜单配置' : '新增目录'" width="min(600px,94vw)" append-to-body>
      <el-form label-position="top"><el-form-item label="唯一编码"><el-input v-model="form.menuCode" :disabled="!!editing" maxlength="120" placeholder="使用小写业务编码，以点分段，每段以字母开头" /></el-form-item><el-form-item label="名称"><el-input v-model="form.title" maxlength="120" /></el-form-item><el-form-item label="父目录"><el-select v-model="form.parentCode" clearable placeholder="顶层菜单" style="width: 100%" :disabled="editing?.menuCode === 'home'"><el-option v-for="parent in parents" :key="parent.menuCode" :value="parent.menuCode" :label="`${parent.title}（${parent.menuCode}）`" /></el-select></el-form-item><el-form-item label="图标编码"><el-input v-model="form.iconKey" maxlength="80" placeholder="例如 book、settings、users、menu" /></el-form-item><el-form-item label="排序"><el-input-number v-model="form.order" :min="-100000" :max="100000" /></el-form-item><el-form-item v-if="editing" label="启用"><el-switch v-model="form.enabled" :disabled="protectedCodes.has(editing.menuCode)" /></el-form-item></el-form>
      <template #footer><el-button @click="editOpen = false">取消</el-button><el-button type="primary" :loading="busy" @click="save">保存</el-button></template>
    </el-dialog>
    <el-dialog v-model="detailOpen" :title="`菜单比较 · ${details?.menuCode ?? ''}`" width="min(960px,96vw)" append-to-body>
      <el-table :data="fields"><el-table-column prop="name" label="字段" width="125" /><el-table-column label="前端默认值" min-width="180"><template #default="{ row }">{{ fieldValue(details?.local, row.key) }}</template></el-table-column><el-table-column label="上次同步值" min-width="180"><template #default="{ row }">{{ fieldValue(details?.current?.lastDeclaration, row.key) }}</template></el-table-column><el-table-column label="后台当前值" min-width="180"><template #default="{ row }">{{ fieldValue(details?.current, row.key) }}</template></el-table-column></el-table>
      <p class="study-muted">后台自建目录不会被同步删除；当前前端缺失的模块可保留或在菜单配置中停用。</p>
    </el-dialog>
  </main>
</template>
<style scoped>
.sync-options { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 20px; padding: 12px 16px; margin-bottom: 16px; background: var(--el-fill-color-light); border-radius: 8px; font-size: 13px; }
.sync-options small { flex-basis: 100%; }
</style>
