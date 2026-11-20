<script setup lang="ts">
import { computed } from 'vue'
import { amountTone, formatMoney, formatSignedMoney } from '../utils/format'

const props = withDefaults(defineProps<{
  value: number | null | undefined
  signed?: boolean
  tone?: boolean
  prefix?: string
  masked?: boolean
}>(), {
  signed: false,
  tone: false,
  prefix: '¥'
})

const text = computed(() => {
  if (props.masked && props.value != null) return '••••'
  return props.signed ? formatSignedMoney(props.value) : formatMoney(props.value)
})
const prefix = computed(() => props.signed ? '' : props.prefix)
const toneClass = computed(() => props.tone && !props.masked ? amountTone(props.value) : '')
</script>

<template>
  <span class="money-amount" :class="toneClass">{{ prefix }}{{ text }}</span>
</template>
