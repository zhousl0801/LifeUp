<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { createCategory, fetchCategories, reorderCategories, updateCategory } from '../api/categories'
import { getApiErrorMessage } from '../api/client'
import AppIcon from '../components/AppIcon.vue'
import BottomSheet from '../components/BottomSheet.vue'
import LoadingButton from '../components/LoadingButton.vue'
import LoadingState from '../components/LoadingState.vue'
import PageHeader from '../components/PageHeader.vue'
import type { EntryCategory } from '../types/category'
import type { EntryKind } from '../types/entry'
import { navigateBack } from '../utils/navigation'

const router = useRouter()
const kind = ref<EntryKind>('expense')
const items = ref<EntryCategory[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const formError = ref('')
const editorOpen = ref(false)
const editing = ref<EntryCategory | null>(null)
const form = reactive({ name: '', isGroup: true, parentId: '', isActive: true })
const ordered = (categories: EntryCategory[]) => categories.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
const groups = computed(() => ordered(items.value.filter(item => item.kind === kind.value && item.isGroup)))
const activeGroups = computed(() => groups.value.filter(group => group.isActive))

function children(id: string): EntryCategory[] {
  return ordered(items.value.filter(item => item.parentId === id))
}

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    items.value = await fetchCategories()
  } catch (cause) {
    error.value = getApiErrorMessage(cause, '分类暂时无法读取。')
  } finally {
    loading.value = false
  }
}

function openEditor(item?: EntryCategory, parentId = ''): void {
  editing.value = item ?? null
  form.name = item?.name ?? ''
  form.isGroup = item ? Boolean(item.isGroup) : !parentId
  form.parentId = item?.parentId ?? parentId
  form.isActive = item?.isActive ?? true
  formError.value = ''
  editorOpen.value = true
}

async function save(): Promise<void> {
  if (saving.value || !form.name.trim() || (!form.isGroup && !form.parentId)) return
  saving.value = true
  formError.value = ''
  try {
    const item = editing.value
    const parentId = !form.isGroup && form.parentId !== item?.parentId ? form.parentId : undefined
    if (item) {
      await updateCategory(item.id, { name: form.name.trim(), isActive: form.isActive, parentId })
    } else {
      await createCategory({ kind: kind.value, name: form.name.trim(), isGroup: form.isGroup, parentId })
    }
    editorOpen.value = false
    await load()
  } catch (cause) {
    formError.value = getApiErrorMessage(cause, '保存分类失败，请重试。')
  } finally {
    saving.value = false
  }
}

async function move(item: EntryCategory, offset: -1 | 1): Promise<void> {
  if (saving.value) return
  const siblings = item.isGroup ? [...groups.value] : children(item.parentId ?? '')
  const index = siblings.findIndex(category => category.id === item.id)
  const target = siblings[index + offset]
  if (!target || index < 0) return

  siblings[index + offset] = item
  siblings[index] = target
  saving.value = true
  error.value = ''
  try {
    await reorderCategories(item.kind, siblings.map(category => category.id), {
      isGroup: Boolean(item.isGroup),
      parentId: item.parentId ?? undefined
    })
    await load()
  } catch (cause) {
    error.value = getApiErrorMessage(cause, '排序失败，请重试。')
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <main class="page category-page">
    <PageHeader title="分类管理" back @back="navigateBack(router, { name: 'ledger' })" />
    <div class="category-kind" role="group" aria-label="收支方向">
      <button v-for="option in (['expense', 'income'] as const)" :key="option" type="button"
        :aria-pressed="kind === option" :disabled="saving" @click="kind = option">
        {{ option === 'expense' ? '支出' : '收入' }}
      </button>
    </div>
    <LoadingState v-if="loading && !items.length" variant="list" label="正在读取分类" />
    <div v-if="error" class="category-error" role="alert">
      {{ error }} <button type="button" :disabled="loading" @click="load">刷新</button>
    </div>
    <section v-for="(group, index) in groups" :key="group.id" class="category-card">
      <header class="category-row">
        <button type="button" class="category-title" :disabled="saving" @click="openEditor(group)">
          {{ group.name }} <small v-if="!group.isActive">已停用</small>
        </button>
        <button type="button" :disabled="saving || index === 0" :aria-label="'上移' + group.name" @click="move(group, -1)">↑</button>
        <button type="button" :disabled="saving || index === groups.length - 1" :aria-label="'下移' + group.name" @click="move(group, 1)">↓</button>
      </header>
      <div v-for="(item, childIndex) in children(group.id)" :key="item.id" class="category-row child">
        <button type="button" class="category-title" :disabled="saving" @click="openEditor(item)">
          <AppIcon :name="item.name" /> {{ item.name }} <small v-if="!item.isActive">已停用</small>
        </button>
        <button type="button" :disabled="saving || childIndex === 0" :aria-label="'上移' + item.name" @click="move(item, -1)">↑</button>
        <button type="button" :disabled="saving || childIndex === children(group.id).length - 1" :aria-label="'下移' + item.name" @click="move(item, 1)">↓</button>
      </div>
      <button v-if="group.isActive" type="button" class="add-child" :disabled="saving" @click="openEditor(undefined, group.id)">＋ 新增二级分类</button>
    </section>
    <button type="button" class="primary-button add-group" :disabled="loading || saving" @click="openEditor()">新增一级分类</button>

    <BottomSheet :open="editorOpen" :close-disabled="saving" close-icon
      :title="(editing ? '编辑' : '新增') + (form.isGroup ? '一级分类' : '二级分类')" @close="editorOpen = false">
      <form class="category-form" @submit.prevent="save">
        <label>名称 <input v-model="form.name" type="text" maxlength="20" required /></label>
        <label v-if="!form.isGroup">所属一级分类
          <select v-model="form.parentId" required>
            <option v-for="group in activeGroups" :key="group.id" :value="group.id">{{ group.name }}</option>
            <option v-if="groups.some(group => group.id === form.parentId && !group.isActive)" :value="form.parentId">
              {{ groups.find(group => group.id === form.parentId)?.name }}（已停用）
            </option>
          </select>
        </label>
        <label v-if="editing" class="enabled"><input v-model="form.isActive" type="checkbox" :disabled="editing.systemKey === 'salary'" /> 启用分类</label>
        <p v-if="formError" class="category-error" role="alert">{{ formError }}</p>
        <LoadingButton type="submit" class="primary-button" :loading="saving" :disabled="!form.name.trim()">保存</LoadingButton>
      </form>
    </BottomSheet>
  </main>
</template>

<style scoped>
.category-kind { display: grid; grid-template-columns: 1fr 1fr; gap: 6px; margin-bottom: 16px; }
.category-kind button, .category-row button, .add-child {
  min-height: 44px; border: 0; border-radius: var(--radius-control);
  background: transparent; color: var(--color-text-primary);
}
.category-kind button { background: var(--color-bg-surface); }
.category-kind button[aria-pressed='true'] { background: var(--color-brand-soft); color: var(--color-brand); font-weight: 700; }
.category-card { margin-bottom: 12px; padding: 6px 10px; background: var(--color-bg-surface); border-radius: var(--radius-card); }
.category-row { display: flex; align-items: center; }
.category-row button:not(.category-title) { min-width: 44px; }
.category-row button:disabled { color: var(--color-text-disabled); }
.category-title { flex: 1; display: flex; gap: 6px; align-items: center; text-align: left; min-width: 0; overflow-wrap: anywhere; }
.category-title .app-icon { width: 18px; height: 18px; flex-shrink: 0; color: var(--color-brand); }
header .category-title { font-weight: 700; }
.child { margin-left: 10px; border-top: 1px solid var(--color-border-subtle); }
small { color: var(--color-text-secondary); flex-shrink: 0; }
.add-child { color: var(--color-brand); margin-left: 10px; }
.add-group { width: 100%; }
.category-error { color: var(--color-danger); font-size: var(--text-footnote); }
.category-form { display: grid; gap: 12px; }
.category-form label { display: grid; gap: 4px; }
.category-form .enabled { display: flex; min-height: 44px; align-items: center; }
.category-form input:not([type='checkbox']), .category-form select {
  width: 100%; min-height: 44px; padding: 0 10px; border: 0; border-radius: var(--radius-control);
  background: var(--color-bg-subtle); color: var(--color-text-primary);
}
</style>
