import { resolvedTheme } from './theme'

/**
 * ECharts 不能直接读取 CSS 变量，图表色集中在这里维护。
 * 浅色 / 深色图表的色值与 docs/design/ui-color-tokens.css 的语义值对应。
 */
const light = {
  brand: '#5B5BD7',
  gain: '#D94A4A',
  loss: '#2E9E75',
  text: '#6F6A7D',
  axis: '#E4E1EB',
  grid: '#EFECF5',
  pointer: '#B8B3C6',
  marker: '#6F6A7D',
  areaStrong: 'rgba(91, 91, 215, 0.18)',
  areaSoft: 'rgba(91, 91, 215, 0.01)',
  pointerShadow: 'rgba(30, 27, 56, 0.06)'
} as const

const dark = {
  brand: '#8B86FF',
  gain: '#FF7A7A',
  loss: '#4ED39B',
  text: '#A9A6C5',
  axis: 'rgba(169, 166, 197, 0.16)',
  grid: 'rgba(255, 255, 255, 0.08)',
  pointer: '#8F8BAC',
  marker: '#A9A6C5',
  areaStrong: 'rgba(139, 134, 255, 0.24)',
  areaSoft: 'rgba(139, 134, 255, 0.02)',
  pointerShadow: 'rgba(255, 255, 255, 0.04)'
} as const

const hero = {
  brand: '#7CE7B1',
  gain: '#FF8A8A',
  loss: '#4ED39B',
  text: '#A9A6C5',
  axis: 'transparent',
  grid: 'transparent',
  pointer: '#A9A6C5',
  marker: '#F7F5FF',
  areaStrong: 'rgba(124, 231, 177, 0.30)',
  areaSoft: 'rgba(124, 231, 177, 0.01)',
  pointerShadow: 'rgba(0, 0, 0, 0.22)'
} as const

// 浅色主题的 Hero 是白日紫：底色更亮更紫，指针和文字需要更亮，曲线仍用薄荷绿，
// 面积改用手白光，避免薄荷绿叠在紫色上发灰。
const lightHero = {
  ...hero,
  text: '#F1EDFF',
  pointer: '#F1EDFF',
  areaStrong: 'rgba(255, 255, 255, 0.16)',
  areaSoft: 'rgba(255, 255, 255, 0.01)',
  pointerShadow: 'rgba(30, 24, 90, 0.20)'
} as const

export type ChartThemeVariant = 'default' | 'hero'

export function chartTheme(variant: ChartThemeVariant = 'default') {
  if (variant === 'hero') {
    return resolvedTheme.value === 'dark' ? hero : lightHero
  }

  return resolvedTheme.value === 'dark' ? dark : light
}

/** 跟随浏览器根字号缩放图表文字，接近 Dynamic Type 的效果。 */
export function chartFontSize(px = 12): number {
  if (typeof window === 'undefined') {
    return px
  }

  const root = Number.parseFloat(getComputedStyle(document.documentElement).fontSize)
  return root > 0 ? Math.round(px * root / 16) : px
}
