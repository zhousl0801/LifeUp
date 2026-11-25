import { onBeforeUnmount, onMounted, watch, type Ref, type WatchSource } from 'vue'
import type { ECharts } from 'echarts/core'
import type { EChartsOption } from 'echarts'
import { loadChartRuntime } from '../utils/chartRuntime'
import { resolvedTheme } from '../utils/theme'
import { useReducedMotion } from './useReducedMotion'

export interface UseEChartOptions {
  watchSources?: WatchSource | WatchSource[]
  deep?: boolean
  flush?: 'pre' | 'post' | 'sync'
  resizeOnUpdate?: boolean
  setOptionOpts?: { notMerge?: boolean; replaceMerge?: string | string[] }
  onInit?: (chart: ECharts) => void
}

export function useEChart(
  container: Readonly<Ref<HTMLElement | null>>,
  buildOption: () => EChartsOption,
  options: UseEChartOptions = {}
) {
  let chart: ECharts | null = null
  let disposed = false
  let resizeObserver: ResizeObserver | null = null
  const reducedMotion = useReducedMotion()

  function resize(): void {
    chart?.resize()
  }

  function render(): void {
    chart?.setOption(buildOption(), options.setOptionOpts ?? { notMerge: true })
  }

  onMounted(() => {
    window.addEventListener('resize', resize)
    if (typeof ResizeObserver !== 'undefined' && container.value) {
      resizeObserver = new ResizeObserver(() => resize())
      resizeObserver.observe(container.value)
    }

    void loadChartRuntime()
      .then((echarts) => {
        if (disposed || !container.value) {
          return
        }

        chart = echarts.init(container.value, undefined, { renderer: 'canvas' })
        render()
        options.onInit?.(chart)
      })
      .catch(() => {
        // Runtime chunk failure resets cache in chartRuntime; keep container inert.
      })
  })

  if (options.watchSources) {
    watch(
      options.watchSources,
      () => {
        if (options.resizeOnUpdate) {
          resize()
        }
        render()
      },
      { deep: options.deep, flush: options.flush }
    )
  }

  watch([resolvedTheme, reducedMotion], () => render())

  onBeforeUnmount(() => {
    disposed = true
    window.removeEventListener('resize', resize)
    resizeObserver?.disconnect()
    resizeObserver = null
    chart?.dispose()
    chart = null
  })

  return { render, resize }
}
