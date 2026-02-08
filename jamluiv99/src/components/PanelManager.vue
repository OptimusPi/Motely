<template>
  <div v-if="layoutMode === 'stack'" ref="stackContainer" class="layout-stack">
    <template v-for="(panel, index) in visiblePanels" :key="panel.id">
      <PanelSection
        :color="panel.color"
        :label="getPanelLabel(panel)"
        :min-height="panel.minHeight"
        :default-height="panel.defaultHeight"
        :layout-mode="layoutMode"
        :fill-remaining="index === visiblePanels.length - 1"
        :panel-id="panel.id"
        :can-close="visiblePanels.length > 1 && !isBasePanel(panel)"
        :tab-align="panel.side === 'left' ? 'left' : 'right'"
        @resize="onPanelResize(panel.id, $event)"
        @collapse="onPanelCollapse(panel.id, $event)"
        @close="removePanel(panel.id)"
        @move-to-side="movePanelToSide"
        @drag-start="playClickSound('click')"
        @top-drag="index > 0 && !isMobile ? startStackResize(visiblePanels[index - 1]?.id, $event) : null"
      >
        <component :is="panel.component" v-bind="getPanelProps(panel)" v-on="getPanelEvents(panel)" />
      </PanelSection>
    </template>
  </div>

  <div v-else ref="splitContainer" class="layout-split" style="position: relative;">
    <div ref="leftColumnContainer" class="split-column split-left" :style="{ width: splitLeftWidth + '%' }">
      <div v-if="collapsedLeftPanels.length > 0" class="collapsed-tab-row">
        <div
          v-for="panel in collapsedLeftPanels"
          :key="panel.id"
          class="collapsed-tab"
          :class="[`collapsed-tab-${panel.color}`]"
          draggable="true"
          @dragstart="startDragFromCollapsed(panel, $event)"
          @dragend="endDragFromCollapsed"
        >
          {{ getPanelLabel(panel) }}
        </div>
      </div>
      <div 
        class="drop-zone drop-zone-top" 
        @dragover.prevent="onDropZoneOver" 
        @dragleave="onDropZoneLeave"
        @drop="onDropZoneTop('left', $event)"
      >
        <div class="drop-zone-content">↓ DROP HERE ↓</div>
      </div>
      <template v-for="(panel, index) in leftPanels" :key="panel.id">
        <PanelSection
          :color="panel.color"
          :label="getPanelLabel(panel)"
          :min-height="panel.minHeight"
          :default-height="panel.defaultHeight"
          :tab-align="'left'"
          :layout-mode="'stack'"
          :fill-remaining="index === leftPanels.length - 1"
          :panel-id="panel.id"
            :can-close="leftPanels.length > 1 && !isBasePanel(panel)"
          @resize="onPanelResize(panel.id, $event)"
          @collapse="onPanelCollapse(panel.id, $event)"
            @close="removePanel(panel.id)"
          @move-to-side="movePanelToSide"
          @drag-start="playClickSound('click')"
          @top-drag="index > 0 && !isMobile ? startColumnResize('left', leftPanels[index - 1]?.id, $event) : null"
        >
          <component :is="panel.component" v-bind="getPanelProps(panel)" v-on="getPanelEvents(panel)" />
        </PanelSection>
      </template>
      <div 
        class="drop-zone drop-zone-bottom" 
        @dragover.prevent="onDropZoneOver" 
        @dragleave="onDropZoneLeave"
        @drop="onDropZoneBottom('left', $event)"
      >
        <div class="drop-zone-content">↓ DROP HERE ↓</div>
      </div>
    </div>

    <div v-if="!isMobile" class="split-divider" @pointerdown="startSplitResize" role="separator">
      <div 
        class="jaml-badge"
        :class="[badgeSnapClass]"
      >
        <GripVertical v-if="badgeSnapState !== 'left'" :size="16" />
        <Home :size="16" @click.stop.prevent="goHome" class="icon-btn" title="Go Home" />
        <span class="logo">JAML</span>
        <button @click.stop.prevent="resetLayout" class="icon-btn reset-btn" title="Reset Layout (Ctrl+R)" :disabled="savingFilter || startingSearch">↻</button>
        <Settings :size="16" @click.stop.prevent="toggleSettings" class="icon-btn" title="Settings (Ctrl+K)" :aria-expanded="showSettings" />
        <GripVertical v-if="badgeSnapState !== 'right'" :size="16" />
      </div>
    </div>

    <div ref="rightColumnContainer" class="split-column split-right" :style="{ width: (100 - splitLeftWidth) + '%' }">
      <div v-if="collapsedRightPanels.length > 0" class="collapsed-tab-row">
        <div
          v-for="panel in collapsedRightPanels"
          :key="panel.id"
          class="collapsed-tab"
          :class="[`collapsed-tab-${panel.color}`]"
          draggable="true"
          @dragstart="startDragFromCollapsed(panel, $event)"
          @dragend="endDragFromCollapsed"
        >
          {{ getPanelLabel(panel) }}
        </div>
      </div>
      <div 
        class="drop-zone drop-zone-top" 
        @dragover.prevent="onDropZoneOver" 
        @dragleave="onDropZoneLeave"
        @drop="onDropZoneTop('right', $event)"
      >
        <div class="drop-zone-content">↓ DROP HERE ↓</div>
      </div>
      <template v-for="(panel, index) in rightPanels" :key="panel.id">
        <PanelSection
          :color="panel.color"
          :label="getPanelLabel(panel)"
          :min-height="panel.minHeight"
          :default-height="panel.defaultHeight"
          :tab-align="'right'"
          :layout-mode="'split'"
          :fill-remaining="index === rightPanels.length - 1"
          :panel-id="panel.id"
            :can-close="rightPanels.length > 1 && !isBasePanel(panel)"
          @resize="onPanelResize(panel.id, $event)"
          @collapse="onPanelCollapse(panel.id, $event)"
            @close="removePanel(panel.id)"
          @move-to-side="movePanelToSide"
          @drag-start="playClickSound('click')"
          @top-drag="index > 0 && !isMobile ? startColumnResize('right', rightPanels[index - 1]?.id, $event) : null"
        >
          <component :is="panel.component" v-bind="getPanelProps(panel)" v-on="getPanelEvents(panel)" />
        </PanelSection>
      </template>
      <div 
        class="drop-zone drop-zone-bottom" 
        @dragover.prevent="onDropZoneOver" 
        @dragleave="onDropZoneLeave"
        @drop="onDropZoneBottom('right', $event)"
      >
        <div class="drop-zone-content">↓ DROP HERE ↓</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { Home, Settings, GripVertical } from 'lucide-vue-next'
import PanelSection from './PanelSection.vue'
import { useSound } from '../composables/useSound'

const props = defineProps({
  layoutMode: String,
  visiblePanels: Array,
  leftPanels: Array,
  rightPanels: Array,
  collapsedLeftPanels: Array,
  collapsedRightPanels: Array,
  splitLeftWidth: Number,
  badgeSnapState: String,
  cornerHandleY: Number,
  isMobile: Boolean,
  savingFilter: Boolean,
  startingSearch: Boolean,
  showSettings: Boolean,
  jamlContent: String,
  results: Array,
  columns: Array,
  searchStatus: String,
  isSearching: Boolean,
  activeSearches: Array,
  getPanelLabel: Function,
  isBasePanel: Function,
    removePanel: Function,
  movePanelToSide: Function,
  onPanelResize: Function,
  onPanelCollapse: Function,
  expandPanel: Function,
  startSplitResize: Function,
  startStackResize: Function,
  startColumnResize: Function,
  startCornerResize: Function,
  goHome: Function,
  resetLayout: Function,
  toggleSettings: Function,
  handleSaveFilter: Function,
  handleStartSearch: Function,
  handleStopSearch: Function,
  clearResults: Function,
  exportResults: Function,
  handleStopSpecificSearch: Function,
  updateJamlContent: Function,
  handleLoadJamlFromGenie: Function,
  startDragFromCollapsed: Function,
  endDragFromCollapsed: Function
})

const { playClickSound } = useSound()

const badgeSnapClass = computed(() => `badge-snap-${props.badgeSnapState}`)

const getPanelProps = (panel) => {
  const base = panel.props || {}
  if (panel.baseId === 'jaml-editor') return { ...base, jaml: props.jamlContent || '' }
  if (panel.baseId === 'results') return { ...base, results: props.results, columns: props.columns, status: props.searchStatus, isSearching: props.isSearching }
  if (panel.baseId === 'active-searches') return { ...base, searches: props.activeSearches }
  return base
}

const getPanelEvents = (panel) => {
  const events = {}
  if (panel.baseId === 'jaml-editor') {
    events.save = props.handleSaveFilter
    events['update:jaml'] = props.updateJamlContent
    events['load-jaml'] = props.handleLoadJamlFromGenie
  } else if (panel.baseId === 'results') {
    events.start = props.handleStartSearch
    events.stop = props.handleStopSearch
    events.clear = props.clearResults
    events.export = props.exportResults
    events['stop-search'] = props.handleStopSpecificSearch
  } else if (panel.baseId === 'active-searches') {
    events['stop-search'] = props.handleStopSpecificSearch
  }
  return events
}

const onDropZoneOver = (event) => {
  event.dataTransfer.dropEffect = 'move'
  event.currentTarget.classList.add('active')
}

const onDropZoneLeave = (event) => {
  event.currentTarget.classList.remove('active')
}

const onDropZoneTop = (side, event) => {
  onDropZoneLeave(event)
  const collapsedId = event.dataTransfer.getData('collapsed-panel-id')
  const regularId = event.dataTransfer.getData('text/plain')
  const panelId = collapsedId || regularId
  
  if (!panelId) return
  
  if (collapsedId) {
    props.expandPanel?.(panelId)
  }
  
  props.movePanelToSide?.(panelId, side)
}

const onDropZoneBottom = (side, event) => {
  onDropZoneLeave(event)
  const collapsedId = event.dataTransfer.getData('collapsed-panel-id')
  const regularId = event.dataTransfer.getData('text/plain')
  const panelId = collapsedId || regularId
  
  if (!panelId) return
  
  if (collapsedId) {
    props.expandPanel?.(panelId)
  }
  
  props.movePanelToSide?.(panelId, side)
}
</script>

<style scoped>
.layout-stack {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: visible;
  gap: 0;
  padding-top: 28px;
}

.layout-split {
  display: flex;
  width: 100%;
}

.split-column {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: visible;
  gap: 0;
  min-height: 0;
  padding-top: 28px;
}

.split-divider {
  width: 10px;
  min-width: 10px;
  cursor: ew-resize;
  background: var(--balatro-gold);
  flex-shrink: 0;
  position: relative;
  touch-action: none;
  user-select: none;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  z-index: 2000;
}

.split-divider:hover {
  background: var(--balatro-dark-gold);
}

.split-divider.is-resizing {
  background: var(--balatro-dark-gold);
}

.jaml-badge {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  font-weight: normal;
  padding: 4px 10px;
  background: rgba(50, 60, 70, 0.95);
  color: #fff;
  height: 28px;
  box-sizing: border-box;
  user-select: none;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  pointer-events: auto;
  z-index: 2001;
  left: 50%;
  margin-left: -50px;
  transition: top 0.2s ease;
}

.jaml-badge .logo {
  letter-spacing: 1px;
  font-weight: normal;
}

.jaml-badge .icon-btn {
  cursor: pointer;
  opacity: 0.8;
  pointer-events: auto;
  background: none;
  border: none;
  color: inherit;
  padding: 2px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: opacity 0.15s;
  font-weight: normal;
}

.jaml-badge .icon-btn:hover {
  opacity: 1;
}
</style>
