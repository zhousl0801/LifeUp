<script setup lang="ts">
import { categoryIcon } from '../constants/categoryIcons'

withDefaults(defineProps<{ name: string; group?: boolean; size?: number }>(), { size: 24 })
</script>

<template>
  <img class="category-icon" :src="categoryIcon(name, group)" :width="size" :height="size" alt="" />
</template>

<style scoped>
.category-icon { display: block; flex: none; object-fit: contain; }
</style>
