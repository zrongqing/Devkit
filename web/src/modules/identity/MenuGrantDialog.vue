<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { menuApi } from '../../api/menuManagement'
import { hasMenuGrant } from '../../api/session'
import { useOperation } from '../exam-study/shared'
import type { MenuView } from '../../types/navigation'
const props = defineProps<{ target: { id: string; name: string; role: boolean; menuCodes: string[]; inheritedMenuCodes: string[]; permissions: string[] }; permissionNames: Record<string, string> }>()
const emit = defineEmits<{ close: []; saved: [] }>()
const { busy, error, run } = useOperation()
const menus = ref<MenuView[]>([])
const selection = ref<string[]>([...props.target.menuCodes])
const treeRef = ref<{ getCheckedKeys: () => (string | number)[] }>()
type Node = MenuView & { children: Node[]; disabled: boolean }
const nodes = computed(() => {
  const map = new Map(menus.value.map(m => [m.menuCode, { ...m, children: [], disabled: m.type === 'module' && (m.isPublic || !hasMenuGrant(m.menuCode)) } as Node]))
  const roots: Node[] = []
  for (const m of map.values()) {
    const parent = m.parentCode ? map.get(m.parentCode) : undefined
    if (parent) parent.children.push(m); else roots.push(m)
  }
  return roots
})
const effective = computed(() => new Set([...selection.value, ...props.target.inheritedMenuCodes]))
const missing = computed(() => menus.value.filter(m => m.type === 'module' && effective.value.has(m.menuCode))
  .map(m => ({ menu: m.title, permissions: m.requiredPermissions.filter(p => !props.target.permissions.includes(p)) }))
  .filter(x => x.permissions.length))
function changed() {
  const checked = new Set(treeRef.value?.getCheckedKeys().map(String) ?? [])
  selection.value = menus.value.filter(x => x.type === 'module' && checked.has(x.menuCode)).map(x => x.menuCode)
}
async function save() {
  await run(async () => {
    if (props.target.role) await menuApi.roleMenus(props.target.id, selection.value)
    else await menuApi.userMenus(props.target.id, selection.value)
    emit('saved')
  })
}
onMounted(() => { void run(async () => { menus.value = (await menuApi.list()).menus }) })
</script>
<template>
  <el-dialog :model-value="true" :title="`菜单授权 · ${target.name}`" width="min(760px,94vw)" append-to-body @close="emit('close')">
    <el-alert v-if="error" :title="error" type="error" :closable="false" class="study-error" />
    <p class="study-muted">{{ target.role ? '勾选目录会批量选择模块菜单。' : '此处仅编辑用户直接菜单授权；取消勾选不会移除角色继承权限。' }} 菜单授权不会自动改变业务权限，公共入口无需授权。</p>
    <el-tree ref="treeRef" v-loading="busy" :data="nodes" node-key="menuCode" :props="{ label: 'title', children: 'children', disabled: 'disabled' }" show-checkbox default-expand-all :default-checked-keys="target.menuCodes" @check="changed">
      <template #default="{ data }"><span>{{ data.title }}</span><el-tag v-if="data.isPublic" class="grant-tag" size="small" type="info">公共</el-tag><el-tag v-else-if="target.inheritedMenuCodes.includes(data.menuCode)" class="grant-tag" size="small" type="info">角色继承</el-tag><el-tag v-if="!data.enabled" class="grant-tag" size="small" type="warning">已停用</el-tag></template>
    </el-tree>
    <div v-if="missing.length" class="grant-missing"><strong>以下页面尚缺少业务权限</strong><p v-for="item in missing" :key="item.menu">{{ item.menu }}：{{ item.permissions.map(p => permissionNames[p] ?? p).join('、') }}</p><small>可以保存菜单授权；用户进入这些页面时会看到受限提示，请另行配置业务权限。</small></div>
    <template #footer><el-button @click="emit('close')">取消</el-button><el-button type="primary" :loading="busy" @click="save">保存菜单授权</el-button></template>
  </el-dialog>
</template>
<style scoped>
.grant-tag { margin-left: 8px; }
.grant-missing { padding: 16px; margin-top: 20px; border-radius: 8px; color: #8a5a13; background: #fff6e4; font-size: 13px; }
</style>
