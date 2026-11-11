import type { InjectionKey, Ref } from 'vue'

export const pageDialogContext: InjectionKey<{
  host: Readonly<Ref<HTMLElement | null>>
  dialogs: Ref<string[]>
}> = Symbol('pageDialogContext')
