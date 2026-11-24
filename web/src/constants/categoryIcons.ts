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

export const categoryIconOptions = [
  ['1f3e0', '房屋'], ['1f6d2', '购物车'], ['1f6cd', '购物袋'], ['1f4e6', '包裹'],
  ['1f5d3', '日历'], ['1f37d', '餐具'], ['2615', '咖啡'], ['1f9cb', '奶茶'],
  ['1f35c', '面食'], ['1f370', '甜品'], ['1f34e', '水果'], ['1f37b', '酒水'],
  ['1f68c', '公交'], ['1f695', '打车'], ['1f687', '地铁'], ['1f697', '汽车'],
  ['26fd', '加油'], ['2708', '飞机'], ['1f3e8', '酒店'], ['1f9f3', '行李'],
  ['1f6cb', '沙发'], ['1f9f4', '日用品'], ['1f4a1', '电费'], ['1f4a7', '水费'],
  ['1f4f6', '信号'], ['1f4f1', '手机'], ['1f4bb', '电脑'], ['1f916', '机器人'],
  ['2728', 'AI'], ['1f9e0', '大脑'], ['1f9f9', '清洁'],
  ['1f527', '维修'], ['1f511', '钥匙'], ['1f331', '绿植'], ['1f455', '衣服'],
  ['1f45f', '运动鞋'], ['1f484', '美妆'], ['1f45c', '包包'], ['1f48e', '珠宝'],
  ['1f3ae', '游戏'], ['1f3ac', '电影'], ['1f3b5', '音乐'], ['1f4f7', '摄影'],
  ['1f3ab', '门票'], ['1f48a', '药品'], ['1f3e5', '医院'], ['1f9b7', '牙科'],
  ['1f6b2', '骑行'], ['1f43e', '宠物'], ['1f37c', '母婴'], ['1f4da', '书本'],
  ['1f393', '教育'], ['1f381', '礼物'], ['1f9e7', '红包'], ['1f490', '鲜花'],
  ['1f382', '蛋糕'], ['1f4b0', '钱袋'], ['1f4b5', '纸币'], ['1f4bc', '公文包'],
  ['1f4b3', '银行卡'], ['1f3e6', '银行'], ['1f4c8', '理财'], ['1f9fe', '票据'],
  ['1f6e1', '保险'], ['1f3c6', '奖杯'], ['1f3af', '目标'], ['1f3f7', '标签'],
  ['1f4c1', '文件夹']
] as const

export type CategoryTone = 'amber' | 'sky' | 'emerald' | 'rose' | 'violet'

const iconTones: Record<string, CategoryTone> = {
  '1f37d': 'amber',
  '2615': 'amber',
  '1f9cb': 'amber',
  '1f35c': 'amber',
  '1f37b': 'amber',
  '1f9f4': 'amber',
  '1f43e': 'amber',
  '1f6d2': 'amber',
  '1f695': 'amber',
  '1f4a1': 'amber',
  '1f9f9': 'amber',
  '1f511': 'amber',
  '1f3ab': 'amber',
  '1f4b3': 'amber',
  '1f3c6': 'amber',
  '2728': 'amber',
  '1f6cd': 'rose',
  '1f455': 'rose',
  '1f381': 'rose',
  '1f48a': 'rose',
  '1f370': 'rose',
  '1f34e': 'rose',
  '1f697': 'rose',
  '26fd': 'rose',
  '1f484': 'rose',
  '1f45c': 'rose',
  '1f3e5': 'rose',
  '1f9e7': 'rose',
  '1f490': 'rose',
  '1f382': 'rose',
  '1f3af': 'rose',
  '1f68c': 'sky',
  '1f687': 'sky',
  '2708': 'sky',
  '1f3e8': 'sky',
  '1f9f3': 'sky',
  '1f4f6': 'sky',
  '1f4a7': 'sky',
  '1f4f1': 'sky',
  '1f4bb': 'sky',
  '1f916': 'sky',
  '1f48e': 'sky',
  '1f9b7': 'sky',
  '1f37c': 'sky',
  '1f4da': 'sky',
  '1f393': 'sky',
  '1f6e1': 'sky',
  '1f3e0': 'emerald',
  '1f6cb': 'emerald',
  '1f45f': 'emerald',
  '1f6b2': 'emerald',
  '1f331': 'emerald',
  '1f4b0': 'emerald',
  '1f4b5': 'emerald',
  '1f4bc': 'emerald',
  '1f3e6': 'emerald',
  '1f4c8': 'emerald',
  '1f3ae': 'violet',
  '1f3ac': 'violet',
  '1f3b5': 'violet',
  '1f4f7': 'violet',
  '1f527': 'violet',
  '1f9fe': 'violet',
  '1f9e0': 'violet',
  '1f5d3': 'violet',
  '1f4e6': 'violet',
  '1f3f7': 'violet',
  '1f4c1': 'violet'
}

const iconAsset = (key: string) => assets[`../assets/category-icons/emoji_u${key}.svg`]

export function resolveCategoryIconKey(name: string, isGroup = false, iconKey?: string | null): string {
  if (iconKey && iconAsset(iconKey)) return iconKey
  return Object.hasOwn(categoryIcons, name) ? categoryIcons[name] : isGroup ? '1f4c1' : '1f3f7'
}

export function categoryIcon(name: string, isGroup = false, iconKey?: string | null): string {
  return iconAsset(resolveCategoryIconKey(name, isGroup, iconKey))!
}

export function categoryTone(name: string, isGroup = false, iconKey?: string | null): CategoryTone {
  const key = resolveCategoryIconKey(name, isGroup, iconKey)
  return iconTones[key] ?? 'violet'
}
