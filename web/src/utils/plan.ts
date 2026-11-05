import type { AutoPlan } from '../types/plan'

/**
 * 基金定投统一展示基金本身名称，而不是历史计划名。
 * 新计划默认名带「基金定投 ·」前缀，旧数据可能带「每交易日定投」后缀；
 * 缺少关联持仓时才回退到清理过前缀/后缀的计划名。
 */
export function fundPlanDisplayName(plan: AutoPlan): string {
  const targetName = plan.fundTarget?.name?.trim()

  if (targetName) {
    return targetName
  }

  const positionName = plan.fundPositionName?.trim()

  if (positionName) {
    return positionName
  }

  return plan.name
    .replace(/^基金定投\s*[·•]\s*/, '')
    .replace(/\s*[·•]?\s*(?:每)?交易日定投\s*$/, '')
    .replace(/\s*[·•]?\s*定投\s*$/, '')
    .trim()
}
