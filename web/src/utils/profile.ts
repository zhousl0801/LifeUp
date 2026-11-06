export interface UserProfile {
  name: string
  avatar: string | null
}

export const profileStorageKey = 'asset-keeper.profile'
export const profileNameLimit = 20
const avatarSize = 256
const avatarMaxSourceBytes = 10 * 1024 * 1024

export function normalizeProfileName(value: unknown): string {
  return typeof value === 'string' ? value.trim().slice(0, profileNameLimit) : ''
}

function normalizeAvatar(value: unknown): string | null {
  return typeof value === 'string' && value.startsWith('data:image/') ? value : null
}

export function loadProfile(): UserProfile {
  if (typeof window === 'undefined') {
    return { name: '', avatar: null }
  }

  try {
    const stored = window.localStorage.getItem(profileStorageKey)

    if (stored) {
      const parsed = JSON.parse(stored) as Partial<UserProfile>
      return {
        name: normalizeProfileName(parsed.name),
        avatar: normalizeAvatar(parsed.avatar)
      }
    }
  } catch {
    // 存储不可用或内容损坏时按未设置资料处理。
  }

  return { name: '', avatar: null }
}

export function saveProfile(profile: UserProfile): boolean {
  const next = {
    name: normalizeProfileName(profile.name),
    avatar: normalizeAvatar(profile.avatar)
  }

  try {
    window.localStorage.setItem(profileStorageKey, JSON.stringify(next))
    return true
  } catch {
    return false
  }
}

export async function fileToAvatar(file: File): Promise<string> {
  if (!file.type.startsWith('image/')) {
    throw new Error('请选择图片文件。')
  }

  if (file.size > avatarMaxSourceBytes) {
    throw new Error('图片不能超过 10 MB。')
  }

  const image = await loadImage(file)
  const canvas = document.createElement('canvas')
  canvas.width = avatarSize
  canvas.height = avatarSize

  const context = canvas.getContext('2d')

  if (!context) {
    throw new Error('当前浏览器无法处理图片。')
  }

  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, avatarSize, avatarSize)

  const scale = Math.max(avatarSize / image.naturalWidth, avatarSize / image.naturalHeight)
  const width = image.naturalWidth * scale
  const height = image.naturalHeight * scale

  context.drawImage(image, (avatarSize - width) / 2, (avatarSize - height) / 2, width, height)

  return canvas.toDataURL('image/jpeg', 0.85)
}

function loadImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const image = new Image()

    image.onload = () => {
      URL.revokeObjectURL(url)
      resolve(image)
    }
    image.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new Error('图片读取失败，请重试。'))
    }
    image.src = url
  })
}
