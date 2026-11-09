<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { onBeforeRouteLeave, useRoute, useRouter } from 'vue-router'
import { createEntry, fetchEntry, reverseEntry, updateEntry } from '../api/entries'
import { createCategory, deleteCategory, fetchCategories, reorderCategories } from '../api/categories'
import { ApiError, clearReadCache, getApiErrorMessage } from '../api/client'
import AmountKeypad from '../components/AmountKeypad.vue'
import AppIcon from '../components/AppIcon.vue'
import LoadingState from '../components/LoadingState.vue'
import LoadingButton from '../components/LoadingButton.vue'
import BottomSheet from '../components/BottomSheet.vue'
import ConfirmSheet from '../components/ConfirmSheet.vue'
import PageHeader from '../components/PageHeader.vue'
import { categoriesFor } from '../constants/categories'
import type { EntryCategory } from '../types/category'
import { applyAmountKey, evaluateAmountExpression, type AmountKey } from '../utils/amount'
import { formatBusinessDateWithWeekday, todayInBusinessTimeZone } from '../utils/format'
import { createRequestId } from '../utils/request'
import { returnToLedger } from '../utils/ledgerReturn'
import type { EntryKind, EntryMutationResult, EntryStatus, LedgerEntry } from '../types/entry'

interface CategoryOption {
  id: string
  name: string
  deletable: boolean
}

const route = useRoute()
const router = useRouter()
const today = todayInBusinessTimeZone()
const entryId = computed(() => (typeof route.query.id === 'string' ? route.query.id : null))
const isEditing = computed(() => entryId.value !== null)
const returnRoute = computed(() =>
  route.query.returnTo === 'cash' ? { name: 'cash' } : { name: 'ledger' })
const entryStatus = ref<EntryStatus>('active')
const entrySource = ref<LedgerEntry['source']>('manual')
const dateInput = ref<HTMLInputElement | null>(null)
const entryContent = ref<HTMLElement | null>(null)
const requestId = ref(createRequestId())
const form = reactive({
  kind: (route.query.kind === 'income' ? 'income' : 'expense') as EntryKind,
  category: '',
  expression: '',
  businessDate: today,
  note: '',
  relatedCashAdjustmentId: null as string | null
})
const initialForm = ref({ ...form })
const selectedCategories: Record<EntryKind, string> = { expense: '', income: '' }
const showAllCategories = ref(false)
const editingNote = ref(false)
const showEntryActions = ref(false)
const leaveDecision = ref<((leave: boolean) => void) | null>(null)
let allowLeave = false
const isLoading = ref(isEditing.value)
const saveMode = ref<'exit' | 'again' | null>(null)
const isSaving = computed(() => saveMode.value !== null)
const savedForNext = ref(false)
const isReversing = ref(false)
const loadError = ref('')
const formError = ref('')
const showReverse = ref(false)
const reverseReason = ref('')
const categoryEditMode = ref(false)
const categorySheet = ref<'add' | 'delete' | null>(null)
const pendingDeleteCategory = ref<CategoryOption | null>(null)
const newCategoryName = ref('')
const categorySheetError = ref('')
const isCategorySaving = ref(false)
const categoriesLoaded = ref(false)
const categoryError = ref('')
const draggingCategoryId = ref('')

const categoryItems = ref<EntryCategory[]>([])
const categoryOptions = computed<CategoryOption[]>(() => {
  const active = categoryItems.value
    .filter((category) => category.kind === form.kind && category.isActive)
    .sort((first, second) => first.sortOrder - second.sortOrder || first.name.localeCompare(second.name))

  if (active.length) {
    const options: CategoryOption[] = active.map((category) => ({
      id: category.id,
      name: category.name,
      deletable: category.systemKey !== 'salary'
    }))

    if (form.category && !options.some((option) => option.name === form.category)) {
      options.unshift({ id: `missing:${form.category}`, name: form.category, deletable: false })
    }

    return options
  }

  return categoriesFor(form.kind).map((name) => ({
    id: `fallback:${name}`,
    name,
    deletable: false
  }))
})
const quickCategories = computed(() => categoryOptions.value.slice(0, 5))
const expandedCategories = computed(() => showAllCategories.value || categoryEditMode.value)
const visibleCategories = computed(() => expandedCategories.value ? categoryOptions.value : quickCategories.value)
const hasMoreCategories = computed(() => categoryOptions.value.length > quickCategories.value.length)
const categoryToggleLabel = computed(() => {
  if (expandedCategories.value) return '收起分类'
  return form.category && !quickCategories.value.some(option => option.name === form.category)
    ? form.category
    : '全部分类'
})
const canManageCategories = computed(() => categoriesLoaded.value && canEdit.value)
const categorySheetTitle = computed(() => categorySheet.value === 'add' ? '新增分类' : '删除分类')
const amount = computed(() => evaluateAmountExpression(form.expression))
const canEdit = computed(() => entryStatus.value === 'active')
const canSubmit = computed(() => amount.value !== null && Boolean(form.category)
  && Boolean(form.businessDate) && form.businessDate <= today && !isSaving.value && !isReversing.value && canEdit.value)
const saveLabel = computed(() => isEditing.value ? '保存修改' : form.kind === 'income' ? '保存收入' : '保存支出')
const isDirty = computed(() => Object.keys(form).some((key) =>
  form[key as keyof typeof form] !== initialForm.value[key as keyof typeof form]
))

onBeforeRouteLeave(() => {
  if (allowLeave) return true
  if (isSaving.value || isReversing.value || leaveDecision.value) return false
  if (!isDirty.value) return true
  return new Promise<boolean>((resolve) => { leaveDecision.value = resolve })
})

function resolveLeave(leave: boolean): void {
  const decision = leaveDecision.value
  leaveDecision.value = null
  decision?.(leave)
}
const dateLabel = computed(() => form.businessDate
  ? formatBusinessDateWithWeekday(form.businessDate)
  : '选择日期')

let longPressTimer: ReturnType<typeof setTimeout> | undefined
let dragArmTimer: ReturnType<typeof setTimeout> | undefined
let suppressClickTimer: ReturnType<typeof setTimeout> | undefined
let longPressTriggered = false
let suppressNextCategoryClick = false
let dragActive = false
let dragArmed = false
let dragMoved = false
let dragStartX = 0
let dragStartY = 0
let dragCategoryId = ''

onMounted(async () => {
  document.addEventListener('keydown', handleKeydown)
  await loadCategories()

  if (entryId.value) {
    await loadEntry()
  }
})

async function loadCategories(): Promise<void> {
  try {
    categoryItems.value = await fetchCategories()
    categoriesLoaded.value = true
  } catch {
    categoryItems.value = []
    categoriesLoaded.value = false
  }
}

function categoryExists(kind: EntryKind, name: string): boolean {
  const active = categoryItems.value.filter(category => category.kind === kind && category.isActive)
  return active.length ? active.some(category => category.name === name) : categoriesFor(kind).includes(name)
}

onBeforeUnmount(() => {
  clearLongPressTimer()
  clearDragArmTimer()

  if (suppressClickTimer !== undefined) {
    clearTimeout(suppressClickTimer)
  }

  resolveLeave(false)
  document.removeEventListener('keydown', handleKeydown)
})

async function loadEntry(): Promise<void> {
  if (!entryId.value) {
    return
  }

  isLoading.value = true
  loadError.value = ''

  try {
    applyEntry(await fetchEntry(entryId.value))
  } catch (error) {
    loadError.value = error instanceof ApiError && error.status === 404
      ? '记录不存在。'
      : getApiErrorMessage(error, '加载记录失败，请重试。')
  } finally {
    isLoading.value = false
  }
}

function applyEntry(entry: LedgerEntry): void {
  entryStatus.value = entry.status
  entrySource.value = entry.source
  form.kind = entry.kind
  form.category = entry.category
  form.expression = entry.amount.toFixed(2)
  form.businessDate = entry.businessDate
  form.note = entry.note ?? ''
  form.relatedCashAdjustmentId = entry.relatedCashAdjustmentId ?? null
  selectedCategories[entry.kind] = entry.category
  initialForm.value = { ...form }
}

function setKind(kind: EntryKind): void {
  if (!canEdit.value || isSaving.value || form.kind === kind) {
    return
  }

  form.kind = kind
  showAllCategories.value = false
  categoryEditMode.value = false
  categorySheet.value = null
  pendingDeleteCategory.value = null
  categoryError.value = ''
  draggingCategoryId.value = ''
  dragActive = false
  dragArmed = false
  dragMoved = false
  clearDragArmTimer()

  const selected = selectedCategories[kind]
  form.category = categoryExists(kind, selected)
    || (initialForm.value.kind === kind && selected === initialForm.value.category)
    ? selected
    : ''
}

function chooseCategory(category: string): void {
  if (!canEdit.value || isSaving.value) {
    return
  }

  form.category = category
  selectedCategories[form.kind] = category
  showAllCategories.value = false
}

function clearLongPressTimer(): void {
  if (longPressTimer !== undefined) {
    clearTimeout(longPressTimer)
    longPressTimer = undefined
  }
}

function clearDragArmTimer(): void {
  if (dragArmTimer !== undefined) {
    clearTimeout(dragArmTimer)
    dragArmTimer = undefined
  }
}

function startCategoryPress(event: PointerEvent, option: CategoryOption): void {
  clearLongPressTimer()
  clearDragArmTimer()
  dragActive = false
  dragArmed = false
  dragMoved = false
  dragCategoryId = ''
  draggingCategoryId.value = ''
  longPressTriggered = false

  if (!canManageCategories.value || option.id.startsWith('fallback:') || option.id.startsWith('missing:')) {
    return
  }

  dragActive = true
  dragCategoryId = option.id
  dragStartX = event.clientX
  dragStartY = event.clientY
  ;(event.currentTarget as HTMLElement | null)?.setPointerCapture?.(event.pointerId)

  if (categoryEditMode.value) {
    dragArmTimer = setTimeout(() => {
      dragArmTimer = undefined
      dragArmed = true

      if (typeof navigator.vibrate === 'function') {
        navigator.vibrate(10)
      }
    }, 220)
    return
  }

  longPressTimer = setTimeout(() => {
    longPressTimer = undefined
    longPressTriggered = true
    categoryEditMode.value = true
    dragArmed = true

    if (typeof navigator.vibrate === 'function') {
      navigator.vibrate(10)
    }
  }, 450)
}

function moveCategoryPress(event: PointerEvent): void {
  if (!dragActive) {
    return
  }

  if (!dragArmed) {
    if (Math.hypot(event.clientX - dragStartX, event.clientY - dragStartY) > 8) {
      clearLongPressTimer()
      clearDragArmTimer()
      dragActive = false
    }

    return
  }

  if (!dragMoved) {
    dragMoved = true
    draggingCategoryId.value = dragCategoryId
  }

  const targetId = categoryTileAt(event.clientX, event.clientY)

  if (targetId && targetId !== dragCategoryId) {
    reorderCategoryPreview(dragCategoryId, targetId)
  }
}

function handleCategoryTouchMove(event: TouchEvent): void {
  if (dragArmed) {
    event.preventDefault()
  }
}

function endCategoryPress(event: PointerEvent): void {
  const moved = dragMoved
  const armed = dragArmed
  const longPressed = longPressTriggered

  clearLongPressTimer()
  clearDragArmTimer()
  dragActive = false
  dragArmed = false
  dragMoved = false
  longPressTriggered = false
  draggingCategoryId.value = ''
  dragCategoryId = ''
  ;(event.currentTarget as HTMLElement | null)?.releasePointerCapture?.(event.pointerId)

  if (moved) {
    suppressCategoryClick()
    void persistCategoryOrder()
    return
  }

  if (longPressed || armed) {
    suppressCategoryClick()
  }
}

function suppressCategoryClick(): void {
  suppressNextCategoryClick = true

  if (suppressClickTimer !== undefined) {
    clearTimeout(suppressClickTimer)
  }

  suppressClickTimer = setTimeout(() => {
    suppressClickTimer = undefined
    suppressNextCategoryClick = false
  }, 600)
}

function categoryTileAt(x: number, y: number): string | null {
  const element = document.elementFromPoint?.(x, y) as HTMLElement | null
  return element?.closest<HTMLElement>('[data-category-id]')?.dataset.categoryId ?? null
}

function isRealCategory(option: CategoryOption): boolean {
  return !option.id.startsWith('fallback:') && !option.id.startsWith('missing:')
}

function reorderCategoryPreview(sourceId: string, targetId: string): void {
  const order = categoryOptions.value
    .filter(isRealCategory)
    .map((option) => option.id)
  const from = order.indexOf(sourceId)
  const to = order.indexOf(targetId)

  if (from < 0 || to < 0 || from === to) {
    return
  }

  const [moved] = order.splice(from, 1)
  order.splice(to, 0, moved!)
  applyCategoryOrder(order)
}

function applyCategoryOrder(ids: string[]): void {
  const positions = new Map(ids.map((id, index) => [id, index]))
  categoryItems.value = categoryItems.value.map((category) => {
    const position = positions.get(category.id)

    return position === undefined ? category : { ...category, sortOrder: position }
  })
}

function categoryOrderIds(): string[] {
  const activeIds = categoryOptions.value.filter(isRealCategory).map((option) => option.id)
  const inactiveIds = categoryItems.value
    .filter((category) => category.kind === form.kind && !category.isActive)
    .sort((first, second) => first.sortOrder - second.sortOrder || first.name.localeCompare(second.name))
    .map((category) => category.id)

  return [...activeIds, ...inactiveIds]
}

async function persistCategoryOrder(): Promise<void> {
  const ids = categoryOrderIds()

  if (ids.length < 2) {
    return
  }

  categoryError.value = ''

  try {
    await reorderCategories(form.kind, ids)
  } catch (error) {
    categoryError.value = getApiErrorMessage(error, '调整分类顺序失败，请重试。')
    await loadCategories()
  }
}

function handleCategoryClick(option: CategoryOption): void {
  if (suppressNextCategoryClick) {
    suppressNextCategoryClick = false

    if (suppressClickTimer !== undefined) {
      clearTimeout(suppressClickTimer)
      suppressClickTimer = undefined
    }

    return
  }

  if (categoryEditMode.value) {
    if (option.deletable) {
      openDeleteCategory(option)
    }

    return
  }

  chooseCategory(option.name)
}

function openAddCategory(): void {
  clearLongPressTimer()
  newCategoryName.value = ''
  categorySheetError.value = ''
  categorySheet.value = 'add'
}

function openDeleteCategory(option: CategoryOption): void {
  clearLongPressTimer()
  pendingDeleteCategory.value = option
  categorySheetError.value = ''
  categorySheet.value = 'delete'
}

function closeCategorySheet(): void {
  if (isCategorySaving.value) {
    return
  }

  categorySheet.value = null
  pendingDeleteCategory.value = null
}

function exitCategoryEdit(): void {
  clearLongPressTimer()
  clearDragArmTimer()
  categoryEditMode.value = false
  draggingCategoryId.value = ''
  categoryError.value = ''
  dragActive = false
  dragArmed = false
  dragMoved = false
}

async function saveCategory(): Promise<void> {
  const name = newCategoryName.value.trim()

  if (!name) {
    categorySheetError.value = '请输入分类名称。'
    return
  }

  if (name.length > 20) {
    categorySheetError.value = '分类名称不能超过 20 个字符。'
    return
  }

  isCategorySaving.value = true
  categorySheetError.value = ''

  try {
    const created = await createCategory({ kind: form.kind, name })
    await loadCategories()
    chooseCategory(created.name)
    categorySheet.value = null
    categoryEditMode.value = false
  } catch (error) {
    categorySheetError.value = error instanceof ApiError && error.status === 409
      ? '同类型下已存在同名分类。'
      : getApiErrorMessage(error, '新增分类失败，请重试。')
  } finally {
    isCategorySaving.value = false
  }
}

async function confirmDeleteCategory(): Promise<void> {
  const target = pendingDeleteCategory.value

  if (!target || isCategorySaving.value) {
    return
  }

  if (categoryItems.value.filter((category) => category.kind === form.kind && category.isActive).length <= 1) {
    categorySheetError.value = '至少保留一个分类。'
    return
  }

  isCategorySaving.value = true
  categorySheetError.value = ''

  try {
    await deleteCategory(target.id)
    await loadCategories()

    if (form.category === target.name) {
      form.category = ''
      selectedCategories[form.kind] = ''
    }

    categorySheet.value = null
    pendingDeleteCategory.value = null
  } catch (error) {
    categorySheetError.value = getApiErrorMessage(error, '删除分类失败，请重试。')
  } finally {
    isCategorySaving.value = false
  }
}

function pickDate(): void {
  const input = dateInput.value

  if (!input) {
    return
  }

  input.focus()

  try {
    input.showPicker()
  } catch {
    // 浏览器不支持或不在用户手势中时，保留聚焦即可。
  }
}

async function submit(mode: 'exit' | 'again' = 'exit'): Promise<void> {
  if (amount.value === null || isSaving.value || isReversing.value || !canEdit.value
    || (mode === 'again' && isEditing.value)) {
    return
  }

  formError.value = ''

  if (!form.businessDate) {
    formError.value = '请选择业务日期。'
    return
  }

  if (form.businessDate > today) {
    formError.value = '业务日期不能晚于今天。'
    return
  }

  if (!form.category) {
    formError.value = '请选择分类。'
    return
  }

  saveMode.value = mode

  try {
    const input = {
      requestId: requestId.value,
      kind: form.kind,
      category: form.category,
      relatedCashAdjustmentId: form.relatedCashAdjustmentId,
      amount: amount.value,
      businessDate: form.businessDate,
      note: form.note.trim()
    }
    const result = entryId.value
      ? await updateEntry(entryId.value, input)
      : await createEntry(input)

    if (mode === 'again') {
      clearReadCache()
      form.category = ''
      form.expression = ''
      form.note = ''
      form.relatedCashAdjustmentId = null
      selectedCategories.expense = ''
      selectedCategories.income = ''
      showAllCategories.value = false
      requestId.value = createRequestId()
      initialForm.value = { ...form }
      savedForNext.value = true
      if (entryContent.value) entryContent.value.scrollTop = 0
    } else {
      await finishSave(result, entryId.value ? 'update' : 'create')
    }
  } catch (error) {
    formError.value = getApiErrorMessage(error, '保存失败，请重试。')
  } finally {
    saveMode.value = null
  }
}

async function confirmReverse(): Promise<void> {
  if (!entryId.value || isReversing.value || isSaving.value) {
    return
  }

  isReversing.value = true
  formError.value = ''

  try {
    const result = await reverseEntry(entryId.value, reverseReason.value.trim(), requestId.value)
    await finishSave(result, 'reverse')
  } catch (error) {
    formError.value = getApiErrorMessage(error, '撤销失败，请重试。')
  } finally {
    isReversing.value = false
  }
}

async function finishSave(result: EntryMutationResult, action: 'create' | 'update' | 'reverse'): Promise<void> {
  clearReadCache()
  allowLeave = true
  if (returnRoute.value.name === 'cash') {
    await router.replace(returnRoute.value)
    return
  }

  const back = router.options.history.state.back
  if (typeof back === 'string' && router.resolve(back).name === 'ledger') {
    returnToLedger({ entry: result.entry, action })
    router.back()
    return
  }

  await router.replace({
    name: 'ledger',
    query: action === 'reverse' ? {} : { date: result.entry.businessDate, focus: result.entry.id }
  })
}

function goBack(): void {
  const previous = (router.options.history.state as { back?: string | null }).back

  if (previous) {
    router.back()
    return
  }

  void router.push(returnRoute.value)
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.defaultPrevented || categorySheet.value !== null || leaveDecision.value || isSaving.value
    || (event.target instanceof Element && event.target.closest('[role="dialog"]'))
    || event.metaKey || event.ctrlKey || event.altKey) {
    return
  }

  const target = event.target as HTMLElement | null

  if (target?.closest('input, textarea, select')) {
    return
  }

  const isInteractiveFocused = Boolean(target?.closest('button, summary, a'))

  if (/^[0-9.+-]$/.test(event.key)) {
    event.preventDefault()

    if (canEdit.value) {
      form.expression = applyAmountKey(form.expression, event.key as AmountKey)
    }

    return
  }

  if (event.key === 'Backspace') {
    event.preventDefault()

    if (canEdit.value) {
      form.expression = applyAmountKey(form.expression, 'back')
    }

    return
  }

  if (event.key === 'Enter') {
    if (isInteractiveFocused) {
      return
    }

    event.preventDefault()
    void submit()
    return
  }

  if (event.key === 'Escape') {
    event.preventDefault()
    goBack()
  }
}

</script>

<template>
  <main class="entry-screen">
    <header class="entry-header">
      <PageHeader class="entry-heading" :title="isEditing ? '编辑账单' : '记一笔'"
        :back="!categoryEditMode" @back="goBack">
        <template #leading>
          <button v-if="categoryEditMode" type="button" class="header-action"
            @click="exitCategoryEdit">完成</button>
        </template>
        <template #actions>
          <button v-if="isEditing && canEdit" type="button" class="header-action"
            :aria-expanded="showEntryActions" @click="showEntryActions = !showEntryActions">更多</button>
        </template>
      </PageHeader>
      <div class="kind-tabs" role="tablist" aria-label="收支方向">
        <button
          type="button"
          role="tab"
          :aria-selected="form.kind === 'expense'"
          :class="{ active: form.kind === 'expense' }"
          :disabled="!canEdit || isSaving"
          @click="setKind('expense')"
        >
          支出
        </button>
        <button
          type="button"
          role="tab"
          :aria-selected="form.kind === 'income'"
          :class="{ active: form.kind === 'income' }"
          :disabled="!canEdit || isSaving"
          @click="setKind('income')"
        >
          收入
        </button>
      </div>
    </header>

    <div v-if="isLoading" class="state entry-state">
      <LoadingState variant="list" :rows="4" label="正在读取记录" />
    </div>

    <section v-else-if="loadError" class="state entry-state">
      <p>{{ loadError }}</p>
      <button type="button" class="secondary-button" @click="goBack">返回账本</button>
    </section>

    <template v-else>
      <div ref="entryContent" class="entry-content">
        <div class="amount-panel">
          <div class="amount-heading">
            <span>金额</span>
            <span v-if="savedForNext && !isDirty" role="status">已保存，可以记下一笔。</span>
          </div>
          <div class="amount-display"><span aria-hidden="true">¥</span>
            <output class="amount-expression" aria-label="输入金额">{{ form.expression || '0' }}</output>
          </div>
        </div>
        <p class="category-label">分类<span v-if="!form.category"> · 请选择</span></p>
        <div v-if="entryStatus === 'reversed'" class="reversed-banner">
          该记录已撤销，不能修改。
        </div>

        <div class="category-grid" :class="{ editing: categoryEditMode }" role="group" aria-label="选择分类" @touchmove="handleCategoryTouchMove">
          <button
            v-for="option in visibleCategories"
            :key="option.id"
            type="button"
            class="category-tile"
            :class="{
              active: !categoryEditMode && form.category === option.name,
              editing: categoryEditMode,
              dragging: draggingCategoryId === option.id
            }"
            :data-category-id="option.id"
            :disabled="!canEdit || isSaving"
            :aria-pressed="form.category === option.name"
            @pointerdown="startCategoryPress($event, option)"
            @pointermove="moveCategoryPress"
            @pointerup="endCategoryPress"
            @pointercancel="endCategoryPress"
            @click="handleCategoryClick(option)"
            @contextmenu.prevent
          >
            <span class="category-mark-wrap">
              <span class="category-mark"><AppIcon :name="option.name" /></span>
              <span
                v-if="categoryEditMode && option.deletable"
                class="category-remove"
                aria-hidden="true"
              >×</span>
            </span>
            <span class="category-name">{{ option.name }}</span>
          </button>
          <button v-if="hasMoreCategories && !categoryEditMode" type="button" class="category-toggle"
            :aria-expanded="expandedCategories" @click="showAllCategories = !showAllCategories">
            <span class="category-name">{{ categoryToggleLabel }}</span>
            <span aria-hidden="true">{{ expandedCategories ? '⌃' : '›' }}</span>
          </button>
          <button
            v-if="categoryEditMode && canManageCategories"
            type="button"
            class="category-tile category-add"
            @click="openAddCategory"
          >
            <span class="category-mark-wrap">
              <span class="category-mark"><AppIcon name="plus" /></span>
            </span>
            <span class="category-name">添加</span>
          </button>
        </div>
        <p v-if="categoryEditMode && canManageCategories" class="category-hint">
          长按拖动分类可调整顺序，点分类可删除
        </p>
        <p v-if="categoryError" class="form-error category-error" role="alert">{{ categoryError }}</p>

        <div v-if="showEntryActions || showReverse || entrySource === 'automatic'" class="category-extras">
          <button
            v-if="showEntryActions && isEditing && canEdit"
            type="button"
            class="reverse-link"
            @click="showReverse = true"
          >
            撤销这笔记录
          </button>

          <section v-if="showReverse" class="reverse-panel">
            <p>撤销会恢复这笔记录对现金的影响，并保留原始记录。</p>
            <label class="field">
              <span>撤销原因</span>
              <input v-model="reverseReason" type="text" maxlength="200" placeholder="选填" />
            </label>
            <div class="reverse-actions">
              <button
                type="button"
                class="secondary-button"
                :disabled="isReversing"
                @click="showReverse = false"
              >
                取消
              </button>
              <LoadingButton
                class="danger-button"
                :loading="isReversing"
                loading-label="撤销中…"
                @click="confirmReverse"
              >
                确认撤销
              </LoadingButton>
            </div>
          </section>

          <p v-if="entrySource === 'automatic'" class="source-note">自动记账记录</p>
        </div>
        <div class="entry-fields">
          <label class="entry-date">
            <AppIcon name="calendar" aria-hidden="true" />
            <span>{{ dateLabel }}</span>
            <input v-model="form.businessDate" type="date" :max="today" :disabled="!canEdit || isSaving"
              aria-label="业务日期" @click="pickDate" ref="dateInput" />
          </label>
          <label class="note-bar">
            <input v-model="form.note" type="text" maxlength="200" placeholder="备注（选填）"
              :disabled="!canEdit || isSaving" aria-label="备注"
              @focus="editingNote = true" @blur="editingNote = false" />
          </label>
        </div>
      </div>

      <footer v-if="canEdit" class="entry-footer">
        <p v-if="formError" class="form-error entry-error" role="alert">{{ formError }}</p>
        <AmountKeypad v-if="!editingNote" v-model="form.expression" :disabled="isSaving" />
        <div class="entry-save-actions" :class="{ 'entry-save-actions--new': !isEditing }">
          <LoadingButton v-if="!isEditing" class="entry-save-again" :loading="saveMode === 'again'"
            :disabled="!canSubmit" loading-label="保存中…" @click="submit('again')">保存后再记</LoadingButton>
          <LoadingButton class="entry-save" :loading="saveMode === 'exit'" :disabled="!canSubmit"
            loading-label="保存中…" @click="submit()">{{ saveLabel }}</LoadingButton>
        </div>
      </footer>

      <ConfirmSheet :open="leaveDecision !== null" title="这笔账还没保存"
        :message="isEditing ? '再改一会儿，还是放弃这次修改？' : '要接着记，还是放弃这笔账？'"
        confirm-label="放弃" cancel-label="继续" close-icon danger
        @confirm="resolveLeave(true)" @cancel="resolveLeave(false)" />

      <BottomSheet
        :open="categorySheet !== null"
        :title="categorySheetTitle"
        :subtitle="categorySheet === 'add' ? `新增后立即选中，只影响「${form.kind === 'income' ? '收入' : '支出'}」分类` : '历史记录仍会保留该分类名称'"
        :close-disabled="isCategorySaving"
        @close="closeCategorySheet"
      >
        <form v-if="categorySheet === 'add'" class="form-stack" @submit.prevent="saveCategory">
          <label class="form-field">
            <span>分类名称</span>
            <input v-model="newCategoryName" maxlength="20" placeholder="例如：咖啡" />
          </label>
          <p v-if="categorySheetError" class="form-error">{{ categorySheetError }}</p>
          <div class="form-actions">
            <button type="button" class="secondary-button" :disabled="isCategorySaving" @click="closeCategorySheet">取消</button>
            <LoadingButton class="primary-button" type="submit" :loading="isCategorySaving" :disabled="!newCategoryName.trim()" loading-label="保存中…">
              保存
            </LoadingButton>
          </div>
        </form>

        <template v-else-if="categorySheet === 'delete' && pendingDeleteCategory">
          <p class="category-delete-note">
            删除「{{ pendingDeleteCategory.name }}」后，新记账不再显示该分类；已有记录仍保留原分类名称。
          </p>
          <p v-if="categorySheetError" class="form-error">{{ categorySheetError }}</p>
          <div class="form-actions">
            <button type="button" class="secondary-button" :disabled="isCategorySaving" @click="closeCategorySheet">取消</button>
            <LoadingButton class="danger-button" :loading="isCategorySaving" loading-label="删除中…" @click="confirmDeleteCategory">
              确认删除
            </LoadingButton>
          </div>
        </template>
      </BottomSheet>
    </template>
  </main>
</template>

<style scoped>
.entry-screen {
  display: flex;
  flex-direction: column;
  width: min(100%, 430px);
  height: 100dvh;
  margin: 0 auto;
  overflow: hidden;
  background: var(--color-bg-page);
}

.entry-header {
  flex-shrink: 0;
  padding: env(safe-area-inset-top) 16px 0;
}

.entry-heading {
  margin-bottom: 4px;
}

.header-action {
  min-height: var(--touch-target);
  padding: 0 4px;
  border: 0;
  background: transparent;
  color: var(--color-brand);
}

.kind-tabs {
  display: flex;
  gap: 4px;
  padding: 3px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
}

.kind-tabs button {
  flex: 1;
  min-height: 40px;
  border: 0;
  border-radius: var(--radius-control);
  background: transparent;
  color: var(--color-text-secondary);
}

.kind-tabs button.active {
  background: var(--color-bg-surface);
  color: var(--color-brand);
  font-weight: 700;
}

.entry-content {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overscroll-behavior: contain;
  padding: 10px 16px 12px;
}

.category-label {
  margin: 12px 0 2px;
  font-weight: 600;
}

.category-label span {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 400;
}

.category-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 6px;
  padding: 6px 0 0;
}

.category-tile,
.category-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  min-width: 0;
  min-height: var(--touch-target);
  padding: 4px 6px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: 0.875rem;
  -webkit-touch-callout: none;
  user-select: none;
  touch-action: manipulation;
}

.category-tile.active {
  background: var(--color-brand-soft);
}

.category-toggle {
  color: var(--color-brand);
}

.category-grid.editing .category-tile {
  touch-action: pan-y;
}

.category-tile.dragging {
  opacity: 0.55;
  transform: scale(.96);
}

.category-mark-wrap {
  position: relative;
  display: inline-flex;
}

.category-mark {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  flex: 0 0 auto;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  transition: background var(--motion-fast) ease, color var(--motion-fast) ease;
}

.category-mark :deep(.app-icon) {
  width: 15px;
  height: 15px;
}

@media (hover: hover) and (pointer: fine) {
  .category-tile:hover:not(:disabled) .category-mark {
    background: var(--color-brand-soft-hover);
    color: var(--color-brand);
  }
}

.category-tile.active .category-mark,
.category-tile.active:hover .category-mark,
.category-tile.active:active .category-mark {
  background: var(--color-brand);
  color: var(--color-on-brand);
  box-shadow: 0 8px 20px color-mix(in srgb, var(--color-brand) 28%, transparent);
}

.category-name {
  max-width: 100%;
  overflow: hidden;
  font-weight: 500;
  line-height: 1.2;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.category-tile.active .category-name {
  color: var(--color-brand);
  font-weight: 700;
}

.category-tile:disabled {
  opacity: 0.5;
}

.category-remove {
  position: absolute;
  top: -3px;
  right: -3px;
  display: inline-flex;
  justify-content: center;
  align-items: center;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: var(--color-danger);
  color: var(--color-on-danger);
  font-size: 0.875rem;
  font-weight: 600;
  line-height: 1;
  box-shadow: 0 1px 3px var(--color-shadow);
}

.category-add .category-mark {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.category-add .category-name {
  color: var(--color-brand);
  font-weight: 600;
}

.category-hint {
  margin: 12px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  text-align: center;
}

.category-error {
  margin: 12px 0 0;
}

.category-extras {
  margin-top: 20px;
  padding-top: 14px;
  border-top: 1px solid var(--color-border-subtle);
}

.category-delete-note {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.7;
}

.amount-panel {
  padding: 10px 14px;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.amount-heading {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.amount-heading [role='status'] {
  color: var(--color-brand);
}

.amount-display {
  display: flex;
  align-items: baseline;
  gap: 5px;
  color: var(--color-text-primary);
  font-size: clamp(1.65rem, 8vw, 2.5rem);
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  line-height: 1.12;
}

.amount-expression {
  min-width: 0;
  overflow-wrap: anywhere;
}

.entry-fields {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 6px;
  margin-top: 10px;
}

.entry-date {
  position: relative;
  display: flex;
  align-items: center;
  gap: 5px;
  min-height: var(--touch-target);
  padding: 0 10px;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  color: var(--color-brand);
  font-size: var(--text-body);
}

.entry-date .app-icon {
  width: 16px;
  height: 16px;
}

.entry-date input {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  opacity: 0;
  cursor: pointer;
}

.entry-date:focus-within {
  outline: 2px solid var(--color-focus-ring);
}

.note-bar input {
  width: 100%;
  min-height: var(--touch-target);
  padding: 0 12px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: var(--text-body);
}

.field {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid var(--color-border-subtle);
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.field input {
  flex: 1;
  min-width: 0;
  max-width: 240px;
  padding: 6px 0;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  font-size: 0.875rem;
  text-align: right;
}

.reversed-banner {
  margin-bottom: 12px;
  padding: 10px 12px;
  border-radius: var(--radius-tag);
  background: var(--color-warning-bg);
  color: var(--color-warning);
  font-size: 0.75rem;
  line-height: 1.7;
}

.reverse-link {
  margin-top: 0;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-danger);
  font-size: 0.8125rem;
}

.reverse-panel {
  margin-top: 14px;
  padding: 16px 18px;
  border: 0;
  border-radius: var(--radius-card);
  background: var(--color-bg-subtle);
}

.reverse-panel p {
  margin: 0 0 8px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.7;
}

.reverse-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 12px;
}

.danger-button {
  min-height: 48px;
  padding: 0 20px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-danger);
  color: var(--color-on-danger);
  font-size: var(--text-subhead);
  font-weight: 700;
}

.danger-button:hover:not(:disabled) {
  background: var(--color-danger-hover);
}

.danger-button:active:not(:disabled) {
  background: var(--color-danger-active);
}

.danger-button:disabled {
  background: var(--color-bg-disabled);
  color: var(--color-text-disabled);
}

.form-error {
  margin: 14px 0 0;
  padding: 12px 14px;
  border-radius: var(--radius-control);
  background: var(--color-error-bg);
  color: var(--color-error);
  font-size: var(--text-footnote);
  line-height: 1.7;
}

.entry-error {
  margin: 0 16px 8px;
}

.source-note {
  margin: 10px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.entry-footer {
  flex-shrink: 0;
  padding: 6px 12px max(8px, env(safe-area-inset-bottom));
  border-top: 1px solid var(--color-border-subtle);
  background: var(--color-bg-surface);
}

.entry-save-actions {
  display: grid;
  gap: 8px;
  margin-top: 6px;
}

.entry-save-actions--new {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.entry-save,
.entry-save-again {
  min-height: var(--touch-target);
  padding-inline: 6px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-weight: 700;
}

.entry-save-again {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.entry-save:disabled:not([aria-busy]),
.entry-save-again:disabled:not([aria-busy]) {
  background: var(--color-bg-disabled);
  color: var(--color-text-disabled);
}

@media (max-height: 700px) {
  .entry-content { padding-top: 6px; }
  .amount-panel { padding-block: 6px; }
  .category-label { margin-top: 7px; }
  .entry-fields { margin-top: 7px; }
}

.entry-state {
  padding: 24px 18px;
}
</style>
