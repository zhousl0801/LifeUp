<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { fetchHistory } from '../api/history'
import { getApiErrorMessage } from '../api/client'
import type { BusinessFact } from '../types/history'
import { formatMoney } from '../utils/format'
import LoadingButton from './LoadingButton.vue'

const props = defineProps<{ businessDate: string; cashEffect: number; disabled?: boolean }>()
const model = defineModel<string | null>({ default: null })
const opened = ref(false)
const anchors = ref<BusinessFact[]>([])
const error = ref('')
const loading = ref(false)
const options = computed(() => anchors.value.filter(fact => !fact.isReversed && fact.date >= props.businessDate
  && Math.sign(fact.recordedDifference ?? 0) === Math.sign(props.cashEffect)))

onMounted(() => { if (model.value) void load() })

async function load(): Promise<void> {
  opened.value = true
  loading.value = true
  error.value = ''
  try { anchors.value = (await fetchHistory()).facts.filter(fact => fact.source === 'adjustment') }
  catch (cause) { error.value = getApiErrorMessage(cause, '核对记录加载失败，请重试。') }
  finally { loading.value = false }
}
</script>

<template>
  <div class="cash-adjustment-link">
    <LoadingButton
      v-if="!opened || loading"
      class="back-link"
      :loading="loading"
      :disabled="disabled"
      loading-label="加载中…"
      @click="load"
    >
      关联此前核对差额（漏记业务）
    </LoadingButton>
    <template v-else>
      <label class="form-field">
        <span>关联现金核对记录</span>
        <select v-model="model" :disabled="disabled || loading">
          <option :value="null">不关联，作为独立业务</option>
          <option v-for="fact in options" :key="fact.id" :value="fact.id">
            {{ fact.date }} · 原差额 {{ formatMoney(fact.recordedDifference) }} · {{ fact.note }}
          </option>
        </select>
      </label>
      <small class="form-note">关联后重算核对差额，避免重复增减现金。日期、方向与累计金额由保存时校验。</small>
      <p v-if="error" class="form-error">
        {{ error }}
        <LoadingButton class="back-link" :loading="loading" loading-label="加载中…" @click="load">重试</LoadingButton>
      </p>
    </template>
  </div>
</template>
