<script setup lang="ts">
import NavigationIcon from './NavigationIcon.vue'
import type { NavigationMenuNode } from '../../types/navigation'

defineOptions({ name: 'RecursiveMenuItem' })

defineProps<{
  item: NavigationMenuNode
}>()
</script>

<template>
  <el-sub-menu v-if="item.children.length" :index="item.id">
    <template #title>
      <NavigationIcon :icon-key="item.iconKey" directory />
      <span>{{ item.title }}</span>
    </template>
    <RecursiveMenuItem v-for="child in item.children" :key="child.id" :item="child" />
  </el-sub-menu>
  <el-menu-item v-else :index="item.id">
    <NavigationIcon :icon-key="item.iconKey" />
    <template #title>{{ item.title }}</template>
  </el-menu-item>
</template>
