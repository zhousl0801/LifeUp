// Noto Emoji v2.051 artwork. License and attribution are shipped in /licenses/noto-emoji.txt.
const assets = import.meta.glob<string>('../assets/category-icons/*.svg', {
  eager: true,
  import: 'default',
  query: '?url'
})

const categoryIcons: Record<string, string> = {
  固定: '1f3e0',
  必要: '1f6d2',
  弹性: '1f6cd',
  其他: '1f4e6',
  房租: '1f3e0',
  订阅: '1f5d3',
  餐饮: '1f37d',
  日用: '1f9f4',
  购物: '1f6cd',
  交通: '1f68c',
  居家: '1f6cb',
  服饰: '1f455',
  娱乐: '1f3ae',
  通讯: '1f4f6',
  医疗: '1f48a',
  学习: '1f4da',
  运动: '1f45f',
  旅行: '1f9f3',
  宠物: '1f43e',
  人情: '1f381',
  工资: '1f4bc',
  奖金: '1f381',
  经常收入: '1f4b0',
  其他收入: '1f4b5'
}

export function categoryIcon(name: string, isGroup = false): string {
  const icon = Object.hasOwn(categoryIcons, name) ? categoryIcons[name] : isGroup ? '1f4c1' : '1f3f7'
  return assets[`../assets/category-icons/emoji_u${icon}.svg`]!
}
