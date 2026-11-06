type ChartRuntime = typeof import('echarts/core')

let runtime: Promise<ChartRuntime> | null = null

/** 图表运行时按需加载，避免 ECharts 进入首屏包体。 */
export function loadChartRuntime(): Promise<ChartRuntime> {
  runtime ??= import('./chartEcharts').then((module) => module.default)

  return runtime
}
