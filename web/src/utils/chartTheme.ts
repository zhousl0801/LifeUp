import { resolvedTheme } from './theme'

/**
 * ECharts 不能直接读取 CSS 变量，图表色集中在这里维护。
 * 浅色 / 深色图表的色值与 docs/design/ui-color-tokens.css 的语义值对应。
 */
const light = {
  brand: '#4F46E5',
  gain: '#D94A4A',
  loss: '#0F766E',
  text: '#525B6E',
  axis: '#E2E7F0',
  grid: '#E5E9F2',
  pointer: '#94A3B8',
  marker: '#525B6E',
  areaStrong: 'rgba(79, 70, 229, 0.18)',
  areaSoft: 'rgba(79, 70, 229, 0.01)',
  pointerShadow: 'rgba(15, 23, 42, 0.06)'
} as const

const dark = {
  brand: '#818CF8',
  gain: '#FF7A7A',
  loss: '#34D399',
  text: '#B4BCD0',
  axis: 'rgba(226, 232, 240, 0.16)',
  grid: 'rgba(226, 232, 240, 0.08)',
  pointer: '#8E98B0',
  marker: '#B4BCD0',
  areaStrong: 'rgba(129, 140, 248, 0.24)',
  areaSoft: 'rgba(129, 140, 248, 0.02)',
  pointerShadow: 'rgba(255, 255, 255, 0.04)'
} as const

const hero = {
  brand: '#7CE7B1',
  gain: '#FF9898',
  loss: '#34D399',
  text: '#CBD5E1',
  axis: 'transparent',
  grid: 'transparent',
  pointer: '#CBD5E1',
  marker: '#F8FAFC',
  areaStrong: 'rgba(124, 231, 177, 0.30)',
  areaSoft: 'rgba(124, 231, 177, 0.01)',
  pointerShadow: 'rgba(0, 0, 0, 0.24)'
} as const

// 浅色主题的 Hero 是晴空极光紫：底色更亮，指针和文字需要更亮，曲线仍用薄荷绿，
// 面积改用柔白光，避免薄荷绿叠在靛紫色上发灰。
const lightHero = {
  ...hero,
  text: '#EEF2FF',
  pointer: '#EEF2FF',
  areaStrong: 'rgba(255, 255, 255, 0.18)',
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
