import { cloneVNode, Comment, defineComponent, Fragment, h, isVNode, nextTick, onBeforeUnmount, ref, watch, type PropType, type VNode } from 'vue'
import { ElAlert, ElButton, ElMessage, ElPagination, ElTable, type TableColumnCtx, type TableInstance } from 'element-plus'
import type { FieldSort, PageLoader, PageQuery } from '../api/tableQueries'
import TableToolbar from './TableToolbar.vue'
import type { ColumnOption, ColumnPin, FilterDraft, FilterOption, TableFilterSchema, TableFilterTag } from './tablePresentation'
import { buildFilter, copyDrafts, filterLabel, hasFilter } from './tableFilters'
import TableColumnHeader from './TableColumnHeader.vue'

type Row = Record<string, any>
function flatten(nodes: VNode[]): VNode[] {
  return nodes.flatMap(node => node.type === Fragment && Array.isArray(node.children)
    ? flatten(node.children.filter(isVNode)) : node.type === Comment ? [] : [node])
}

/** Server owns queries; column pin and width preferences are stored locally. */
export default defineComponent({
  name: 'DataTable', inheritAttrs: false,
  emits: ['resetFilters', 'removeFilter'],
  props: {
    data: { type: Array as PropType<Row[]>, default: () => [] },
    tableKey: { type: String, required: true },
    loadPage: { type: Function as PropType<PageLoader>, required: true },
    queryContext: { type: String, default: '' },
    searchField: String,
    externalFilters: { type: Array as PropType<TableFilterTag[]>, default: () => [] },
    filterSchema: { type: Object as PropType<TableFilterSchema>, default: () => ({}) },
  },
  setup(props, { attrs, slots, expose, emit }) {
    const page = ref(1), pageSize = ref(20), total = ref(0)
    const rows = ref<Row[]>([]), loading = ref(false), error = ref('')
    const showLoading = ref(false)
    const table = ref<TableInstance>()
    const widths = ref<Record<string, number>>({})
    const measuredMinimums = ref<Record<string, number>>({})
    const minimumWidths = new Map<string, number>()
    const drafts = ref<Record<string, FilterDraft>>({}), filters = ref<Record<string, FilterDraft>>({})
    const optionLabels = ref<Record<string, FilterOption[]>>({})
    const sorts = ref<FieldSort[]>([]), pins = ref<Record<string, ColumnPin>>({})
    const hidden = ref<Record<string, boolean>>({}), viewportWidth = ref(0)
    const storageKey = () => `devkit:table-pins:v1:${props.tableKey}`
    const hiddenKey = () => `devkit:table-hidden:v1:${props.tableKey}`
    const widthsKey = () => `devkit:table-widths:v1:${props.tableKey}`
    let controller: AbortController | undefined
    let sequence = 0
    let viewportObserver: ResizeObserver | undefined
    let loadingTimer: ReturnType<typeof setTimeout> | undefined
    function savePins() { try { localStorage.setItem(storageKey(), JSON.stringify(pins.value)) } catch { /* Optional cache. */ } }
    function saveWidths() { try { localStorage.setItem(widthsKey(), JSON.stringify(widths.value)) } catch { /* Resizing still works without storage. */ } }
    function saveHidden() { try { localStorage.setItem(hiddenKey(), JSON.stringify(hidden.value)) } catch { /* Optional preference. */ } }
    watch(() => table.value?.$el, element => {
      viewportObserver?.disconnect()
      if (!(element instanceof HTMLElement)) return
      viewportWidth.value = element.clientWidth
      viewportObserver = new ResizeObserver(() => { viewportWidth.value = element.clientWidth })
      viewportObserver.observe(element)
    }, { flush: 'post' })
    function resized(width: number, oldWidth: number, column: TableColumnCtx<Row>, event: MouseEvent) {
      const key = column.columnKey
      if (key && Number.isFinite(width)) {
        const bounded = Math.max(minimumWidths.get(key) ?? 40, Math.min(10000, Math.round(width)))
        column.width = column.realWidth = bounded
        widths.value[key] = bounded
        saveWidths()
      }
      const handler = attrs.onHeaderDragend
      if (typeof handler === 'function') handler(column.width, oldWidth, column, event)
    }
    // Element Plus forces these utility columns to be non-resizable during setup.
    // Its exposed column configurations let us enable the native drag behavior
    // while retaining the built-in selection and row expansion renderers.
    watch(() => table.value?.columns, columns => {
      columns?.forEach(column => { column.resizable = true })
    }, { flush: 'post' })
    async function reload() {
      controller?.abort(); controller = new AbortController()
      const current = ++sequence
      const query: PageQuery = { page: page.value, pageSize: pageSize.value,
        filters: Object.entries(filters.value).flatMap(([field, value]) => buildFilter(field, value, props.filterSchema[field])),
        sorts: [...sorts.value],
      }
      loading.value = true; error.value = ''
      // Keep the last page visible. Fast requests should not flash a loading state.
      if (!showLoading.value && loadingTimer === undefined) loadingTimer = setTimeout(() => {
        loadingTimer = undefined
        showLoading.value = true
      }, 150)
      try {
        const result = await props.loadPage(query, controller.signal)
        if (current !== sequence) return
        const last = Math.max(1, Math.ceil(result.total / pageSize.value))
        if (page.value > last) { page.value = last; await reload(); return }
        rows.value = result.items; total.value = result.total
      } catch (e) {
        if (current !== sequence || controller.signal.aborted) return
        rows.value = []; total.value = 0
        error.value = e instanceof Error ? e.message : String(e)
      } finally {
        if (current === sequence) {
          loading.value = false; showLoading.value = false
          clearTimeout(loadingTimer); loadingTimer = undefined
        }
      }
    }
    function submit() {
      try { Object.entries(drafts.value).forEach(([field, value]) => buildFilter(field, value, props.filterSchema[field])) }
      catch (e) { ElMessage.warning(e instanceof Error ? e.message : String(e)); return }
      filters.value = copyDrafts(drafts.value); page.value = 1; void reload()
    }
    async function resetFilters() {
      controller?.abort()
      const current = ++sequence
      drafts.value = {}; filters.value = {}; page.value = 1
      emit('resetFilters')
      // Page-level search/status selectors update the loader and context in the
      // same tick. Let their watcher query once, or query here if none changed.
      await nextTick()
      if (current === sequence) await reload()
    }
    function resetTable() {
      pins.value = {}; hidden.value = {}; widths.value = {}; sorts.value = []
      savePins(); saveHidden(); saveWidths(); void resetFilters()
    }
    async function autoFit() {
      await nextTick()
      const element = table.value?.$el as HTMLElement | undefined
      if (!element || loading.value) return
      const measure = document.createElement('div')
      measure.className = 'data-table-measure'
      measure.setAttribute('aria-hidden', 'true')
      measure.inert = true
      element.appendChild(measure)
      const next = { ...widths.value }
      try {
        for (const column of table.value?.columns ?? []) {
          const key = column.columnKey
          if (!key) continue
          let width = minimumWidths.get(key) ?? 40
          const cells = element.querySelectorAll<HTMLElement>(`.el-table__body-wrapper td.${column.id} > .cell`)
          for (const cell of cells) {
            const clone = cell.cloneNode(true) as HTMLElement
            const style = getComputedStyle(cell)
            // Measure a detached copy at its intrinsic width, including rendered
            // tags, buttons, icons and padding rather than raw field values.
            Object.assign(clone.style, {
              width: 'max-content', minWidth: '0', maxWidth: 'none', whiteSpace: 'nowrap',
              font: style.font, paddingLeft: style.paddingLeft, paddingRight: style.paddingRight,
            })
            measure.replaceChildren(clone)
            width = Math.max(width, Math.ceil(clone.getBoundingClientRect().width + 2))
          }
          next[key] = Math.max(minimumWidths.get(key) ?? 40, Math.min(480, width))
        }
      } finally { measure.remove() }
      widths.value = next
      saveWidths()
      await nextTick()
      table.value?.doLayout()
    }
    async function removeFilter(key: string, external: boolean) {
      if (!external) {
        delete drafts.value[key]; delete filters.value[key]; page.value = 1; await reload(); return
      }
      const current = sequence
      emit('removeFilter', key)
      await nextTick()
      if (current === sequence) { page.value = 1; await reload() }
    }
    function sortIndex(field: string | undefined) {
      return field === undefined ? -1 : sorts.value.findIndex(sort => sort.field.toLowerCase() === field.toLowerCase())
    }
    function setSort(field: string, direction: number) {
      // Read current state on every click, including clicks before the next render.
      const index = sortIndex(field)
      const current = index < 0 ? 0 : sorts.value[index].direction === 'asc' ? 1 : -1
      if (index < 0 && sorts.value.length >= 8) return
      const next = [...sorts.value]
      if (current === direction) next.splice(index, 1)
      else {
        const sort: FieldSort = { field, direction: direction === 1 ? 'asc' : 'desc' }
        if (index >= 0) next[index] = sort
        else next.push(sort)
      }
      sorts.value = next
      page.value = 1; void reload()
    }
    watch(() => props.tableKey, () => {
      pins.value = {}; drafts.value = {}; filters.value = {}; sorts.value = []; page.value = 1
      optionLabels.value = {}
      hidden.value = {}; widths.value = {}; measuredMinimums.value = {}; minimumWidths.clear()
      try {
        const saved: unknown = JSON.parse(localStorage.getItem(storageKey()) ?? '{}')
        if (saved && typeof saved === 'object' && !Array.isArray(saved)) pins.value = Object.fromEntries(Object.entries(saved).filter(([, value]) => typeof value === 'boolean' || value === 'left' || value === 'right').map(([key, value]) => [key, value === true ? 'left' : value]))
      } catch { /* Invalid cache never prevents querying. */ }
      try {
        const saved: unknown = JSON.parse(localStorage.getItem(widthsKey()) ?? '{}')
        if (saved && typeof saved === 'object' && !Array.isArray(saved)) widths.value = Object.fromEntries(
          Object.entries(saved).filter(([, width]) => typeof width === 'number' && Number.isFinite(width) && width >= 40 && width <= 10000),
        )
      } catch { /* Invalid or unavailable width storage falls back to page defaults. */ }
      try {
        const saved: unknown = JSON.parse(localStorage.getItem(hiddenKey()) ?? '{}')
        if (saved && typeof saved === 'object' && !Array.isArray(saved)) hidden.value = Object.fromEntries(Object.entries(saved).filter(([, value]) => typeof value === 'boolean'))
      } catch { /* Invalid cache falls back to visible columns. */ }
    }, { immediate: true })
    // Batch context and refresh changes into one request. Only a different table
    // clears the previous page; filtering updates it atomically on success.
    watch([() => props.tableKey, () => props.queryContext, () => props.data], ([key, context], previous) => {
      if (key !== previous[0] || context !== previous[1]) page.value = 1
      if (key !== previous[0]) { rows.value = []; total.value = 0 }
      void reload()
    }, { immediate: true, flush: 'post' })
    onBeforeUnmount(() => { sequence++; controller?.abort(); clearTimeout(loadingTimer); viewportObserver?.disconnect() })
    expose({ reload, submit, resetFilters, autoFit })
    return () => {
      let index = 0
      const options: ColumnOption[] = []
      const nodes = flatten(slots.default?.() ?? []).filter(node => node.props?.label || node.props?.type).map(column => {
        const original = column.props ?? {}
        const utility = original.type === 'selection' || original.type === 'expand' || original.type === 'index'
        const key: string = original['column-key'] ?? original.columnKey ?? original.prop ?? original.label ?? original.type
        const field: string | undefined = original['query-field'] ?? original.prop ?? (original['table-value'] ? key : undefined)
        const labelWidth = String(original.label ?? '').length * 16
        const required = measuredMinimums.value[key] ?? (labelWidth + (utility ? 32 : field ? 96 : 32))
        const declaredMinimum = Number(original['min-width'] ?? original.minWidth ?? 0)
        minimumWidths.set(key, Math.max(utility ? 40 : 72, required, Number.isFinite(declaredMinimum) ? declaredMinimum : 0))
        const cachedWidth = widths.value[key] === undefined ? undefined : Math.max(minimumWidths.get(key)!, widths.value[key])
        if (utility) return cloneVNode(column, {
          key, columnKey: key, 'column-key': key, resizable: true,
          width: cachedWidth ?? Math.max(Number(original.width ?? 48), minimumWidths.get(key)!),
          minWidth: cachedWidth ?? minimumWidths.get(key),
          'min-width': cachedWidth ?? minimumWidths.get(key),
        })
        const defaultPin: ColumnPin = field ? (props.searchField ? field === props.searchField : index++ === 0) ? 'left' : false : key === 'actions' || key === 'details' ? 'right' : false
        const pin = pins.value[key] ?? defaultPin
        options.push({ key, label: String(original.label), field, visible: !hidden.value[key] || !field, pin })
        if (hidden.value[key] && field) return null
        const align = original.align ?? 'left'
        const minimum = Math.max(Number(original.width ?? original['min-width'] ?? 160), minimumWidths.get(key)!)
        const node = cloneVNode(column, { key, columnKey: key, 'column-key': key, resizable: true,
          fixed: viewportWidth.value >= 720 ? pin : false, align, headerAlign: align,
          showOverflowTooltip: !!field, width: cachedWidth ?? (original.width ? minimum : undefined),
          minWidth: cachedWidth ?? minimum, 'min-width': cachedWidth ?? minimum,
          // Element Plus retains this renderer in its column configuration. Read refs
          // inside it so the header tracks changes instead of capturing initial values.
          'render-header': () => {
            const orderIndex = sortIndex(field)
            const order = orderIndex < 0 ? 0 : sorts.value[orderIndex].direction === 'asc' ? 1 : -1
            const config = field ? props.filterSchema[field] : undefined
            const selected = field ? drafts.value[field] : undefined
            return h(TableColumnHeader, { label: original.label, searchable: !!field,
              filter: field ? drafts.value[field] ?? '' : '', config: config?.loadOptions ? { ...config,
                options: [...(config.options ?? []), ...(optionLabels.value[field!] ?? []).filter(o => Array.isArray(selected) && selected.includes(o.value))],
              } : config,
              active: !!(field && hasFilter(filters.value[field])), order,
              priority: orderIndex < 0 ? 0 : orderIndex + 1,
              onFilter: (value: FilterDraft) => { if (field) drafts.value[field] = value }, onSubmit: submit,
              onOptions: (values: FilterOption[]) => { if (field) optionLabels.value[field] = values },
              onMinimumWidth: (width: number) => {
                if (measuredMinimums.value[key] !== width) measuredMinimums.value[key] = width
              },
              onSort: (direction: number) => {
                if (field) setSort(field, direction)
              },
            })
          },
        })
        if (node.props) { delete node.props['table-value']; delete node.props['query-field'] }
        return node
      })
      const requestedRowKey = attrs['row-key'] ?? attrs.rowKey
      const rowKey = typeof requestedRowKey === 'string' ? requestedRowKey : (row: Row) =>
        String(typeof requestedRowKey === 'function' ? requestedRowKey(row) : row.id ?? row.menuCode ?? row.key)
      const tags: TableFilterTag[] = Object.entries(filters.value).filter(([, value]) => hasFilter(value)).map(([key, value]) => ({
        key, label: options.find(c => c.field === key)?.label ?? key,
        value: filterLabel(value, [...(props.filterSchema[key]?.options ?? []), ...(optionLabels.value[key] ?? [])]),
      })).concat(props.externalFilters.map(tag => ({ ...tag, external: true })))
      return h('div', { class: ['data-table', { 'data-table--pending': loading.value }], 'aria-busy': loading.value }, [
        h(TableToolbar, {
          columns: options, tags, loading: showLoading.value, querying: loading.value,
          onRefresh: reload, onResetFilters: resetFilters, onResetTable: resetTable, onAutoFit: autoFit, onRemoveFilter: removeFilter,
          onVisibility: (key: string, visible: boolean) => { hidden.value[key] = !visible; saveHidden() },
          onPin: (key: string, pin: ColumnPin) => { pins.value[key] = pin; savePins() },
        }, { filters: slots.filters, query: slots.toolbar, actions: slots.actions }),
        error.value ? h(ElAlert, { title: error.value, type: 'error', closable: false }, { default: () => h(ElButton, { text: true, onClick: reload }, () => '重试') }) : null,
        h(ElTable, { ...attrs, ref: table, border: true, allowDragLastColumn: false, fit: true,
          scrollbarAlwaysOn: true, tableLayout: 'fixed', data: rows.value,
          onHeaderDragend: resized,
          rowKey,
          // Kept rows may belong to the previous scope while a new query runs.
          // Header controls remain usable; row actions wait for the result.
          onClickCapture: (event: MouseEvent) => {
            if (loading.value && event.target instanceof Element && event.target.closest('.el-table__body-wrapper')) {
              event.preventDefault(); event.stopImmediatePropagation()
            }
            const handler = attrs.onClickCapture
            if (typeof handler === 'function') handler(event)
          },
          emptyText: String(attrs['empty-text'] ?? '暂无数据'),
        }, { empty: slots.empty, append: slots.append, default: () => nodes.filter((node): node is VNode => node !== null) }),
        h('div', { class: 'data-table-footer' }, [
          h('span', { class: 'data-table-total' }, `共 ${total.value} 条`),
          h(ElPagination, { currentPage: page.value, pageSize: pageSize.value, total: total.value, pageSizes: [10, 20, 50, 100],
            layout: 'sizes, prev, pager, next', background: true,
            'onUpdate:currentPage': (value: number) => { page.value = value; void reload() },
            'onUpdate:pageSize': (value: number) => { pageSize.value = value; page.value = 1; void reload() },
          }),
        ]),
      ])
    }
  },
})
