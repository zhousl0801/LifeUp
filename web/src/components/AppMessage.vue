<script setup lang="ts">
import { Toaster } from 'vue-sonner'
import 'vue-sonner/style.css'
import { useTheme } from '../utils/theme'

const { resolvedTheme } = useTheme()
</script>

<template>
  <Toaster
    class="lifeup-toaster"
    :theme="resolvedTheme"
    position="top-center"
    :visible-toasts="3"
    :gap="8"
    :offset="{ top: 'calc(env(safe-area-inset-top) + 12px)' }"
    :mobile-offset="{ top: 12, left: 16, right: 16 }"
    rich-colors
    :toast-options="{ class: 'lifeup-toast' }"
  />
</template>

<style>
[data-sonner-toaster].lifeup-toaster,
[data-sonner-toaster].lifeup-toaster[data-sonner-theme] {
  z-index: 70;
  font-family: inherit;
  --border-radius: 14px;
  --width: 320px;

  --normal-bg: var(--color-bg-popover);
  --normal-border: var(--color-border-subtle);
  --normal-text: var(--color-text-primary);

  --success-bg: var(--color-success-bg);
  --success-border: color-mix(in srgb, var(--color-success) 26%, transparent);
  --success-text: var(--color-success);

  --info-bg: var(--color-info-bg);
  --info-border: color-mix(in srgb, var(--color-info) 26%, transparent);
  --info-text: var(--color-info);

  --warning-bg: var(--color-warning-bg);
  --warning-border: color-mix(in srgb, var(--color-warning) 28%, transparent);
  --warning-text: var(--color-warning);

  --error-bg: var(--color-error-bg);
  --error-border: color-mix(in srgb, var(--color-error) 28%, transparent);
  --error-text: var(--color-error);
}

[data-sonner-toaster].lifeup-toaster [data-sonner-toast].lifeup-toast {
  padding: 11px 14px;
  gap: 8px;
  font-size: 0.8125rem;
  box-shadow: 0 10px 28px var(--color-shadow-strong);
}

[data-sonner-toaster].lifeup-toaster [data-sonner-toast].lifeup-toast [data-title] {
  font-weight: 550;
  line-height: 1.4;
}

@media (max-width: 600px) {
  [data-sonner-toaster].lifeup-toaster[data-x-position='center'] {
    left: 50%;
    right: auto;
    width: min(calc(100vw - 32px), 340px);
    transform: translateX(-50%);
  }

  [data-sonner-toaster].lifeup-toaster [data-sonner-toast] {
    width: 100%;
  }
}
</style>
