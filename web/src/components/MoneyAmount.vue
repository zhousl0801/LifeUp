<script setup lang="ts">
import { computed } from 'vue'
import { useAmountPrivacy } from '../composables/useAmountPrivacy'
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
  prefix: '¥',
  masked: undefined
})

const { amountsMasked } = useAmountPrivacy()
const isMasked = computed(() => props.masked ?? amountsMasked.value)

const text = computed(() => {
  if (isMasked.value && props.value != null) return '••••'
  return props.signed ? formatSignedMoney(props.value) : formatMoney(props.value)
})
const prefix = computed(() => props.signed ? '' : props.prefix)
const toneClass = computed(() => props.tone && !isMasked.value ? amountTone(props.value) : '')
</script>

<template>
  <span class="money-amount" :class="toneClass">{{ prefix }}{{ text }}</span>
</template>
