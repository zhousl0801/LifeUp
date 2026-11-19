<script setup lang="ts">
import Decimal from 'decimal.js'
import { computed } from 'vue'
import type { FundGroup, FundSummary } from '../../types/fund'
import { formatMoney, sumAmounts } from '../../utils/format'
import AppIcon from '../AppIcon.vue'
import BottomSheet from '../BottomSheet.vue'
import LoadingButton from '../LoadingButton.vue'

const props = defineProps<{
  open: boolean
  groups: FundGroup[]
  allFunds: FundSummary[]
  detailId: string | null
  editingGroupId: string | null
  editingGroupName: string
  groupDraft: string
  busy: boolean
  busyKey: string
  error: string
}>()

const emit = defineEmits<{
  close: []
  openDetail: [groupId: string]
  backToList: []
  'update:groupDraft': [value: string]
  submitGroup: []
  startRename: [groupId: string, name: string]
  cancelRename: []
  'update:editingGroupName': [value: string]
  saveGroupName: [groupId: string]
  removeGroup: [groupId: string, name: string]
  toggleFund: [fund: FundSummary]
}>()

const managedGroup = computed(() =>
  props.groups.find((group) => group.id === props.detailId) ?? null)

const managedGroupFunds = computed(() => {
  const groupId = managedGroup.value?.id

  return groupId ? props.allFunds.filter((fund) => fund.groupId === groupId) : []
})

const managedGroupValue = computed(() =>
  sumAmounts(managedGroupFunds.value.map((fund) => fund.assetValue)))

const groupAssetFunds = computed(() =>
  [...props.allFunds].sort((left, right) =>
    new Decimal(right.assetValue).comparedTo(left.assetValue)
      || left.name.localeCompare(right.name, 'zh-Hans-CN')))
</script>

<template>
  <BottomSheet
    :open="open"
    title="管理分组"
    :subtitle="managedGroup ? '勾选资产加入分组，取消勾选即移出' : '新增、改名或删除分组'"
    :close-disabled="busy"
    @close="emit('close')"
  >
    <div v-if="managedGroup" class="group-detail">
      <button type="button" class="group-back" @click="emit('backToList')">
        <AppIcon name="chevron-back" />
        全部分组
      </button>

      <div class="group-detail-head">
        <template v-if="editingGroupId === managedGroup.id">
          <input
            :value="editingGroupName"
            type="text"
            maxlength="20"
            aria-label="分组名称"
            @input="emit('update:editingGroupName', ($event.target as HTMLInputElement).value)"
            @keydown.enter.prevent="emit('saveGroupName', managedGroup.id)"
          />
          <LoadingButton class="confirm-button" :loading="busy" loading-label="保存中…" @click="emit('saveGroupName', managedGroup.id)">保存</LoadingButton>
          <button type="button" class="text-button" :disabled="busy" @click="emit('cancelRename')">取消</button>
        </template>
        <template v-else>
          <div class="group-detail-copy">
            <strong>{{ managedGroup.name }}</strong>
            <small>{{ managedGroupFunds.length }} 只基金 · {{ managedGroup.positionCount }} 个持仓</small>
          </div>
          <button type="button" class="text-button" @click="emit('startRename', managedGroup.id, managedGroup.name)">改名</button>
          <button
            type="button"
            class="text-button danger"
            :disabled="busy"
            @click="emit('removeGroup', managedGroup.id, managedGroup.name)"
          >删除</button>
        </template>
      </div>

      <div class="group-summary">
        <div>
          <span>已选资产（元）</span>
          <b>¥{{ formatMoney(managedGroupValue) }}</b>
        </div>
        <div>
          <span>已选资产数</span>
          <b>{{ managedGroupFunds.length }}</b>
        </div>
      </div>

      <p class="group-asset-title">选择资产</p>
      <p v-if="!groupAssetFunds.length" class="summary-note">还没有基金持仓，先录入持仓后再添加。</p>
      <section v-else class="group-asset-list">
        <button
          v-for="fund in groupAssetFunds"
          :key="fund.key"
          type="button"
          class="group-asset-row"
          :class="{ selected: fund.groupId === managedGroup.id }"
          :disabled="busy"
          :aria-busy="busyKey === fund.key"
          @click="emit('toggleFund', fund)"
        >
          <span class="group-asset-copy">
            <strong>{{ fund.name }}</strong>
            <small>当前持仓 ¥{{ formatMoney(fund.assetValue) }} · {{ fund.classes.length }} 个类别</small>
            <small v-if="fund.groupId && fund.groupId !== managedGroup.id" class="group-asset-hint">
              该产品已放置「{{ fund.groupName }}」中
            </small>
          </span>
          <span class="group-check" :class="{ checked: fund.groupId === managedGroup.id }">
            <span v-if="busyKey === fund.key" class="group-check-spinner" aria-hidden="true" />
            <AppIcon v-else-if="fund.groupId === managedGroup.id" name="check" />
          </span>
        </button>
      </section>

      <p class="summary-note">同一基金的 A/C 类会一起调整；取消勾选后回到「未分组」。</p>
    </div>

    <div v-else class="group-list">
      <section v-if="groups.length" class="group-manage-list">
        <button
          v-for="group in groups"
          :key="group.id"
          type="button"
          class="group-manage-row"
          @click="emit('openDetail', group.id)"
        >
          <span>
            <strong>{{ group.name }}</strong>
            <small>{{ group.fundCount }} 只基金 · ¥{{ formatMoney(group.confirmedValue) }}</small>
          </span>
          <AppIcon name="chevron" />
        </button>
      </section>
      <p v-else class="summary-note">还没有分组，先在下方新增一个。</p>

      <form class="form-stack" @submit.prevent="emit('submitGroup')">
        <label class="form-field">
          <span>新增分组</span>
          <div class="group-create-row">
            <input
              :value="groupDraft"
              type="text"
              maxlength="20"
              placeholder="例如：纳斯达克指数"
              @input="emit('update:groupDraft', ($event.target as HTMLInputElement).value)"
            />
            <LoadingButton class="primary-button" type="submit" :loading="busy" loading-label="添加中…">添加</LoadingButton>
          </div>
        </label>
      </form>

      <p class="summary-note">分组只改变查看范围，不影响持仓、成本和收益。</p>
    </div>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </BottomSheet>
</template>

<style scoped>
.group-create-row {
  display: flex;
  gap: 8px;
}

.group-create-row input {
  flex: 1;
  min-width: 0;
  text-align: left;
  text-align-last: left;
}

.group-create-row button {
  flex: 0 0 auto;
  min-height: 48px;
  padding: 0 18px;
  border-radius: var(--radius-pill);
  font-size: var(--text-footnote);
}

.group-detail,
.group-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.group-back {
  display: inline-flex;
  align-items: center;
  align-self: flex-start;
  gap: 2px;
  min-height: var(--touch-target);
  padding: 0 4px 0 0;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-footnote);
}

.group-back .app-icon {
  width: 16px;
  height: 16px;
}

.group-detail-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
}

.group-detail-copy {
  flex: 1;
  min-width: 0;
}

.group-detail-copy strong {
  display: block;
  font-size: 1rem;
  font-weight: 600;
}

.group-detail-copy small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-detail-head input {
  flex: 1;
  min-width: 0;
  min-height: var(--touch-target);
  padding: 0 12px;
  border: 1px solid transparent;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: var(--text-footnote);
}

.group-summary {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  padding: 12px;
  border-radius: var(--radius-control);
  background: var(--color-brand-soft);
}

.group-summary span {
  display: block;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-summary b {
  display: block;
  margin-top: 5px;
  font-size: 1rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.group-summary > div:last-child {
  text-align: right;
}

.group-asset-title {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-asset-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.group-asset-row {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 14px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  text-align: left;
}

.group-asset-row.selected {
  background: var(--color-brand-soft);
}

.group-asset-copy {
  flex: 1;
  min-width: 0;
}

.group-asset-copy strong {
  display: block;
  font-size: var(--text-footnote);
  font-weight: 600;
  line-height: 1.45;
  overflow-wrap: anywhere;
}

.group-asset-copy small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.group-asset-copy .group-asset-hint {
  color: var(--color-brand);
}

.group-check-spinner {
  width: 14px;
  height: 14px;
  border: 2px solid var(--color-brand);
  border-right-color: transparent;
  border-radius: 50%;
  animation: group-check-spin 0.7s linear infinite;
}

.group-check.checked .group-check-spinner {
  border-color: var(--color-on-brand);
  border-right-color: transparent;
}

@keyframes group-check-spin {
  to {
    transform: rotate(360deg);
  }
}

.group-check {
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  width: 22px;
  height: 22px;
  border: 1.5px solid var(--color-border-default);
  border-radius: 50%;
  color: var(--color-on-brand);
}

.group-check.checked {
  border-color: var(--color-brand);
  background: var(--color-brand);
}

.group-check .app-icon {
  width: 14px;
  height: 14px;
}

.group-manage-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.group-manage-row {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  min-height: 52px;
  padding: 12px 14px;
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  text-align: left;
}

.group-manage-row:hover {
  background: var(--color-bg-hover);
}

.group-manage-row > span {
  flex: 1;
  min-width: 0;
}

.group-manage-row strong {
  display: block;
  font-size: var(--text-footnote);
  font-weight: 600;
}

.group-manage-row small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.group-manage-row .app-icon {
  width: 16px;
  height: 16px;
  color: var(--color-text-secondary);
}

.text-button {
  flex: 0 0 auto;
  min-height: var(--touch-target);
  padding: 0 4px;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-caption);
}

.text-button.danger {
  color: var(--color-danger);
}

.confirm-button {
  min-height: 36px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand);
  color: var(--color-on-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.summary-note {
  margin: 14px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
  text-align: center;
}
</style>
