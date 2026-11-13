import { computed, shallowRef, watch } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'

export function useUnsavedSheet(
  session: () => string,
  isEditing: () => boolean,
  isSaving: () => boolean,
  getFields: () => Record<string, string>,
  close: () => void
) {
  let initialFields = getFields()
  const decideDiscard = shallowRef<((discard: boolean) => void) | null>(null)
  const discardOpen = computed(() => decideDiscard.value !== null)

  watch(session, () => { initialFields = getFields() })

  function hasChanges(): boolean {
    return isEditing() && Object.entries(getFields()).some(([field, value]) => value !== initialFields[field])
  }

  function updateDefault(field: string, value: string): void {
    initialFields[field] = value
  }

  function requestClose(): void {
    if (isSaving() || discardOpen.value) return

    if (hasChanges()) {
      decideDiscard.value = (discard) => { if (discard) close() }
    } else {
      close()
    }
  }

  function resolveDiscard(discard: boolean): void {
    const decide = decideDiscard.value
    decideDiscard.value = null
    decide?.(discard)
  }

  onBeforeRouteLeave(() => {
    if (discardOpen.value || (isEditing() && isSaving())) return false
    if (!hasChanges()) return true
    return new Promise<boolean>((resolve) => { decideDiscard.value = resolve })
  })

  return { discardOpen, requestClose, resolveDiscard, updateDefault }
}
