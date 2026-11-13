<script setup lang="ts">
import { computed, ref } from 'vue'
import AppIcon from '../components/AppIcon.vue'
import AppNav from '../components/AppNav.vue'
import BottomSheet from '../components/BottomSheet.vue'
import DefaultAvatar from '../components/DefaultAvatar.vue'
import { fileToAvatar, loadProfile, normalizeProfileName, saveProfile, type UserProfile } from '../utils/profile'
import { useTheme, type ThemeMode } from '../utils/theme'

const profile = ref<UserProfile>(loadProfile())
const profileSheetOpen = ref(false)
const draftName = ref('')
const draftAvatar = ref<string | null>(null)
const profileError = ref('')
const avatarBusy = ref(false)
const avatarInput = ref<HTMLInputElement | null>(null)
const themeSheetOpen = ref(false)
const { mode, setThemeMode } = useTheme()
const themeOptions: { value: ThemeMode; label: string; description: string }[] = [
  { value: 'system', label: '跟随系统', description: '随设备的亮色 / 暗色外观自动切换' },
  { value: 'light', label: '亮色', description: '始终使用浅色界面' },
  { value: 'dark', label: '暗色', description: '始终使用深色界面' }
]
const themeLabel = computed(() =>
  themeOptions.find((option) => option.value === mode.value)?.label ?? '跟随系统')
const displayName = computed(() => profile.value.name || 'LifeUp')

function openProfileSheet(): void {
  draftName.value = profile.value.name
  draftAvatar.value = profile.value.avatar
  profileError.value = ''
  profileSheetOpen.value = true
}

function chooseAvatar(): void {
  if (!avatarBusy.value) {
    avatarInput.value?.click()
  }
}

async function onAvatarChange(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''

  if (!file) {
    return
  }

  avatarBusy.value = true
  profileError.value = ''

  try {
    draftAvatar.value = await fileToAvatar(file)
  } catch (error) {
    profileError.value = error instanceof Error ? error.message : '头像处理失败，请重试。'
  } finally {
    avatarBusy.value = false
  }
}

function saveProfileChanges(): void {
  const next: UserProfile = {
    name: normalizeProfileName(draftName.value),
    avatar: draftAvatar.value
  }

  if (!saveProfile(next)) {
    profileError.value = '保存失败，浏览器存储空间可能不足。'
    return
  }

  profile.value = next
  profileSheetOpen.value = false
}

function chooseTheme(value: ThemeMode): void {
  setThemeMode(value)
}
</script>

<template>
  <main class="page mine-page with-nav">
    <header class="mine-header">
      <h1>我的</h1>
    </header>

    <h2 class="settings-title">个性化设置</h2>

    <div class="list-box">
      <button type="button" class="list-row" @click="openProfileSheet">
        <span>头像与昵称</span>
        <span class="list-value">
          <span class="avatar avatar-row">
            <img v-if="profile.avatar" :src="profile.avatar" alt="" />
            <DefaultAvatar v-else />
          </span>
          <b>{{ displayName }} ›</b>
        </span>
      </button>
      <button type="button" class="list-row" @click="themeSheetOpen = true">
        <span>主题配色</span>
        <b>{{ themeLabel }} ›</b>
      </button>
    </div>

    <h2 class="settings-title data-title">数据管理</h2>
    <div class="list-box">
      <RouterLink to="/backup" class="list-row"><span>数据导出与备份</span><b aria-hidden="true">›</b></RouterLink>
    </div>

    <BottomSheet
      :open="profileSheetOpen"
      title="头像与昵称"
      subtitle="只保存在当前设备，不会同步到其他设备"
      @close="profileSheetOpen = false"
    >
      <div class="profile-editor">
        <div class="avatar-editor">
          <span class="avatar avatar-large">
            <img v-if="draftAvatar" :src="draftAvatar" alt="头像预览" />
            <DefaultAvatar v-else />
          </span>
          <span class="avatar-actions">
            <button type="button" class="link-action" :disabled="avatarBusy" @click="chooseAvatar">
              {{ draftAvatar ? '更换头像' : '上传头像' }}
            </button>
            <button v-if="draftAvatar" type="button" class="link-action muted" :disabled="avatarBusy" @click="draftAvatar = null">
              移除头像
            </button>
          </span>
          <input ref="avatarInput" class="file-input" type="file" accept="image/*" @change="onAvatarChange" />
        </div>

        <label class="profile-field">
          <span>昵称</span>
          <input v-model="draftName" type="text" maxlength="20" placeholder="输入昵称" />
        </label>

        <p v-if="profileError" class="profile-error" role="alert">{{ profileError }}</p>
        <p class="profile-note">支持 JPG、PNG 等图片，头像会压缩后保存在本机。</p>

        <button type="button" class="primary-button profile-save" :disabled="avatarBusy" @click="saveProfileChanges">
          保存
        </button>
      </div>
    </BottomSheet>

    <BottomSheet
      :open="themeSheetOpen"
      title="主题配色"
      subtitle="选择亮色、暗色，或跟随设备外观"
      @close="themeSheetOpen = false"
    >
      <div class="theme-options" role="radiogroup" aria-label="主题配色">
        <button
          v-for="option in themeOptions"
          :key="option.value"
          type="button"
          role="radio"
          class="theme-option"
          :class="{ active: mode === option.value }"
          :aria-checked="mode === option.value"
          @click="chooseTheme(option.value)"
        >
          <span class="theme-option-copy">
            <strong>{{ option.label }}</strong>
            <small>{{ option.description }}</small>
          </span>
          <span class="theme-check" :class="{ checked: mode === option.value }">
            <AppIcon v-if="mode === option.value" name="check" />
          </span>
        </button>
      </div>
      <p class="theme-note">主题选择只保存在当前设备，不会改变账本数据。</p>
    </BottomSheet>

    <AppNav />
  </main>
</template>

<style scoped>
.mine-header {
  margin: 4px 0 22px;
}

.mine-header h1 {
  margin: 0;
  font-size: var(--text-title1);
  font-weight: 700;
  letter-spacing: -0.6px;
}

.settings-title {
  margin: 0 0 8px 4px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 600;
}

.settings-title.data-title {
  margin-top: var(--space-6);
}

.list-box {
  overflow: hidden;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.list-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  box-sizing: border-box;
  width: 100%;
  min-height: 60px;
  padding: 0 18px;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  font-size: var(--text-subhead);
  font-weight: 500;
  text-align: left;
  text-decoration: none;
}

.list-row + .list-row {
  border-top: 1px solid var(--color-border-subtle);
}

.list-row:hover {
  background: var(--color-bg-hover);
}

.list-row b {
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
  font-weight: 500;
}

.list-value {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.list-value b {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.avatar {
  display: inline-grid;
  flex: 0 0 auto;
  overflow: hidden;
  place-items: center;
  border-radius: 50%;
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-row {
  width: 28px;
  height: 28px;
}

.avatar-large {
  width: 76px;
  height: 76px;
}

.profile-editor {
  display: grid;
  gap: 16px;
}

.avatar-editor {
  display: flex;
  align-items: center;
  gap: 16px;
}

.avatar-actions {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
}

.link-action {
  min-height: 32px;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-size: var(--text-footnote);
  font-weight: 600;
}

.link-action.muted {
  color: var(--color-text-secondary);
  font-weight: 500;
}

.file-input {
  display: none;
}

.profile-field {
  display: grid;
  gap: 8px;
  color: var(--color-text-secondary);
  font-size: var(--text-footnote);
}

.profile-field input {
  box-sizing: border-box;
  width: 100%;
  min-height: 48px;
  padding: 0 13px;
  border: 1px solid var(--color-border-control);
  border-radius: var(--radius-control);
  background: var(--color-bg-page);
  color: var(--color-text-primary);
  font-size: var(--text-body);
}

.profile-error {
  margin: 0;
  color: var(--color-error);
  font-size: var(--text-footnote);
  line-height: 1.6;
}

.profile-note {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.6;
}

.profile-save {
  width: 100%;
}

.theme-options {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.theme-option {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 14px 16px;
  border: 0;
  border-radius: 16px;
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  text-align: left;
}

.theme-option.active {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.theme-option-copy {
  flex: 1;
  min-width: 0;
}

.theme-option-copy strong {
  display: block;
  font-size: 0.875rem;
  font-weight: 600;
}

.theme-option-copy small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.5;
}

.theme-check {
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  width: 22px;
  height: 22px;
  border: 1.5px solid var(--color-border-default);
  border-radius: 50%;
  color: var(--color-on-brand);
}

.theme-check.checked {
  border-color: var(--color-brand);
  background: var(--color-brand);
}

.theme-check .app-icon {
  width: 14px;
  height: 14px;
}

.theme-note {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.6;
}
</style>
