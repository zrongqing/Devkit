<script setup lang="ts">
import { computed, inject, nextTick, onBeforeUnmount, ref, useId, useTemplateRef, watch } from 'vue'
import { Close } from '@element-plus/icons-vue'
import { pageDialogContext } from './pageDialogContext'

const props = withDefaults(defineProps<{
  title: string
  width?: number
  closable?: boolean
  closeOnClickMask?: boolean
}>(), { width: 560, closable: true, closeOnClickMask: true })
const open = defineModel<boolean>({ required: true })
const context = inject(pageDialogContext)
if (!context) throw new Error('PageDialog 必须在工作台页签内使用。')
const { host, dialogs } = context
const id = useId()
const stackIndex = computed(() => dialogs.value.indexOf(id))
const front = computed(() => dialogs.value.at(-1) === id)
const visible = computed(() => open.value && !!host.value)
const layer = useTemplateRef<HTMLElement>('layer')
const panel = useTemplateRef<HTMLElement>('panel')
const bounds = ref({ width: 0, height: 0 })

type Rect = { left: number; top: number; width: number; height: number }
const rect = ref<Rect>()
let returnFocus: HTMLElement | undefined
let gesture: { pointerId: number; x: number; y: number; box: Rect; resize: boolean } | undefined
const fittedRect = computed(() => {
  if (!rect.value || !bounds.value.width || !bounds.value.height) return
  const width = Math.min(rect.value.width, bounds.value.width)
  const height = Math.min(rect.value.height, bounds.value.height)
  return {
    left: clamp(rect.value.left, 0, bounds.value.width - width),
    top: clamp(rect.value.top, 0, bounds.value.height - height),
    width,
    height,
  }
})
const panelStyle = computed(() => {
  const box = fittedRect.value
  if (!box) return { '--page-dialog-width': `${props.width}px` }
  return {
    inset: `${box.top}px auto auto ${box.left}px`,
    width: `${box.width}px`,
    height: `${box.height}px`,
    maxWidth: '100%',
    maxHeight: '100%',
  }
})

function release() {
  if (stackIndex.value < 0) return
  const restore = front.value && panel.value?.contains(document.activeElement)
  dialogs.value = dialogs.value.filter(dialog => dialog !== id)
  gesture = undefined
  const trigger = returnFocus
  if (restore) {
    void nextTick(() => {
      if (stackIndex.value < 0) trigger?.focus({ preventScroll: true })
    })
  }
}

watch(visible, async (shown) => {
  if (!shown) {
    release()
    return
  }
  const trigger = document.activeElement
  returnFocus = trigger instanceof HTMLElement ? trigger : undefined
  rect.value = undefined
  dialogs.value.push(id)
  await nextTick()
  if (visible.value && front.value) panel.value?.focus({ preventScroll: true })
}, { immediate: true })

watch(layer, (element, _, onCleanup) => {
  if (!element) return
  const measure = () => {
    bounds.value = { width: element.clientWidth, height: element.clientHeight }
  }
  measure()
  const observer = new ResizeObserver(measure)
  observer.observe(element)
  onCleanup(() => observer.disconnect())
})
onBeforeUnmount(release)

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value))
}

function startGesture(event: PointerEvent, resize: boolean) {
  const element = panel.value
  const handle = event.currentTarget
  if (event.button !== 0 || !event.isPrimary || !element || !(handle instanceof HTMLElement)) return
  const { offsetLeft, offsetTop, offsetWidth, offsetHeight } = element
  const box = { left: offsetLeft, top: offsetTop, width: offsetWidth, height: offsetHeight }
  gesture = { pointerId: event.pointerId, x: event.clientX, y: event.clientY, box, resize }
  handle.setPointerCapture(event.pointerId)
  event.preventDefault()
}

function moveGesture(event: PointerEvent) {
  if (!gesture || event.pointerId !== gesture.pointerId) return
  const { box, x, y, resize } = gesture
  const dx = event.clientX - x
  const dy = event.clientY - y
  const { width, height } = bounds.value
  if (!width || !height) return
  if (resize) {
    const availableWidth = Math.max(1, width - box.left)
    const availableHeight = Math.max(1, height - box.top)
    rect.value = {
      ...box,
      width: clamp(box.width + dx, Math.min(300, availableWidth), availableWidth),
      height: clamp(box.height + dy, Math.min(180, availableHeight), availableHeight),
    }
  } else {
    rect.value = {
      ...box,
      left: clamp(box.left + dx, 0, width - box.width),
      top: clamp(box.top + dy, 0, height - box.height),
    }
  }
}

function endGesture(event: PointerEvent) {
  if (gesture?.pointerId === event.pointerId) gesture = undefined
}

function close(event?: KeyboardEvent) {
  if (!props.closable || event?.defaultPrevented) return
  event?.stopPropagation()
  open.value = false
}
</script>

<template>
  <Teleport v-if="visible && host" :to="host">
    <div
      ref="layer"
      class="page-dialog-layer"
      :style="{ zIndex: stackIndex }"
      :inert="!front"
      @click.self="closeOnClickMask && close()"
    >
      <section
        ref="panel"
        class="page-dialog"
        :style="panelStyle"
        role="dialog"
        :aria-labelledby="id"
        tabindex="-1"
        @keydown.esc="close"
        @pointermove="moveGesture"
        @pointerup="endGesture"
        @pointercancel="endGesture"
        @lostpointercapture="endGesture"
      >
        <header class="page-dialog-header" @pointerdown="startGesture($event, false)">
          <h2 :id="id">{{ title }}</h2>
          <el-button text :icon="Close" :disabled="!closable" aria-label="关闭弹框" @pointerdown.stop @click="close()" />
        </header>
        <div class="page-dialog-body"><slot /></div>
        <div v-if="$slots.footer" class="page-dialog-footer"><slot name="footer" /></div>
        <span
          class="page-dialog-resize"
          title="拖动调整弹框大小"
          aria-hidden="true"
          @pointerdown="startGesture($event, true)"
        />
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.page-dialog-layer {
  position: absolute;
  inset: 0;
  overflow: hidden;
  background: var(--el-overlay-color-lighter);
  pointer-events: auto;
}
.page-dialog {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  width: var(--page-dialog-width);
  height: fit-content;
  max-width: calc(100% - 24px);
  max-height: calc(100% - 24px);
  margin: auto;
  overflow: hidden;
  border-radius: var(--el-border-radius-base);
  outline: none;
  background: var(--el-bg-color-overlay);
  box-shadow: var(--el-box-shadow);
}
.page-dialog-header {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 10px 16px;
  cursor: move;
  touch-action: none;
  user-select: none;
}
.page-dialog-header h2 {
  margin: 0;
  color: var(--el-text-color-primary);
  font-size: 18px;
  font-weight: 500;
  overflow-wrap: anywhere;
}
.page-dialog-body {
  flex: 1 1 auto;
  min-height: 0;
  padding: 0 16px 20px;
  overflow: auto;
  overscroll-behavior: contain;
}
.page-dialog-footer {
  flex: 0 0 auto;
  padding: 0 24px 16px;
  text-align: right;
}
.page-dialog-resize {
  position: absolute;
  right: 0;
  bottom: 0;
  width: 20px;
  height: 20px;
  color: var(--el-text-color-secondary);
  background: linear-gradient(135deg, transparent 52%, currentColor 53% 58%, transparent 59% 69%, currentColor 70% 75%, transparent 76%) center / 12px 12px no-repeat;
  cursor: nwse-resize;
  touch-action: none;
}
</style>
