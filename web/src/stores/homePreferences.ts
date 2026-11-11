import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useHomePreferencesStore = defineStore('homePreferences', () => {
  const amountsMasked = ref(true)

  return { amountsMasked }
})
