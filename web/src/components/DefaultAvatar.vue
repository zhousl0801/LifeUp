<script setup lang="ts">
import { useId } from 'vue'

const gradientId = `lifeup-avatar-${useId()}`
</script>

<template>
  <svg class="default-avatar" viewBox="0 0 64 64" aria-hidden="true" focusable="false">
    <defs>
      <linearGradient :id="gradientId" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stop-color="#6C5CE7" />
        <stop offset="0.55" stop-color="#5B8CFF" />
        <stop offset="1" stop-color="#3EE6C1" />
      </linearGradient>
    </defs>
    <rect width="64" height="64" :fill="`url(#${gradientId})`" />
    <circle cx="32" cy="32" r="19" fill="none" stroke="#FFFFFF" stroke-opacity="0.3" stroke-width="1.5" />
    <path d="M32 13 47 24 40 45 24 45 17 24Z" fill="#FFFFFF" fill-opacity="0.9" />
    <path d="M32 13 40 45 24 45Z" fill="#FFFFFF" fill-opacity="0.24" />
    <path d="M17 24 32 31 47 24" fill="none" stroke="#FFFFFF" stroke-opacity="0.7" stroke-width="1.5" stroke-linejoin="round" />
    <circle cx="47.5" cy="17.5" r="3.5" fill="#B9FFE4" />
    <circle cx="16.5" cy="46" r="2" fill="#FFFFFF" fill-opacity="0.75" />
  </svg>
</template>

<style scoped>
.default-avatar {
  display: block;
  width: 100%;
  height: 100%;
}
</style>
