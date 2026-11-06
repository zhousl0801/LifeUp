<script setup lang="ts">
import { computed, toRef } from 'vue'
import { useCountUp } from '../utils/countUp'
import { amountTone, formatMoney, formatSignedMoney } from '../utils/format'

const props = withDefaults(defineProps<{
  value: number | null | undefined
  signed?: boolean
  tone?: boolean
  prefix?: string
}>(), {
  signed: false,
  tone: false,
  prefix: '¥'
})

const display = useCountUp(toRef(props, 'value'))
const text = computed(() => props.signed
  ? formatSignedMoney(display.value)
  : formatMoney(display.value))
const prefix = computed(() => props.signed ? '' : props.prefix)
const toneClass = computed(() => props.tone ? amountTone(display.value) : '')
</script>

<template>
  <span class="animated-money" :class="toneClass">{{ prefix }}{{ text }}</span>
</template>
