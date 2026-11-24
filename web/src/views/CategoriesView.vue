<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { createCategory, fetchCategories, reorderCategories, updateCategory } from '../api/categories'
import { getApiErrorMessage } from '../api/client'
import AppIcon from '../components/AppIcon.vue'
import BottomSheet from '../components/BottomSheet.vue'
import CategoryIcon from '../components/CategoryIcon.vue'
import LoadingButton from '../components/LoadingButton.vue'
import LoadingState from '../components/LoadingState.vue'
import PageHeader from '../components/PageHeader.vue'
import { categoryIconOptions, resolveCategoryIconKey } from '../constants/categoryIcons'
import type { EntryCategory } from '../types/category'
import type { EntryKind } from '../types/entry'
import { navigateBack } from '../utils/navigation'

interface DropPosition {
  parentId?: string
  index: number
  markerId: string
  after: boolean
}

interface DragGesture {
  item: EntryCategory
  handle: HTMLElement
  pointerId: number
  x: number
  y: number
  timer: ReturnType<typeof setTimeout>
}

const router = useRouter()
const kind = ref<EntryKind>('expense')
const items = ref<EntryCategory[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const status = ref('')
const formError = ref('')
const sheet = ref<'edit' | 'icons' | 'move' | null>(null)
const editing = ref<EntryCategory | null>(null)
const moveDestination = ref('')
const categoryList = ref<HTMLElement | null>(null)
const folded = reactive(new Set<string>())
const form = reactive({ name: '', isGroup: true, parentId: '', isActive: true, iconKey: '' })
const ordered = (categories: EntryCategory[]) => [...categories].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
const groups = computed(() => ordered(items.value.filter(item => item.kind === kind.value && item.isGroup)))
const activeGroups = computed(() => groups.value.filter(group => group.isActive))
const moveGroups = computed(() => activeGroups.value.filter(group => group.id !== editing.value?.parentId))
const childrenByGroup = computed(() => {
  const grouped = new Map<string, EntryCategory[]>()
  for (const item of items.value) {
    if (!item.parentId) continue
    const list = grouped.get(item.parentId) ?? []
    list.push(item)
    grouped.set(item.parentId, list)
  }
  for (const [groupId, list] of grouped) {
    grouped.set(groupId, ordered(list))
  }
  return grouped
})
const dragItem = ref<EntryCategory | null>(null)
const dragPoint = ref({ x: 0, y: 0 })
const drop = ref<DropPosition | null>(null)
let gesture: DragGesture | null = null
let scrollFrame = 0
let suppressClick = false
let clickTimer: ReturnType<typeof setTimeout> | undefined

function children(id: string): EntryCategory[] {
  return childrenByGroup.value.get(id) ?? []
}

function siblings(item: EntryCategory): EntryCategory[] {
  return item.isGroup ? groups.value : children(item.parentId ?? '')
}

const editingIndex = computed(() => editing.value ? siblings(editing.value).findIndex(item => item.id === editing.value?.id) : -1)
const currentIcon = computed(() => editing.value
  ? resolveCategoryIconKey(editing.value.name, editing.value.isGroup, editing.value.iconKey)
  : form.iconKey)

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

function switchKind(value: EntryKind): void {
  cancelDrag()
  kind.value = value
  status.value = ''
}

function openEditor(item?: EntryCategory, parentId = ''): void {
  editing.value = item ?? null
  form.name = item?.name ?? ''
  form.isGroup = item ? Boolean(item.isGroup) : !parentId
  form.parentId = item?.parentId ?? parentId
  form.isActive = item?.isActive ?? true
  form.iconKey = item ? resolveCategoryIconKey(item.name, item.isGroup, item.iconKey) : ''
  formError.value = ''
  sheet.value = 'edit'
}

function openIcons(item: EntryCategory): void {
  editing.value = item
  formError.value = ''
  sheet.value = 'icons'
}

function closeSheet(): void {
  sheet.value = null
  formError.value = ''
}

function replaceItem(item: EntryCategory): void {
  items.value = [...items.value.filter(category => category.id !== item.id), item]
}

async function save(): Promise<void> {
  const name = form.name.trim()
  if (saving.value || !name || (!form.isGroup && !form.parentId)) return
  saving.value = true
  formError.value = ''
  try {
    const item = editing.value
    const parentId = !form.isGroup && form.parentId !== item?.parentId ? form.parentId : undefined
    const iconKey = form.iconKey || undefined
    const saved = item
      ? await updateCategory(item.id, { name, isActive: form.isActive, parentId, iconKey })
      : await createCategory({ kind: kind.value, name, isGroup: form.isGroup, parentId, iconKey })
    replaceItem(saved)
    if (saved.parentId) folded.delete(saved.parentId)
    status.value = item ? '已保存「' + saved.name + '」' : '已新增「' + saved.name + '」'
    closeSheet()
  } catch (cause) {
    formError.value = getApiErrorMessage(cause, '保存分类失败，请重试。')
  } finally {
    saving.value = false
  }
}

async function saveIcon(iconKey: string): Promise<void> {
  const item = editing.value
  if (saving.value || !item) return
  saving.value = true
  formError.value = ''
  try {
    replaceItem(await updateCategory(item.id, { name: item.name, isActive: item.isActive, iconKey }))
    status.value = '已更换「' + item.name + '」的图标'
    closeSheet()
  } catch (cause) {
    formError.value = getApiErrorMessage(cause, '保存图标失败，请重试。')
  } finally {
    saving.value = false
  }
}

function openMove(item: EntryCategory): void {
  if (suppressClick || saving.value) return
  editing.value = item
  moveDestination.value = moveGroups.value[0]?.id ?? ''
  formError.value = ''
  sheet.value = 'move'
}

async function move(item: EntryCategory, parentId: string | undefined, index: number): Promise<void> {
  if (saving.value) return
  const crossGroup = !item.isGroup && parentId !== item.parentId
  const target = parentId ? [...children(parentId)] : [...groups.value]
  const targetGroup = groups.value.find(group => group.id === parentId)
  if (crossGroup && (!targetGroup?.isActive || !item.parentId)) return

  const oldIndex = target.findIndex(category => category.id === item.id)
  if (oldIndex !== -1) {
    if (index > oldIndex) index--
    if (index === oldIndex) return
    target.splice(oldIndex, 1)
  }
  target.splice(index, 0, item)

  saving.value = true
  error.value = ''
  formError.value = ''
  status.value = ''
  try {
    const ids = target.map(category => category.id)
    await reorderCategories(item.kind, ids, {
      isGroup: Boolean(item.isGroup),
      parentId,
      ...(crossGroup ? { movingId: item.id, sourceParentId: item.parentId ?? undefined } : {})
    })
    const positions = new Map(ids.map((id, position) => [id, position]))
    items.value = items.value.map(category => {
      const sortOrder = positions.get(category.id)
      if (sortOrder === undefined) return category
      return { ...category, sortOrder, parentId: crossGroup && category.id === item.id ? parentId : category.parentId }
    })
    if (crossGroup && parentId) folded.delete(parentId)
    status.value = crossGroup ? '已将「' + item.name + '」移至「' + targetGroup?.name + '」' : '已调整「' + item.name + '」的顺序'
    closeSheet()
  } catch (cause) {
    const message = getApiErrorMessage(cause, '移动失败，请刷新后重试。')
    if (sheet.value === 'move') formError.value = message
    else error.value = message
  } finally {
    saving.value = false
  }
}

function moveFromMenu(offset: -1 | 1): void {
  const item = editing.value
  if (!item) return
  void move(item, item.parentId ?? undefined, editingIndex.value + (offset === 1 ? 2 : -1))
}

function toggleGroup(group: EntryCategory): void {
  if (folded.has(group.id)) folded.delete(group.id)
  else folded.add(group.id)
}

function startDrag(event: PointerEvent, item: EntryCategory): void {
  if (event.button !== 0 || gesture || loading.value || saving.value) return
  const handle = event.currentTarget as HTMLElement
  handle.setPointerCapture?.(event.pointerId)
  gesture = {
    item, handle, pointerId: event.pointerId, x: event.clientX, y: event.clientY,
    timer: setTimeout(() => {
      dragItem.value = item
      dragPoint.value = { x: event.clientX, y: event.clientY }
      aimDrop()
      scrollFrame = requestAnimationFrame(scrollDuringDrag)
    }, 340)
  }
}

function aimDrop(): void {
  drop.value = null
  const item = dragItem.value
  if (!item) return
  const { x, y } = dragPoint.value
  const hit = document.elementFromPoint?.(x, y)
  const card = hit?.closest<HTMLElement>('[data-category-group]')
  if (!card || !categoryList.value?.contains(card)) return
  const group = groups.value.find(category => category.id === card.dataset.categoryGroup)
  if (!group) return

  if (item.isGroup) {
    const bounds = card.getBoundingClientRect()
    const after = y >= bounds.top + bounds.height / 2
    drop.value = { index: groups.value.indexOf(group) + Number(after), markerId: group.id, after }
    return
  }

  if (!group.isActive && group.id !== item.parentId) return
  const childItems = children(group.id)
  const row = hit?.closest<HTMLElement>('.category-child')
  if (row) {
    const child = childItems.findIndex(category => category.id === row.dataset.categoryId)
    if (child < 0) return
    const bounds = row.getBoundingClientRect()
    const after = y >= bounds.top + bounds.height / 2
    drop.value = { parentId: group.id, index: child + Number(after), markerId: childItems[child]!.id, after }
    return
  }

  const atStart = !folded.has(group.id) && Boolean(hit?.closest('.category-group'))
  const markerId = atStart || folded.has(group.id) ? group.id : childItems.at(-1)?.id ?? group.id
  drop.value = { parentId: group.id, index: atStart ? 0 : childItems.length, markerId, after: true }
}

function scrollDuringDrag(): void {
  if (!dragItem.value) return
  const { x, y } = dragPoint.value
  if (x >= 0 && x <= window.innerWidth && y >= 0 && y <= window.innerHeight) {
    const edge = 72
    const distance = y < edge ? y - edge : Math.max(0, y - (window.innerHeight - edge))
    if (distance) {
      window.scrollBy(0, distance / 5)
      aimDrop()
    }
  }
  scrollFrame = requestAnimationFrame(scrollDuringDrag)
}

function trackDrag(event: PointerEvent): void {
  if (gesture?.pointerId !== event.pointerId) return
  if (dragItem.value) {
    dragPoint.value = { x: event.clientX, y: event.clientY }
    aimDrop()
  } else if (Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) > 10) {
    clearTimeout(gesture.timer)
    suppressClick = true
  }
}

function cancelDrag(): void {
  const current = gesture
  gesture = null
  clearTimeout(current?.timer)
  cancelAnimationFrame(scrollFrame)
  if (current?.handle.hasPointerCapture?.(current.pointerId)) {
    current.handle.releasePointerCapture(current.pointerId)
  }
  dragItem.value = null
  drop.value = null
  if (suppressClick) {
    clearTimeout(clickTimer)
    clickTimer = setTimeout(() => { suppressClick = false }, 0)
  }
}

function finishDrag(event: PointerEvent): void {
  if (gesture?.pointerId !== event.pointerId) return
  dragPoint.value = { x: event.clientX, y: event.clientY }
  aimDrop()
  const item = dragItem.value
  const position = drop.value
  if (item) suppressClick = true
  cancelDrag()
  if (item && position) void move(item, position.parentId, position.index)
}

function abortDrag(event: PointerEvent): void {
  if (gesture?.pointerId !== event.pointerId) return
  if (dragItem.value) suppressClick = true
  cancelDrag()
}

function insertClass(id: string, isGroup: boolean): string {
  if (drop.value?.markerId !== id || Boolean(dragItem.value?.isGroup) !== isGroup) return ''
  return drop.value.after ? 'insert-after' : 'insert-before'
}

onMounted(load)
onBeforeUnmount(() => {
  cancelDrag()
  clearTimeout(clickTimer)
})
</script>

<template>
  <main class="page category-page">
    <PageHeader title="分类管理" back @back="navigateBack(router, { name: 'ledger' })">
      <template #actions>
        <button type="button" class="add-group" :disabled="loading || saving" @click="openEditor()">
          <AppIcon name="plus" />一级分类
        </button>
      </template>
    </PageHeader>
    <div class="category-kind" role="group" aria-label="收支方向">
      <button v-for="option in (['expense', 'income'] as const)" :key="option" type="button"
        :aria-pressed="kind === option" :disabled="saving" @click="switchKind(option)">
        {{ option === 'expense' ? '支出' : '收入' }}
      </button>
    </div>
    <p class="category-hint">点图标更换 · 点名称编辑 · 长按手柄拖动</p>
    <LoadingState v-if="loading && !items.length" variant="list" label="正在读取分类" />
    <div v-if="error" class="category-error" role="alert">
      {{ error }} <button type="button" :disabled="loading || saving" @click="load">刷新</button>
    </div>
    <p v-if="!loading && !error && !groups.length" class="category-hint">暂无一级分类</p>
    <div ref="categoryList" class="category-list" :aria-busy="saving">
      <section v-for="group in groups" :key="group.id" class="category-card"
        :aria-label="group.name" :data-category-group="group.id"
        :class="[insertClass(group.id, true), { 'drop-card': drop?.parentId === group.id, 'drag-source': dragItem?.id === group.id }]">
        <template v-for="item in (folded.has(group.id) ? [group] : [group, ...children(group.id)])" :key="item.id">
          <div class="category-row" :data-category-id="item.id"
            :class="[item.isGroup ? 'category-group' : 'category-child', insertClass(item.id, false), { 'drag-source': !item.isGroup && dragItem?.id === item.id }]">
            <button type="button" class="icon-control" :aria-label="'更换 ' + item.name + ' 的图标'" :disabled="saving" @click="openIcons(item)">
              <CategoryIcon :name="item.name" :group="item.isGroup" :icon-key="item.iconKey" :size="item.isGroup ? 26 : 22" />
            </button>
            <button type="button" class="category-name" :disabled="saving" @click="openEditor(item)">
              {{ item.name }} <small v-if="!item.isActive">已停用</small>
            </button>
            <button v-if="item.isGroup" type="button" class="icon-control fold-control" :aria-expanded="!folded.has(group.id)"
              :aria-label="(folded.has(group.id) ? '展开 ' : '折叠 ') + group.name" @click="toggleGroup(group)">
              <AppIcon name="chevron" />
            </button>
            <button type="button" class="icon-control drag-handle" :disabled="loading || saving" :aria-label="'移动 ' + item.name"
              @click="openMove(item)" @contextmenu.prevent @pointerdown="startDrag($event, item)" @pointermove="trackDrag"
              @pointerup="finishDrag" @pointercancel="abortDrag" @lostpointercapture="abortDrag">
              <span class="grip" aria-hidden="true"></span>
            </button>
          </div>
        </template>
        <button v-if="group.isActive && !folded.has(group.id)" type="button" class="add-child"
          :aria-label="'在' + group.name + '新增二级分类'" :disabled="saving" @click="openEditor(undefined, group.id)">
          <AppIcon name="plus" />二级分类
        </button>
      </section>
    </div>
    <p class="category-status" role="status">{{ status }}</p>
    <Teleport to="body">
      <div v-if="dragItem" class="category-ghost" aria-hidden="true"
        :style="{ left: Math.max(8, dragPoint.x - 140) + 'px', top: Math.max(8, dragPoint.y - 48) + 'px' }">
        <CategoryIcon :name="dragItem.name" :icon-key="dragItem.iconKey" :group="dragItem.isGroup" :size="20" />
        <span>{{ dragItem.name }}</span>
      </div>
    </Teleport>

    <BottomSheet :open="sheet === 'edit'" :close-disabled="saving" close-icon
      :title="(editing ? '编辑' : '新增') + (form.isGroup ? '一级分类' : '二级分类')" @close="closeSheet">
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
        <label v-if="editing" class="enabled">
          <input v-model="form.isActive" type="checkbox" :disabled="editing.systemKey === 'salary'" />启用分类
        </label>
        <p v-if="formError" class="category-error" role="alert">{{ formError }}</p>
        <LoadingButton type="submit" class="primary-button" :loading="saving" :disabled="!form.name.trim()">保存</LoadingButton>
      </form>
    </BottomSheet>

    <BottomSheet :open="sheet === 'icons'" :close-disabled="saving" close-icon
      :title="'更换「' + (editing?.name ?? '') + '」的图标'" @close="closeSheet">
      <div class="category-icon-grid" role="group" aria-label="分类图标">
        <button v-for="[key, label] in categoryIconOptions" :key="key" type="button"
          :aria-label="label" :aria-pressed="key === currentIcon" :disabled="saving" @click="saveIcon(key)">
          <CategoryIcon :name="label" :icon-key="key" :size="26" />
        </button>
      </div>
      <p class="category-status" role="status">{{ saving ? '正在保存图标…' : '' }}</p>
      <p v-if="formError" class="category-error" role="alert">{{ formError }}</p>
    </BottomSheet>

    <BottomSheet :open="sheet === 'move'" :close-disabled="saving" close-icon
      :title="'移动「' + (editing?.name ?? '') + '」'" @close="closeSheet">
      <div v-if="editing" class="category-form">
        <div class="move-actions">
          <button type="button" :disabled="saving || editingIndex <= 0" @click="moveFromMenu(-1)">上移</button>
          <button type="button" :disabled="saving || editingIndex >= siblings(editing).length - 1" @click="moveFromMenu(1)">下移</button>
        </div>
        <template v-if="!editing.isGroup && moveGroups.length">
          <label>移至一级分类
            <select v-model="moveDestination" :disabled="saving">
              <option v-for="group in moveGroups" :key="group.id" :value="group.id">{{ group.name }}</option>
            </select>
          </label>
          <LoadingButton class="primary-button" :loading="saving" :disabled="!moveDestination"
            @click="move(editing, moveDestination, children(moveDestination).length)">移动到末尾</LoadingButton>
        </template>
        <p v-if="formError" class="category-error" role="alert">{{ formError }}</p>
      </div>
    </BottomSheet>
  </main>
</template>

<style scoped>
.category-page { max-width: 520px; margin-inline: auto; }
.category-page button:disabled { opacity: .5; }
.add-group, .add-child {
  display: inline-flex; align-items: center; justify-content: center; gap: 4px;
  min-height: 44px; border: 0; border-radius: var(--radius-control); white-space: nowrap;
  font-size: var(--text-footnote);
}
.category-page .add-group { padding: 0 8px; color: var(--color-brand); background: var(--color-brand-soft); font-weight: 650; }
.add-group .app-icon, .add-child .app-icon { width: 14px; height: 14px; flex: none; }
.category-kind {
  display: grid; grid-template-columns: 1fr 1fr; gap: 4px; padding: 4px;
  background: var(--color-bg-subtle); border-radius: var(--radius-control);
}
.category-kind button {
  min-height: 44px; border: 0; border-radius: var(--radius-control);
  color: var(--color-text-secondary); background: transparent;
}
.category-kind button[aria-pressed='true'] { background: var(--color-bg-surface); color: var(--color-brand); font-weight: 700; }
.category-hint, .category-status { margin: 10px 2px; color: var(--color-text-secondary); font-size: var(--text-footnote); }
.category-status { color: var(--color-brand); }
.category-status:empty { margin: 0; }
.category-list { display: grid; gap: 10px; }
.category-card {
  position: relative; background: var(--color-bg-surface); border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-card);
}
.category-row {
  position: relative; display: grid; grid-template-columns: 44px minmax(0, 1fr) 44px; align-items: center;
}
.category-group { grid-template-columns: 44px minmax(0, 1fr) 44px 44px; padding: 4px 4px 4px 7px; }
.category-child { margin-inline: 19px 4px; border-top: 1px solid var(--color-border-subtle); }
.icon-control, .category-name {
  min-height: 44px; padding: 0; border: 0; border-radius: var(--radius-control); background: transparent;
}
.category-row .icon-control { display: grid; place-items: center; width: 44px; color: var(--color-text-secondary); }
.category-name { color: var(--color-text-primary); display: flex; align-items: center; text-align: left; flex-wrap: wrap; gap: 4px 7px; overflow-wrap: anywhere; }
.category-group .category-name { font-weight: 700; }
.category-name small { color: var(--color-text-secondary); font-size: var(--text-footnote); }
.fold-control .app-icon { width: 15px; height: 15px; }
.fold-control[aria-expanded='true'] .app-icon { transform: rotate(90deg); }
.drag-handle { touch-action: none; user-select: none; -webkit-user-select: none; cursor: grab; }
.grip {
  width: 12px; height: 15px;
  background: radial-gradient(circle, currentColor 1px, transparent 1.6px) 0 0 / 6px 5px;
}
.category-page .add-child { margin-left: 26px; padding-inline: 8px; background: transparent; color: var(--color-brand); }
.category-card.drop-card { background: var(--color-brand-soft); border-color: var(--color-brand); }
.drag-source { opacity: .5; }
.insert-before::after, .insert-after::after {
  content: ''; position: absolute; z-index: 1; inset-inline: 5px; height: 3px;
  background: var(--color-brand); border-radius: 2px; pointer-events: none;
}
.insert-before::after { top: -2px; }
.insert-after::after { bottom: -2px; }
.category-ghost {
  position: fixed; z-index: 50; display: flex; align-items: center; gap: 6px; pointer-events: none;
  max-width: 155px; padding: 7px 10px; overflow-wrap: anywhere;
  background: var(--color-bg-surface); color: var(--color-text-primary);
  border: 1px solid var(--color-brand); border-radius: var(--radius-control);
}
.category-ghost span { min-width: 0; }
.category-error { color: var(--color-danger); font-size: var(--text-footnote); }
.category-form { display: flex; flex-direction: column; gap: 0; }
.category-form label {
  display: grid; grid-template-columns: minmax(0, auto) minmax(0, 1fr); align-items: center; gap: 12px;
  min-height: 46px; padding: 8px 14px; background: var(--color-bg-surface);
  border-bottom: 1px solid var(--color-border-subtle); color: var(--color-text-primary); font-size: 0.875rem; font-weight: 500;
}
.category-form > label:first-of-type { border-top-left-radius: 16px; border-top-right-radius: 16px; }
.category-form > label:last-of-type { border-bottom-left-radius: 16px; border-bottom-right-radius: 16px; border-bottom: 0; margin-bottom: 10px; }
.category-form .enabled { display: flex; min-height: 46px; align-items: center; justify-content: space-between; flex-direction: row-reverse; }
.category-form .enabled input[type='checkbox'] { width: 18px; height: 18px; accent-color: var(--color-brand); }
.category-form input:not([type='checkbox']), .category-form select {
  appearance: none; -webkit-appearance: none; width: 100%; min-width: 0; min-height: 30px; padding: 2px 0;
  border: 0; border-radius: 0; background: transparent; color: var(--color-text-primary);
  font-size: 0.9375rem; font-weight: 500; text-align: right; text-align-last: right;
}
.category-form select {
  padding-right: 16px;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='%238A8E9E' stroke-width='2.2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='m6 9 6 6 6-6'/%3E%3C/svg%3E");
  background-repeat: no-repeat; background-position: right center; background-size: 12px 12px;
}
.category-form input:focus-visible, .category-form select:focus-visible { outline: none; box-shadow: none; }
.category-form > .move-actions { margin-bottom: 10px; }
.category-icon-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 6px; }
.category-icon-grid button, .move-actions button {
  display: grid; place-items: center; min-height: 44px; border: 1px solid transparent;
  border-radius: var(--radius-control); color: var(--color-text-primary); background: var(--color-bg-subtle);
}
.category-icon-grid button[aria-pressed='true'] { border-color: var(--color-brand); background: var(--color-brand-soft); }
.move-actions { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.move-actions button:disabled { color: var(--color-text-disabled); }
</style>
