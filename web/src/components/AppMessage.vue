<script setup lang="ts">
import { useMessages, type MessageTone } from '../composables/useMessage'

const { messages, dismiss } = useMessages()

const toneLabel: Record<MessageTone, string> = {
  success: '已完成',
  error: '失败',
  info: '提示'
}
</script>

<template>
  <Teleport to="body">
    <div class="message-stack" aria-live="polite" aria-atomic="false">
      <TransitionGroup name="message">
        <article
          v-for="item in messages"
          :key="item.id"
          class="message-card"
          :class="`is-${item.tone}`"
          :role="item.tone === 'error' ? 'alert' : 'status'"
        >
          <span class="message-icon" aria-hidden="true">
            <svg v-if="item.tone === 'success'" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <path d="m5 12.5 4.2 4.2L19 6.8" />
            </svg>
            <svg v-else-if="item.tone === 'error'" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              stroke-width="2.2" stroke-linecap="round">
              <path d="M12 7v6.5" />
              <circle cx="12" cy="17.2" r="1.2" fill="currentColor" stroke="none" />
            </svg>
            <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"
              stroke-linecap="round">
              <path d="M12 7.5v.01M12 11v6" />
            </svg>
          </span>

          <div class="message-copy">
            <span class="message-kicker">{{ toneLabel[item.tone] }}</span>
            <p class="message-text">{{ item.text }}</p>
            <button
              v-if="item.action"
              type="button"
              class="message-action"
              @click="item.action.run(); dismiss(item.id)"
            >
              {{ item.action.label }}
            </button>
          </div>

          <button type="button" class="message-close" aria-label="关闭提示" @click="dismiss(item.id)">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"
              aria-hidden="true">
              <path d="M6.5 6.5l11 11M17.5 6.5l-11 11" />
            </svg>
          </button>

          <span
            v-if="item.duration > 0"
            class="message-progress"
            :style="{ animationDuration: `${item.duration}ms` }"
            aria-hidden="true"
          />
        </article>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<style scoped>
.message-stack {
  position: fixed;
  top: calc(env(safe-area-inset-top) + 10px);
  right: 0;
  left: 0;
  z-index: 70;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  box-sizing: border-box;
  width: 100%;
  padding: 0 12px;
  pointer-events: none;
}

.message-card {
  --message-accent: #8b86ff;
  --message-accent-soft: rgba(139, 134, 255, 0.16);
  --message-accent-line: rgba(139, 134, 255, 0.42);
  --message-accent-glow: rgba(139, 134, 255, 0.28);
  position: relative;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 10px;
  align-items: start;
  overflow: hidden;
  box-sizing: border-box;
  width: min(100%, 430px);
  padding: 11px 12px 12px;
  border: 1px solid rgba(255, 255, 255, 0.13);
  border-radius: 16px;
  background:
    linear-gradient(135deg, var(--message-accent-soft), rgba(10, 14, 22, 0) 58%),
    rgba(10, 13, 20, 0.93);
  box-shadow:
    0 18px 42px rgba(2, 5, 12, 0.46),
    inset 0 1px 0 rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(18px) saturate(140%);
  color: #f4f7fb;
  pointer-events: auto;
}

.message-card.is-success {
  --message-accent: #3ee6a6;
  --message-accent-soft: rgba(62, 230, 166, 0.15);
  --message-accent-line: rgba(62, 230, 166, 0.4);
  --message-accent-glow: rgba(62, 230, 166, 0.26);
}

.message-card.is-error {
  --message-accent: #ff6b81;
  --message-accent-soft: rgba(255, 107, 129, 0.16);
  --message-accent-line: rgba(255, 107, 129, 0.44);
  --message-accent-glow: rgba(255, 107, 129, 0.28);
}

:global(html[data-color-scheme='light'] .message-card) {
  border-color: rgba(17, 24, 39, 0.08);
  background:
    linear-gradient(135deg, var(--message-accent-soft), rgba(255, 255, 255, 0) 58%),
    rgba(255, 255, 255, 0.91);
  box-shadow:
    0 16px 38px rgba(18, 22, 34, 0.14),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
  color: #111827;
}

:global(html[data-color-scheme='light'] .message-card.is-success) {
  --message-accent: #0f9f6e;
  --message-accent-soft: rgba(15, 159, 110, 0.1);
  --message-accent-line: rgba(15, 159, 110, 0.3);
  --message-accent-glow: rgba(15, 159, 110, 0.16);
}

:global(html[data-color-scheme='light'] .message-card.is-error) {
  --message-accent: #d93a4a;
  --message-accent-soft: rgba(217, 58, 74, 0.1);
  --message-accent-line: rgba(217, 58, 74, 0.32);
  --message-accent-glow: rgba(217, 58, 74, 0.16);
}

:global(html[data-color-scheme='light'] .message-card.is-info) {
  --message-accent: #6c5ce7;
  --message-accent-soft: rgba(108, 92, 231, 0.1);
  --message-accent-line: rgba(108, 92, 231, 0.3);
  --message-accent-glow: rgba(108, 92, 231, 0.16);
}

:global(html[data-color-scheme='light'] .message-text) {
  color: #111827;
}

:global(html[data-color-scheme='light'] .message-close) {
  color: rgba(17, 24, 39, 0.45);
}

:global(html[data-color-scheme='light'] .message-close:hover) {
  background: rgba(17, 24, 39, 0.06);
  color: #111827;
}

:global(html[data-color-scheme='light'] .message-progress) {
  background: linear-gradient(90deg, var(--message-accent), rgba(17, 24, 39, 0.16));
}

.message-icon {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  margin-top: 1px;
  border: 1px solid var(--message-accent-line);
  border-radius: 10px;
  background: var(--message-accent-soft);
  box-shadow: 0 0 18px var(--message-accent-glow);
  color: var(--message-accent);
}

.message-icon svg {
  width: 15px;
  height: 15px;
}

.message-copy {
  min-width: 0;
  padding-top: 1px;
}

.message-kicker {
  display: block;
  margin-bottom: 3px;
  color: var(--message-accent);
  font-size: 0.625rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  line-height: 1;
  opacity: 0.86;
}

.message-text {
  margin: 0;
  color: #f4f7fb;
  font-size: 0.8125rem;
  font-weight: 500;
  line-height: 1.55;
}

.message-action {
  min-height: 28px;
  margin-top: 7px;
  padding: 0 10px;
  border: 1px solid var(--message-accent-line);
  border-radius: 8px;
  background: var(--message-accent-soft);
  color: var(--message-accent);
  font-size: 0.6875rem;
  font-weight: 600;
  letter-spacing: 0.02em;
}

.message-close {
  display: grid;
  place-items: center;
  width: 24px;
  height: 24px;
  margin: 1px -3px 0 0;
  padding: 0;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: rgba(244, 247, 251, 0.52);
}

.message-close svg {
  width: 14px;
  height: 14px;
}

.message-close:hover {
  background: rgba(255, 255, 255, 0.08);
  color: rgba(244, 247, 251, 0.92);
}

.message-progress {
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 2px;
  background: linear-gradient(90deg, var(--message-accent), rgba(255, 255, 255, 0.28));
  transform-origin: left center;
  animation-name: message-progress;
  animation-timing-function: linear;
  animation-fill-mode: forwards;
}

@keyframes message-progress {
  from { transform: scaleX(1); }
  to { transform: scaleX(0); }
}

.message-enter-active {
  transition: opacity 0.22s ease, transform 0.26s cubic-bezier(0.2, 0.8, 0.2, 1);
}

.message-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.message-enter-from {
  opacity: 0;
  transform: translateY(-10px) scale(0.98);
}

.message-leave-to {
  opacity: 0;
  transform: translateY(-6px) scale(0.99);
}

.message-move {
  transition: transform 0.22s ease;
}

@media (prefers-reduced-motion: reduce) {
  .message-enter-active,
  .message-leave-active,
  .message-move {
    transition: none;
  }

  .message-progress {
    animation: none;
    opacity: 0;
  }
}
</style>
