import { toast } from '../composables/useMessage'
import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, expect, test, vi } from 'vitest'
import { ApiError } from '../api/client'
import BackupView from './BackupView.vue'

const mocks = vi.hoisted(() => ({
  validate: vi.fn(),
  restore: vi.fn(),
  fetchSnapshot: vi.fn(),
  downloadBackup: vi.fn(),
  downloadDetails: vi.fn()
}))

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))
vi.mock('../api/backup', () => ({
  validateBackup: mocks.validate,
  restoreBackup: mocks.restore,
  fetchBackupSnapshot: mocks.fetchSnapshot,
  downloadBackup: mocks.downloadBackup,
  downloadDetails: mocks.downloadDetails
}))

const summary = {
  ledgerId: 'ledger-1',
  startDate: '2026-10-01',
  cashBalance: 10850,
  providentBalance: 500,
  fundValue: 1100,
  fundPending: 300,
  fundPendingArrival: 0,
  goldValue: 9000,
  goldPendingArrival: 0,
  stockValue: 12500,
  totalAssets: null,
  isComplete: false,
  entryCount: 2,
  assetTransactionCount: 4,
  planCount: 1,
  planOccurrenceCount: 2,
  mutationReceiptCount: 3,
  priceObservationCount: 2
}

const validValidation = {
  isValid: true,
  willReplace: true,
  validationToken: 'token-1',
  summary,
  current: { ...summary, entryCount: 1, assetTransactionCount: 1 },
  errors: [],
  warnings: ['业务重放提示：部分基金持仓日期或行情依据不足。'],
  duplicateRisks: ['恢复将替换当前账本：1 条收支、1 条资产记录、1 个计划、1 个执行期次。']
}

beforeEach(() => {
  vi.clearAllMocks()
  sessionStorage.clear()
  mocks.validate.mockResolvedValue(structuredClone(validValidation))
  mocks.restore.mockResolvedValue({
    restored: true,
    replacedExisting: true,
    summary,
    restoredAt: '2026-10-22T10:00:00Z',
    warnings: []
  })
  mocks.fetchSnapshot.mockResolvedValue({ format: 'lifeup-backup', previous: true })
  mocks.downloadBackup.mockResolvedValue(undefined)
  mocks.downloadDetails.mockResolvedValue(undefined)
  document.body.innerHTML = ''
})

test('export actions show progress on the active button until the download completes', async () => {
  let finishDownload!: () => void
  mocks.downloadDetails.mockImplementationOnce(() => new Promise<void>(resolve => { finishDownload = resolve }))
  mocks.downloadBackup.mockImplementationOnce(() => new Promise<void>(resolve => { finishDownload = resolve }))
  const view = mount(BackupView)
  const [backup, details] = view.findAll('.backup-actions button')

  await details!.trigger('click')
  expect(mocks.downloadDetails).toHaveBeenCalledWith('', '')
  expect(details!.text()).toBe('导出中…')
  expect(details!.attributes('aria-busy')).toBe('true')
  expect(backup!.attributes('disabled')).toBeDefined()
  finishDownload()
  await flushPromises()
  expect(details!.text()).toBe('导出明细 CSV')

  await backup!.trigger('click')
  expect(mocks.downloadBackup).toHaveBeenCalledTimes(1)
  expect(backup!.text()).toBe('下载中…')
  expect(backup!.attributes('aria-busy')).toBe('true')
  expect(details!.attributes('disabled')).toBeDefined()
  finishDownload()
  await flushPromises()
  expect(backup!.text()).toBe('下载完整备份')
  view.unmount()
})

test('valid backup shows both summaries and requires confirmation before replacing', async () => {
  const view = mount(BackupView, { attachTo: document.body })
  await flushPromises()
  await selectBackup(view, '{"format":"lifeup-backup"}')

  expect(mocks.validate).toHaveBeenCalledTimes(1)
  expect(view.text()).toContain('校验通过')
  expect(view.text()).toContain('当前账本')
  expect(view.text()).toContain('备份内容')
  expect(view.text()).toContain('恢复将替换当前账本')
  expect(view.text()).toContain('执行期次')
  expect(view.text()).toContain('2 个')
  expect(view.text()).toContain('股票')
  expect(view.text()).toContain('¥12,500.00')

  const restoreButton = view.findAll('button').find((button) => button.text() === '确认恢复并替换当前账本')!
  expect(restoreButton.attributes('disabled')).toBeDefined()

  await view.find('.confirm-replace input').setValue(true)
  await restoreButton.trigger('click')
  await flushPromises()

  expect(mocks.restore).toHaveBeenCalledWith(
    { format: 'lifeup-backup' },
    'token-1',
    true
  )
  expect(view.text()).toContain('恢复完成')
  expect(view.text()).toContain('返回总览核对余额')
  view.unmount()
})

test('invalid backup shows errors and offers no restore button', async () => {
  mocks.validate.mockResolvedValue({
    ...structuredClone(validValidation),
    isValid: false,
    validationToken: null,
    errors: ['备份缺少账本或现金账户。'],
    current: null,
    willReplace: false
  })
  const view = mount(BackupView, { attachTo: document.body })
  await flushPromises()
  await selectBackup(view, '{"format":"bad"}')

  expect(view.text()).toContain('校验未通过')
  expect(view.text()).toContain('备份缺少账本或现金账户。')
  expect(view.findAll('button').some((button) => button.text().includes('确认恢复'))).toBe(false)
  expect(mocks.restore).not.toHaveBeenCalled()
  view.unmount()
})

test('restore failure keeps the validation and shows the error', async () => {
  const errorToast = vi.spyOn(toast, 'error')
  mocks.restore.mockRejectedValue(new Error('备份或当前账本已变化，请重新校验后再恢复。'))
  const view = mount(BackupView, { attachTo: document.body })
  await flushPromises()
  await selectBackup(view, '{"format":"lifeup-backup"}')
  await view.find('.confirm-replace input').setValue(true)
  await view.findAll('button').find((button) => button.text() === '确认恢复并替换当前账本')!.trigger('click')
  await flushPromises()

  expect(errorToast).toHaveBeenCalledWith('备份或当前账本已变化，请重新校验后再恢复。')
  expect(view.text()).toContain('校验通过')
  expect(view.text()).not.toContain('替换前的账本快照')
  view.unmount()
})

test('previous snapshot persists across remount and when restore result is unknown (status 0)', async () => {
  const errorToast = vi.spyOn(toast, 'error')
  mocks.restore.mockRejectedValueOnce(new ApiError(0, '网络超时'))
  const view = mount(BackupView, { attachTo: document.body })
  await flushPromises()
  await selectBackup(view, '{"format":"lifeup-backup"}')
  await view.find('.confirm-replace input').setValue(true)
  await view.findAll('button').find((button) => button.text() === '确认恢复并替换当前账本')!.trigger('click')
  await flushPromises()

  expect(errorToast).toHaveBeenCalledWith(expect.stringContaining('暂时无法确认恢复结果'))
  expect(view.text()).toContain('替换前的账本快照')
  view.unmount()

  const remounted = mount(BackupView, { attachTo: document.body })
  await flushPromises()
  expect(remounted.text()).toContain('替换前的账本快照')
  expect(remounted.text()).toContain('取回替换前的账本')
  remounted.unmount()
})

async function selectBackup(view: ReturnType<typeof mount>, content: string): Promise<void> {
  const file = new File([content], 'backup.json', { type: 'application/json' })
  Object.defineProperty(file, 'text', { value: async () => content })
  const input = view.find('input[type="file"]')
  Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
  await input.trigger('change')
  await flushPromises()
}
