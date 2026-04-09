<script setup>
import { ArrowLeft, ArrowRight, Close, Hide, Refresh, View } from '@element-plus/icons-vue';
import { onBeforeUnmount, onMounted, ref } from 'vue';
import BaseButton from '../../components/BaseButton.vue';

const props = defineProps({
  settings: { type: Object, required: true },
  siteZoom: { type: [Number, String], required: true },
  patchSetting: { type: Function, required: true },
  webviewBack: Function,
  webviewForward: Function,
  webviewReload: Function,
  zoomIn: Function,
  zoomOut: Function,
  resetZoom: Function,
  toggleToolbarPinned: Function,
  disableToolbar: Function,
  toolbarVisible: Boolean,
  toolbarPinned: Boolean,
  toolbarDisabled: Boolean,
  toolbarIconOnly: Boolean,
  toolbarDocked: Boolean,
  hideHandle: Boolean
});

const popActiveKey = ref(null);
const popValue = ref(0);
const popLeft = ref('50%');
const draggingKey = ref(null);
const fontRangeRef = ref(null);
const autoRangeRef = ref(null);
let hideTimeout = null;
let hidePopTimeout = null;

function clearHidePopTimeout() {
  if (hidePopTimeout) {
    clearTimeout(hidePopTimeout);
    hidePopTimeout = null;
  }
}
// 全局 pointermove 相关，用于检测鼠标靠近窗口底部时显示工具栏
let _lastNear = false;
let _pmHandler = null;
const HOTZONE_HEIGHT = 220; // 与 toolbar-hotzone 保持一致

function _onPointerMove(e) {
  try {
    if (props.toolbarDisabled || props.toolbarPinned || props.hideHandle) return;
    const y = e && typeof e.clientY === 'number' ? e.clientY : (window.event && window.event.clientY) || 0;
    const winH = window.innerHeight || document.documentElement.clientHeight || 0;
    const near = y >= winH - HOTZONE_HEIGHT;
    if (near && !_lastNear) {
      _lastNear = true;
      try {
        props.patchSetting('toolbarVisible', true);
      } catch (err) {}
      if (hideTimeout) {
        clearTimeout(hideTimeout);
        hideTimeout = null;
      }
    } else if (!near && _lastNear) {
      _lastNear = false;
      // 只有在没有拖拽或弹出时才自动隐藏
      if (!draggingKey.value && !popActiveKey.value && !props.toolbarPinned) {
        if (hideTimeout) clearTimeout(hideTimeout);
        hideTimeout = setTimeout(() => {
          try {
            props.patchSetting('toolbarVisible', false);
          } catch (e) {}
          hideTimeout = null;
        }, 180);
      }
    }
  } catch (e) {}
}

function computeLeftPct(value, min, max) {
  const v = Number(value);
  if (Number.isNaN(v)) return '50%';
  const mn = Number(min || 0);
  const mx = Number(max || 100);
  const pct = mx === mn ? 50 : Math.max(0, Math.min(100, ((v - mn) / (mx - mn)) * 100));
  return `${pct}%`;
}

function computeLeftFromInput(key, value, min = 0, max = 100) {
  try {
    const el = key === 'font' ? fontRangeRef.value : key === 'auto' ? autoRangeRef.value : null;
    if (el && el instanceof HTMLElement) {
      const mn = Number(min || 0);
      const mx = Number(max || 100);
      let v = Number(value);
      if (Number.isNaN(v)) v = mn + (mx - mn) / 2;
      const ratio = mx === mn ? 0.5 : Math.max(0, Math.min(1, (v - mn) / (mx - mn)));
      const leftPx = el.offsetLeft + ratio * el.clientWidth;
      const parent = el.offsetParent || el.parentElement || el;
      const parentWidth = parent && parent.clientWidth ? parent.clientWidth : el.clientWidth || 1;
      const pct = Math.round((leftPx / parentWidth) * 100);
      return `${pct}%`;
    }
  } catch (e) {
    // fallback
  }
  return computeLeftPct(value, min, max);
}

function showPop(key, value, min = 0, max = 100) {
  popActiveKey.value = key;
  popValue.value = Number(value || 0);
  popLeft.value = computeLeftFromInput(key, popValue.value, min, max);
  clearHidePopTimeout();
}

function updatePopPosition(value, min = 0, max = 100) {
  popValue.value = Number(value || 0);
  popLeft.value = computeLeftFromInput(draggingKey.value || popActiveKey.value, popValue.value, min, max);
}

function hidePopIfNotDragging(delay = 300) {
  if (draggingKey.value) return;
  clearHidePopTimeout();
  hidePopTimeout = setTimeout(() => {
    popActiveKey.value = null;
    hidePopTimeout = null;
  }, delay);
}

function onPopMouseEnter() {
  clearHidePopTimeout();
}

function onPopMouseLeave() {
  hidePopIfNotDragging();
}

function onToggleMouseLeave(e) {
  try {
    const to = e && e.relatedTarget;
    const el = e && e.currentTarget;
    if (to && el) {
      const hover = el.querySelector && el.querySelector('.hover-pop');
      if (hover && (hover === to || hover.contains(to) || el === to || el.contains(to))) {
        return;
      }
    }
  } catch (err) {
    // ignore
  }
  hidePopIfNotDragging();
}

function onRangePointerDown(key) {
  draggingKey.value = key;
}

function onRangePointerUp() {
  draggingKey.value = null;
}

function setFontDefault() {
  const def = 100;
  props.patchSetting('readerFontScale', def);
  if (!props.settings.forceReaderFont) props.patchSetting('forceReaderFont', true);
  updatePopPosition(def, 80, 160);
}

function setAutoDefault() {
  const def = 22;
  props.patchSetting('autoScrollSpeed', def);
  if (!props.settings.autoScrollEnabled) props.patchSetting('autoScrollEnabled', true);
  updatePopPosition(def, 5, 80);
}

function onReaderColorInput(e) {
  const v = e.target.value;
  props.patchSetting('readerTextColor', v);
  if (!props.settings.forceReaderTextColor) props.patchSetting('forceReaderTextColor', true);
}

function onFontScaleInput(e) {
  const v = Number(e.target.value || 100);
  props.patchSetting('readerFontScale', v);
  if (!props.settings.forceReaderFont) props.patchSetting('forceReaderFont', true);
  updatePopPosition(v, 80, 160);
}

function onAutoScrollSpeedInput(e) {
  const v = Number(e.target.value || 22);
  props.patchSetting('autoScrollSpeed', v);
  if (!props.settings.autoScrollEnabled) props.patchSetting('autoScrollEnabled', true);
  updatePopPosition(v, 5, 80);
}

onMounted(() => {
  window.addEventListener('pointerup', onRangePointerUp);
  _pmHandler = _onPointerMove;
  window.addEventListener('pointermove', _pmHandler);
});

onBeforeUnmount(() => {
  window.removeEventListener('pointerup', onRangePointerUp);
  if (hideTimeout) {
    clearTimeout(hideTimeout);
    hideTimeout = null;
  }
  if (_pmHandler) {
    window.removeEventListener('pointermove', _pmHandler);
    _pmHandler = null;
  }
});
</script>

<template>
  <div :class="['bottom-toolbar-container', props.toolbarDocked ? 'docked' : 'overlay', props.toolbarDisabled ? 'no-hover' : '']">
    <div
      v-if="!props.toolbarPinned && !props.toolbarVisible && !props.toolbarDisabled && !props.hideHandle"
      class="toolbar-handle"
      @click="props.patchSetting('toolbarVisible', true)"></div>

    <div
      class="bottom-toolbar"
      :class="{ hidden: !props.toolbarVisible, 'icon-mode': props.toolbarIconOnly }">
      <div class="toolbar-left">
        <BaseButton
          class="icon-btn icon-only"
          @click="props.webviewBack"
          title="后退"
          ><el-icon><ArrowLeft /></el-icon
        ></BaseButton>
        <BaseButton
          class="icon-btn icon-only"
          @click="props.webviewForward"
          title="前进"
          ><el-icon><ArrowRight /></el-icon
        ></BaseButton>
        <BaseButton
          class="icon-btn icon-only"
          @click="props.webviewReload"
          title="刷新"
          ><el-icon><Refresh /></el-icon
        ></BaseButton>
      </div>

      <div class="toolbar-center">
        <div
          class="toggle-with-pop"
          @mouseenter="() => showPop('color', props.settings.readerTextColor, 0, 1)"
          @mouseleave="onToggleMouseLeave">
          <BaseButton
            class="icon-btn icon-only"
            :class="{ on: props.settings.forceReaderTextColor }"
            @click="() => props.patchSetting('forceReaderTextColor', !props.settings.forceReaderTextColor)"
            title="文字颜色"
            >A</BaseButton
          >
          <div
            class="hover-pop"
            :class="{ visible: popActiveKey === 'color' || draggingKey === 'color' }"
            @mouseenter="onPopMouseEnter"
            @mouseleave="onPopMouseLeave">
            <div class="range-row">
              <input
                type="color"
                :value="props.settings.readerTextColor"
                @input="onReaderColorInput" />
            </div>
          </div>
        </div>

        <!-- 字号控制 -->
        <div
          class="toggle-with-pop"
          @mouseenter="() => showPop('font', props.settings.readerFontScale, 80, 160)"
          @mouseleave="onToggleMouseLeave">
          <BaseButton
            class="icon-btn icon-only"
            :class="{ on: props.settings.forceReaderFont }"
            @click="() => props.patchSetting('forceReaderFont', !props.settings.forceReaderFont)"
            title="字号"
            >字</BaseButton
          >

          <div
            class="hover-pop"
            :class="{ visible: popActiveKey === 'font' || draggingKey === 'font' }"
            @mouseenter="onPopMouseEnter"
            @mouseleave="onPopMouseLeave">
            <div class="range-row">
              <input
                ref="fontRangeRef"
                data-test="font-range"
                class="mini-range"
                type="range"
                min="80"
                max="160"
                :value="props.settings.readerFontScale"
                @input="onFontScaleInput"
                @pointerdown="() => onRangePointerDown('font')" />
              <div
                class="range-anchor"
                title="回到默认"
                @click="setFontDefault"
                :style="{ left: computeLeftFromInput('font', 100, 80, 160) }"></div>
              <BaseButton
                class="mini-reset-icon"
                title="重置到默认"
                @click="setFontDefault"
                >⟲</BaseButton
              >
              <div class="range-default">默认 100</div>
            </div>
            <div
              v-if="draggingKey === 'font'"
              class="pop-value"
              :style="{ left: popLeft }">
              {{ popValue }}
            </div>
          </div>
        </div>

        <BaseButton
          class="icon-btn icon-only"
          :class="{ on: props.settings.noImageMode }"
          @click="() => props.patchSetting('noImageMode', !props.settings.noImageMode)"
          :title="props.settings.noImageMode ? '显示图片' : '隐藏图片'"
          >🖼</BaseButton
        >
        <BaseButton
          class="icon-btn icon-only"
          :class="{ on: props.settings.showScrollbars }"
          @click="() => props.patchSetting('showScrollbars', !props.settings.showScrollbars)"
          :title="props.settings.showScrollbars ? '隐藏滚动条' : '显示滚动条'"
          >≡</BaseButton
        >

        <div
          class="toggle-with-pop"
          @mouseenter="() => showPop('auto', props.settings.autoScrollSpeed, 5, 80)"
          @mouseleave="onToggleMouseLeave">
          <BaseButton
            class="icon-btn icon-only"
            :class="{ on: props.settings.autoScrollEnabled }"
            @click="() => props.patchSetting('autoScrollEnabled', !props.settings.autoScrollEnabled)"
            :title="props.settings.autoScrollEnabled ? '关闭自动滚动' : '开启自动滚动'"
            >⇳</BaseButton
          >

          <div
            class="hover-pop"
            :class="{ visible: popActiveKey === 'auto' || draggingKey === 'auto' }"
            @mouseenter="onPopMouseEnter"
            @mouseleave="onPopMouseLeave">
            <div class="range-row">
              <input
                ref="autoRangeRef"
                data-test="auto-range"
                class="mini-range"
                type="range"
                min="5"
                max="80"
                :value="props.settings.autoScrollSpeed"
                @input="onAutoScrollSpeedInput"
                @pointerdown="() => onRangePointerDown('auto')" />
              <div
                class="range-anchor"
                title="回到默认"
                @click="setAutoDefault"
                :style="{ left: computeLeftFromInput('auto', 22, 5, 80) }"></div>
              <BaseButton
                class="mini-reset-icon"
                title="重置到默认"
                @click="setAutoDefault"
                >⟲</BaseButton
              >
              <div class="range-default">默认 22</div>
            </div>
            <div
              v-if="draggingKey === 'auto'"
              class="pop-value"
              :style="{ left: popLeft }">
              {{ popValue }}
            </div>
          </div>
        </div>
      </div>

      <div class="toolbar-right">
        <BaseButton
          data-test="zoom-out"
          class="icon-btn icon-only"
          @click="props.zoomOut"
          title="缩小"
          >-</BaseButton
        >
        <span class="zoom-label">{{ Math.round(props.siteZoom * 100) }}%</span>
        <BaseButton
          data-test="zoom-in"
          class="icon-btn icon-only"
          @click="props.zoomIn"
          title="放大"
          >+</BaseButton
        >
        <BaseButton
          data-test="reset-zoom"
          class="icon-btn icon-only"
          @click="props.resetZoom"
          title="重置"
          >1x</BaseButton
        >

        <BaseButton
          data-test="pin-toggle"
          class="icon-btn hide-toolbar-btn icon-only"
          @click="props.toggleToolbarPinned"
          :title="props.settings.toolbarPinned ? '切换到移入显示/移出隐藏' : '切换到始终显示'">
          <component
            :is="props.settings.toolbarPinned ? View : Hide"
            style="width: 18px; height: 18px" />
        </BaseButton>

        <BaseButton
          data-test="close-toolbar"
          class="icon-btn close-toolbar-btn icon-only"
          @click="
            props.disableToolbar ? props.disableToolbar() : (props.patchSetting('toolbarDisabled', true), props.patchSetting('toolbarVisible', false), props.patchSetting('toolbarPinned', false))
          "
          title="关闭工具栏（不再移入显示）">
          <component
            :is="Close"
            style="width: 14px; height: 14px; display: block" />
        </BaseButton>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* toolbar container (overlay/docked) */
.bottom-toolbar-container.overlay {
  position: absolute;
  left: 12px;
  right: 12px;
  bottom: 12px;
  /* 保证容器能接收 hover，并置于 webview 之上 */
  z-index: 10001;
  pointer-events: auto;
}

.bottom-toolbar-container.docked {
  position: fixed;
  left: 12px;
  right: 12px;
  bottom: 12px;
  margin: 0 auto;
  max-width: calc(100% - 24px);
  z-index: 10001;
  pointer-events: auto;
}

.bottom-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border-radius: 8px;
  /* 提升到较高层级，避免被 webview 或热区遮挡 */
  position: relative;
  z-index: 10002;
  background: rgba(255, 255, 255, 0.92);
  backdrop-filter: blur(8px);
  box-shadow: 0 6px 18px rgba(16, 23, 32, 0.08);
}

.bottom-toolbar.hidden {
  transform: translateY(calc(100% + 8px));
  opacity: 0;
  pointer-events: none;
}

/* 悬浮手柄 */
.toolbar-handle {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  bottom: 6px;
  width: 56px;
  height: 8px;
  border-radius: 999px;
  background: linear-gradient(90deg, rgba(0, 0, 0, 0.06), rgba(0, 0, 0, 0.03));
  border: 1px solid rgba(0, 0, 0, 0.06);
  cursor: pointer;
  /* 确保手柄始终在最上层，能被鼠标触达 */
  z-index: 10003;
  box-shadow: 0 6px 18px rgba(16, 23, 32, 0.06);
  transition:
    transform 150ms ease,
    opacity 150ms;
}
.toolbar-handle:hover {
  transform: translateX(-50%) translateY(-3px);
  opacity: 0.98;
}

.toolbar-hotzone {
  /* 使用 fixed 定位，避免容器高度或 transform 导致位置偏移 */
  position: fixed;
  left: 12px;
  right: 12px;
  /* 从视口底部开始，覆盖向上一段距离，确保能从手柄移动到弹层 */
  bottom: 12px;
  height: 220px;
  pointer-events: auto;
  background: transparent;
  z-index: 10002;
}

.bottom-toolbar-container:hover .bottom-toolbar.hidden {
  transform: translateY(0);
  opacity: 1;
  pointer-events: auto;
}
.bottom-toolbar-container.no-hover:hover .bottom-toolbar.hidden {
  transform: translateY(calc(100% + 8px));
  opacity: 0;
  pointer-events: none;
}

.toolbar-left,
.toolbar-center,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}
.zoom-label {
  min-width: 48px;
  text-align: center;
  font-weight: 600;
}
.mini-range {
  width: 110px;
  height: 28px;
}
.hover-pop {
  position: absolute;
  bottom: calc(100% + 8px);
  left: 50%;
  transform: translateX(-50%) translateY(6px);
  opacity: 0;
  pointer-events: none;
  transition:
    opacity 0.12s ease,
    transform 0.12s ease;
  background: rgba(255, 255, 255, 0.98);
  padding: 8px;
  border-radius: 8px;
  box-shadow: 0 8px 24px rgba(16, 23, 32, 0.12);
  z-index: 10004;
}
.hover-pop.visible,
.toggle-with-pop:hover .hover-pop {
  opacity: 1;
  pointer-events: auto;
  transform: translateX(-50%) translateY(0);
}
.pop-value {
  position: absolute;
  top: -26px;
  transform: translateX(-50%);
  background: rgba(0, 0, 0, 0.8);
  color: #fff;
  padding: 3px 6px;
  border-radius: 4px;
  font-size: 12px;
  z-index: 10005;
}

/* icon/button visuals */
.icon-btn {
  border: 0;
  background: transparent;
  padding: 6px 8px;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  transition:
    background 120ms ease,
    transform 120ms ease;
}
.icon-btn.on {
  background: rgba(64, 158, 255, 0.1);
  color: #409eff;
}
.close-toolbar-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  padding: 6px;
  border-radius: 10px;
  border: 1px solid rgba(224, 72, 66, 0.06);
  background: rgba(255, 255, 255, 0.92);
  color: #e04842;
  box-shadow: 0 2px 6px rgba(16, 23, 32, 0.04);
  transition:
    background 150ms ease,
    transform 120ms ease;
}
.close-toolbar-btn:hover {
  background: rgba(224, 72, 66, 0.06);
  transform: translateY(-2px);
}

.mini-reset-icon {
  border: 0;
  background: transparent;
  width: 28px;
  height: 28px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  color: #444;
}
.mini-reset-icon:hover {
  background: rgba(0, 0, 0, 0.06);
}

.range-row {
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
}
.range-anchor {
  position: absolute;
  top: 50%;
  transform: translate(-50%, -50%);
  width: 12px;
  height: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
.range-anchor::before {
  content: '';
  display: block;
  width: 2px;
  height: 10px;
  background: rgba(64, 158, 255, 0.9);
  border-radius: 2px;
}
.range-anchor:hover::before {
  background: #2b88ff;
}
</style>
