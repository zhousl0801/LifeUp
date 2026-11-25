<script setup lang="ts">
import { navigateBack } from '../utils/navigation'
import { useAmountPrivacy } from '../composables/useAmountPrivacy'
import { toast } from '../composables/useMessage'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  downloadBackup,
  downloadDetails,
  fetchBackupSnapshot,
  restoreBackup,
  validateBackup
} from '../api/backup'
import { ApiError } from '../api/client'
import type { BackupSummary, BackupValidation, RestoreBackupResult } from '../api/backup'
import BackButton from '../components/BackButton.vue'
import LoadingButton from '../components/LoadingButton.vue'
import { addAmounts, formatBusinessDate } from '../utils/format'

const router = useRouter()
const { maskMoney } = useAmountPrivacy()
const backup = ref<unknown>(null)
const fileName = ref('')
const isReading = ref(false)
const isValidating = ref(false)
const isRestoring = ref(false)
const isExporting = ref(false)
const exportingKind = ref<'details' | 'backup' | ''>('')
const PreviousSnapshotKey = 'lifeup.backup.previousSnapshot'

interface StoredPreviousSnapshot {
  savedAt: string
  ledgerId: string | null
  snapshot: unknown
}

const validation = ref<BackupValidation | null>(null)
const restoreResult = ref<RestoreBackupResult | null>(null)
const previousSnapshot = ref<StoredPreviousSnapshot | null>(null)
const errorMessage = ref('')

function readStoredSnapshot(): StoredPreviousSnapshot | null {
  try {
    const raw = sessionStorage.getItem(PreviousSnapshotKey)
    if (!raw) return null
    const parsed = JSON.parse(raw) as StoredPreviousSnapshot
    if (parsed && typeof parsed.savedAt === 'string' && 'snapshot' in parsed) {
      return parsed
    }
  } catch {
    // Ignore storage read errors in restricted contexts.
  }
  return null
}

function saveStoredSnapshot(record: StoredPreviousSnapshot | null): void {
  previousSnapshot.value = record
  try {
    if (record) {
      sessionStorage.setItem(PreviousSnapshotKey, JSON.stringify(record))
    } else {
      sessionStorage.removeItem(PreviousSnapshotKey)
    }
  } catch {
    // Keep in-memory ref even if sessionStorage quota is exceeded.
  }
}

onMounted(() => {
  previousSnapshot.value = readStoredSnapshot()
})
const confirmReplace = ref(false)
const exportFrom = ref('')
const exportTo = ref('')

const hasBackup = computed(() => backup.value !== null)
const canRestore = computed(() =>
  validation.value?.isValid === true
  && validation.value.validationToken !== null
  && (!validation.value.willReplace || confirmReplace.value)
  && !isRestoring.value)

function goBack(): void {
  navigateBack(router, { name: 'mine' })
}

async function exportDetails(): Promise<void> {
  isExporting.value = true
  exportingKind.value = 'details'
  errorMessage.value = ''

  try {
    await downloadDetails(exportFrom.value, exportTo.value)
    toast.success('明细 CSV 已导出')
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '导出明细失败，请重试。'
  } finally {
    isExporting.value = false
    exportingKind.value = ''
  }
}

async function exportBackup(): Promise<void> {
  isExporting.value = true
  exportingKind.value = 'backup'
  errorMessage.value = ''

  try {
    await downloadBackup()
    toast.success('完整备份已导出')
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '下载备份失败，请重试。'
  } finally {
    isExporting.value = false
    exportingKind.value = ''
  }
}

async function chooseFile(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]

  if (!file) {
    return
  }

  fileName.value = file.name
  backup.value = null
  validation.value = null
  restoreResult.value = null
  confirmReplace.value = false
  errorMessage.value = ''
  isReading.value = true

  try {
    const text = await file.text()
    backup.value = JSON.parse(text) as unknown
    await validateSelected()
  } catch {
    errorMessage.value = '无法读取备份文件：请确认它是本应用导出的 JSON 文件。'
  } finally {
    isReading.value = false
  }
}

async function validateSelected(): Promise<void> {
  if (!hasBackup.value) {
    errorMessage.value = '请先选择备份文件。'
    return
  }

  isValidating.value = true
  errorMessage.value = ''
  restoreResult.value = null
  confirmReplace.value = false

  try {
    validation.value = await validateBackup(backup.value)
    if (validation.value.isValid) {
      toast.success('备份校验通过')
    } else {
      errorMessage.value = '备份校验未通过，未修改当前账本。'
    }
  } catch (error) {
    validation.value = null
    errorMessage.value = error instanceof Error ? error.message : '备份校验失败，请重试。'
  } finally {
    isValidating.value = false
  }
}

async function restore(): Promise<void> {
  const current = validation.value

  if (!current?.isValid || !current.validationToken || !canRestore.value) {
    return
  }

  isRestoring.value = true
  errorMessage.value = ''

  try {
    let snapshotRecord: StoredPreviousSnapshot | null = null
    if (current.willReplace && typeof fetchBackupSnapshot === 'function') {
      try {
        const snapshot = await fetchBackupSnapshot()
        snapshotRecord = {
          savedAt: new Date().toISOString(),
          ledgerId: current.current?.ledgerId ?? null,
          snapshot
        }
        // 在覆盖写入前先持久化旧快照，即使网络超时或切换页面后仍可取回。
        saveStoredSnapshot(snapshotRecord)
      } catch {
        // 无法预取旧快照时不静默跳过，提示用户先手动下载再替换。
        errorMessage.value = '替换前自动保留当前账本失败，为避免数据丢失已取消本次替换，请先手动下载完整备份后再试。'
        return
      }
    }

    restoreResult.value = await restoreBackup(
      backup.value,
      current.validationToken,
      current.willReplace
    )
    toast.success('账本已恢复')
    validation.value = null
    backup.value = null
    confirmReplace.value = false
  } catch (error) {
    if (error instanceof ApiError && error.status === 0) {
      errorMessage.value = '暂时无法确认恢复结果，请先返回总览核对当前账本，避免重复覆盖。替换前的旧账本快照已保留在下方。'
    } else {
      // 服务端明确返回失败时当前账本未被覆盖，清理本次预存快照避免误导。
      if (current.willReplace) {
        saveStoredSnapshot(null)
      }
      errorMessage.value = error instanceof Error ? error.message : '恢复失败，当前账本未修改。'
    }
  } finally {
    isRestoring.value = false
  }
}

function downloadPreviousSnapshot(): void {
  if (!previousSnapshot.value) return
  const blob = new Blob([JSON.stringify(previousSnapshot.value.snapshot, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `lifeup-before-restore-${previousSnapshot.value.savedAt.slice(0, 10)}.json`
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

async function rollbackToPrevious(): Promise<void> {
  if (!previousSnapshot.value) return
  backup.value = previousSnapshot.value.snapshot
  fileName.value = '替换前的账本快照'
  await validateSelected()
}

function clearPreviousSnapshot(): void {
  saveStoredSnapshot(null)
}

function summaryItems(summary: BackupSummary): { label: string; value: string }[] {
  return [
    { label: '账单记录', value: `${summary.entryCount} 条` },
    { label: '资产记录', value: `${summary.assetTransactionCount} 条` },
    { label: '自动记账', value: `${summary.planCount} 个` },
    { label: '执行期次', value: `${summary.planOccurrenceCount} 个` },
    { label: '现金', value: maskMoney(summary.cashBalance) },
    { label: '公积金', value: summary.providentBalance === null ? '未录入' : maskMoney(summary.providentBalance) },
    { label: '基金', value: maskMoney(addAmounts(summary.fundValue, summary.fundPending, summary.fundPendingArrival)) },
    { label: '股票', value: summary.stockValue === null ? '未录入' : maskMoney(summary.stockValue) },
    { label: '贵金属', value: summary.goldValue === null ? '未录入' : maskMoney(addAmounts(summary.goldValue, summary.goldPendingArrival)) },
    { label: '总资产', value: summary.totalAssets === null ? '缺少报价' : maskMoney(summary.totalAssets) },
    { label: '起始日期', value: formatBusinessDate(summary.startDate) }
  ]
}

function technicalSummaryItems(summary: BackupSummary): { label: string; value: string }[] {
  return [
    { label: '请求回执', value: `${summary.mutationReceiptCount} 条` },
    { label: '行情观测', value: `${summary.priceObservationCount} 条` }
  ]
}
</script>

<template>
  <main class="page backup-page">
    <header class="backup-header">
      <BackButton @back="goBack" />
      <h1>数据导出与备份</h1>
      <span class="header-spacer" />
    </header>

    <p class="page-intro">
      完整备份用于换设备或误操作后还原全部数据；明细表用于导出到表格软件核对收支与交易。
    </p>

    <section class="backup-card">
      <header>
        <h2>完整备份</h2>
        <small>包含全部账本数据，不受日期筛选影响</small>
      </header>
      <div class="backup-actions">
        <LoadingButton
          class="action-primary"
          :loading="exportingKind === 'backup'"
          :disabled="isExporting"
          loading-label="下载中…"
          @click="exportBackup"
        >
          下载完整备份
        </LoadingButton>
      </div>
    </section>

    <section class="backup-card">
      <header>
        <h2>导出明细表</h2>
        <small>只读取，不改变账本</small>
      </header>
      <details class="date-filter-details">
        <summary>按日期范围导出（默认导出全部）</summary>
        <div class="form-row">
          <label class="form-field">
            <span>开始日期（选填）</span>
            <input v-model="exportFrom" type="date" />
          </label>
          <label class="form-field">
            <span>结束日期（选填）</span>
            <input v-model="exportTo" type="date" />
          </label>
        </div>
      </details>
      <div class="backup-actions">
        <LoadingButton
          class="action-secondary"
          :loading="exportingKind === 'details'"
          :disabled="isExporting"
          loading-label="导出中…"
          @click="exportDetails"
        >
          导出明细 CSV
        </LoadingButton>
      </div>
      <p class="helper-note">包含日期、类型、金额、现金变动、数量、净值、费用、分类与备注。</p>
    </section>

    <section class="backup-card">
      <header>
        <h2>恢复备份</h2>
        <small>先校验，再确认</small>
      </header>
      <label class="file-field">
        <span>选择备份 JSON</span>
        <input type="file" accept=".json,application/json" @change="chooseFile" />
      </label>
      <p v-if="fileName" class="file-name">已选择：{{ fileName }}</p>

      <div class="backup-actions">
        <LoadingButton
          class="action-secondary"
          :loading="isValidating"
          :disabled="!hasBackup || isReading"
          loading-label="正在校验…"
          @click="validateSelected"
        >
          重新校验
        </LoadingButton>
      </div>

      <p class="helper-note">
        备份文件只在本机浏览器读取后发送到当前账本服务校验。校验通过前不会写入任何数据。
      </p>
    </section>

    <p v-if="errorMessage" class="form-error" role="alert">{{ errorMessage }}</p>

    <section v-if="restoreResult" class="backup-card success-card">
      <header>
        <h2>恢复完成</h2>
        <small>{{ formatBusinessDate(restoreResult.restoredAt.slice(0, 10)) }}</small>
      </header>
      <div class="summary-grid">
        <div v-for="item in summaryItems(restoreResult.summary)" :key="`restored-${item.label}`">
          <span>{{ item.label }}</span>
          <b>{{ item.value }}</b>
        </div>
      </div>
      <RouterLink class="home-link" to="/">返回总览核对余额 ›</RouterLink>
    </section>

    <section v-if="previousSnapshot" class="backup-card">
      <header>
        <h2>替换前的账本快照</h2>
        <small>{{ formatBusinessDate(previousSnapshot.savedAt.slice(0, 10)) }}</small>
      </header>
      <p class="helper-note">已保留本次替换前的旧账本快照，离开页面后也可回来下载或取回。</p>
      <div class="backup-actions">
        <button type="button" class="action-secondary" @click="downloadPreviousSnapshot">下载旧账本</button>
        <button type="button" class="action-secondary" @click="rollbackToPrevious">取回替换前的账本</button>
      </div>
      <button type="button" class="text-button clear-snapshot-btn" @click="clearPreviousSnapshot">不再需要，清除快照</button>
    </section>

    <template v-if="validation">
      <section class="backup-card" :class="validation.isValid ? 'valid-card' : 'invalid-card'">
        <header>
          <h2>{{ validation.isValid ? '校验通过' : '校验未通过' }}</h2>
          <small>{{ validation.isValid ? (validation.willReplace ? '将替换当前账本' : '将建立新账本') : '不会修改当前账本' }}</small>
        </header>

        <ul v-if="validation.errors.length" class="validation-list errors">
          <li v-for="error in validation.errors" :key="error">{{ error }}</li>
        </ul>

        <div class="summary-compare">
          <article v-if="validation.current">
            <h3>当前账本</h3>
            <div class="summary-grid compact">
              <div v-for="item in summaryItems(validation.current)" :key="`current-${item.label}`">
                <span>{{ item.label }}</span>
                <b>{{ item.value }}</b>
              </div>
            </div>
          </article>
          <article>
            <h3>{{ validation.current ? '备份内容' : '将恢复的备份' }}</h3>
            <div class="summary-grid compact">
              <div v-for="item in summaryItems(validation.summary)" :key="`backup-${item.label}`">
                <span>{{ item.label }}</span>
                <b>{{ item.value }}</b>
              </div>
            </div>
          </article>
        </div>

        <details class="tech-details">
          <summary>技术详情</summary>
          <div class="summary-grid compact">
            <div v-for="item in technicalSummaryItems(validation.summary)" :key="`tech-${item.label}`">
              <span>{{ item.label }}</span>
              <b>{{ item.value }}</b>
            </div>
          </div>
        </details>

        <ul v-if="validation.duplicateRisks.length" class="validation-list risks">
          <li v-for="risk in validation.duplicateRisks" :key="risk">{{ risk }}</li>
        </ul>

        <ul v-if="validation.warnings.length" class="validation-list warnings">
          <li v-for="warning in validation.warnings" :key="warning">{{ warning }}</li>
        </ul>

        <label v-if="validation.willReplace && validation.isValid" class="confirm-replace">
          <input v-model="confirmReplace" type="checkbox" />
          <span>我确认用这份备份替换当前账本；恢复失败不会修改当前账本。</span>
        </label>

        <LoadingButton
          v-if="validation.isValid"
          class="danger-button restore-button"
          :loading="isRestoring"
          :disabled="!canRestore"
          loading-label="正在恢复…"
          @click="restore"
        >
          {{ validation.willReplace ? '确认恢复并替换当前账本' : '确认恢复' }}
        </LoadingButton>
      </section>
    </template>

    <p class="page-footnote">
      恢复是替换而不是追加：收支、资产记录和计划期次按备份还原，不会重复记账；恢复前请先下载当前账本备份。
    </p>
  </main>
</template>

<style scoped>
.backup-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.header-spacer {
  width: 36px;
  flex-shrink: 0;
}

.backup-header h1 {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 600;
  letter-spacing: -0.4px;
}

.page-intro {
  margin: 14px 0 18px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  line-height: 1.8;
}

.backup-card {
  margin-top: 12px;
  padding: 14px;
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
}

.backup-card > header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
}

.backup-card > .form-row {
  margin-top: 10px;
  border: 1px solid var(--color-border-subtle);
  border-radius: 14px;
}

.backup-card > .form-row > .form-field {
  background: var(--color-bg-subtle);
}

.backup-card h2 {
  margin: 0;
  font-size: 0.875rem;
  font-weight: 600;
}

.backup-card header small {
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.backup-actions {
  display: flex;
  gap: 8px;
  margin-top: 12px;
}

.backup-actions button {
  flex: 1;
  min-height: var(--touch-target);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.helper-note,
.page-footnote {
  margin: 10px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.7;
}

.page-footnote {
  margin-top: 18px;
}

.file-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 12px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.file-field input {
  padding: 9px;
  border: 1px dashed var(--color-border-control);
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  font-size: var(--text-caption);
}

.file-name {
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.form-error {
  margin: 12px 0 0;
  padding: 10px 12px;
  border-radius: var(--radius-tag);
  background: var(--color-error-bg);
  color: var(--color-finance-loss);
  font-size: var(--text-footnote);
  line-height: 1.7;
}

.valid-card {
  border-color: var(--color-border-control);
}

.invalid-card {
  border-color: var(--color-finance-loss);
}

.validation-list {
  margin: 10px 0 0;
  padding-left: 16px;
  font-size: var(--text-caption);
  line-height: 1.7;
}

.validation-list.errors {
  color: var(--color-finance-loss);
}

.validation-list.risks {
  color: var(--color-finance-gain);
}

.validation-list.warnings {
  color: var(--color-text-secondary);
}

.summary-compare {
  display: grid;
  gap: 10px;
  margin-top: 12px;
}

.summary-compare article {
  min-width: 0;
}

.summary-compare h3 {
  margin: 0 0 8px;
  font-size: var(--text-caption);
  font-weight: 600;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
}

.summary-grid > div {
  min-width: 0;
  padding: 8px;
  border-radius: var(--radius-tag);
  background: var(--color-bg-subtle);
}

.summary-grid span {
  display: block;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.summary-grid b {
  display: block;
  margin-top: 3px;
  overflow: hidden;
  font-size: var(--text-caption);
  font-weight: 500;
  font-variant-numeric: tabular-nums;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.confirm-replace {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin-top: 14px;
  color: var(--color-text-primary);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.confirm-replace input {
  margin-top: 2px;
}

.restore-button {
  width: 100%;
  min-height: var(--touch-target);
  margin-top: 12px;
  font-size: var(--text-footnote);
}

.date-filter-details,
.tech-details {
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px solid var(--color-border-subtle);
}

.date-filter-details summary,
.tech-details summary {
  cursor: pointer;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 500;
}

.date-filter-details[open] > :not(summary),
.tech-details[open] > :not(summary) {
  animation: motion-collapse-in var(--motion-collapse-in) var(--ease-enter);
}

@media (prefers-reduced-motion: reduce) {
  .date-filter-details[open] > :not(summary),
  .tech-details[open] > :not(summary) {
    animation: none;
  }
}

.rollback-box {
  margin-top: 14px;
  padding: 12px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
}

.rollback-box .helper-note {
  margin-top: 0;
}

.clear-snapshot-btn {
  display: block;
  margin: 10px auto 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
}

.success-card .home-link {
  display: inline-block;
  margin-top: 12px;
  color: var(--color-brand);
  font-size: var(--text-caption);
  text-decoration: none;
}
</style>
