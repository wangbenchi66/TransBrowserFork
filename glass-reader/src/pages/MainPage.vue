<script setup>
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { View, Hide } from '@element-plus/icons-vue';
import { useDesktopApp } from '../composables/useDesktopApp';
import recommendedPage from './RecommendedPage.vue';

const { settings, activeTab, activeTabId, tabs, addNewTab, selectTab, closeTab, patchSetting } = useDesktopApp();

const webviewRef = ref(null);
const webviewReady = ref(false);
const localReaderRef = ref(null);
let localScrollTimer = null;
const siteZoom = ref(1);
const popActiveKey = ref(null);
const popValue = ref(0);
const popLeft = ref('50%');
const draggingKey = ref(null);
const fontRangeRef = ref(null);
const autoRangeRef = ref(null);

const transparentPageCss = `
html,
body,
#app,
#root,
#__next,
#__nuxt {
  background: transparent !important;
  background-color: transparent !important;
}

html::before,
html::after,
body::before,
body::after {
  background: transparent !important;
}
`;

// 更强力的透明样式（保留图片/视频/canvas/svg）
const transparentPageCssAggressive = `
html,
body,
#app,
#root,
#__next,
#__nuxt {
  background: transparent !important;
  background-color: transparent !important;
}

html::before,
html::after,
body::before,
body::after {
  background: transparent !important;
}

*:not(img):not(picture):not(video):not(canvas):not(svg) {
  background: transparent !important;
  background-image: none !important;
  box-shadow: none !important;
}
`;

const noImageCss = `
img, picture, video, canvas, svg {
  visibility: hidden !important;
}
`;

function buildStyleScript(styleId, enabled, cssText) {
  return `(() => {
    const id = ${JSON.stringify(styleId)};
    const css = ${JSON.stringify(cssText)};
    let styleNode = document.getElementById(id);

    if (${enabled ? 'true' : 'false'}) {
      if (!styleNode) {
        styleNode = document.createElement('style');
        styleNode.id = id;
        document.documentElement.appendChild(styleNode);
      }

      styleNode.textContent = css;
      return true;
    }

    if (styleNode) {
      styleNode.remove();
    }

    return true;
  })();`;
}

function buildAutoScrollScript(enabled, speed) {
  const interval = Math.max(16, Math.round(320 - speed * 3));
  const step = Math.max(1, speed / 6);

  return `(() => {
    if (window.__glassReaderAutoScrollTimer) {
      window.clearInterval(window.__glassReaderAutoScrollTimer);
      window.__glassReaderAutoScrollTimer = null;
    }

    if (!${enabled ? 'true' : 'false'}) {
      return true;
    }

    window.__glassReaderAutoScrollTimer = window.setInterval(() => {
      const root = document.scrollingElement || document.documentElement || document.body;
      if (!root) {
        return;
      }

      root.scrollBy(0, ${step});
    }, ${interval});

    return true;
  })();`;
}

function computeLeftPct(value, min, max) {
  const v = Number(value || 0);
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
      const ratio = mx === mn ? 0.5 : Math.max(0, Math.min(1, (Number(value || 0) - mn) / (mx - mn)));
      const leftPx = el.offsetLeft + ratio * el.clientWidth;
      return `${Math.round(leftPx)}px`;
    }
  } catch (e) {
    // fallback to percent
  }
  return computeLeftPct(value, min, max);
}

function showPop(key, value, min = 0, max = 100) {
  popActiveKey.value = key;
  popValue.value = Number(value || 0);
  popLeft.value = computeLeftFromInput(key, popValue.value, min, max);
}

function updatePopPosition(value, min = 0, max = 100) {
  popValue.value = Number(value || 0);
  popLeft.value = computeLeftFromInput(draggingKey.value || popActiveKey.value, popValue.value, min, max);
}

function hidePopIfNotDragging() {
  if (!draggingKey.value) popActiveKey.value = null;
}

function onRangePointerDown(key) {
  draggingKey.value = key;
}

function onRangePointerUp() {
  draggingKey.value = null;
}

function setFontDefault() {
  const def = 100;
  patchSetting('readerFontScale', def);
  if (!settings.forceReaderFont) patchSetting('forceReaderFont', true);
  updatePopPosition(def, 80, 160);
  syncWebviewFontSize();
  applyLocalReaderStyles();
}

function setAutoDefault() {
  const def = 22;
  patchSetting('autoScrollSpeed', def);
  if (!settings.autoScrollEnabled) patchSetting('autoScrollEnabled', true);
  updatePopPosition(def, 5, 80);
  syncWebviewAutoScroll();
  syncLocalReaderAutoScroll();
}

function toggleToolbarPinned() {
  if (settings.toolbarPinned) {
    // 切换到移入显示模式：取消固定并隐藏工具栏（由 hover/handle 控制显示）
    patchSetting('toolbarPinned', false);
    patchSetting('toolbarVisible', false);
  } else {
    // 切换到固定显示模式：固定并显示工具栏
    patchSetting('toolbarPinned', true);
    patchSetting('toolbarVisible', true);
  }
}

// 原始的 JS 注入方法（用于自动滚动等需要执行脚本的场景）
function runWebviewJS(script) {
  const webview = webviewRef.value;
  if (!webview || activeTab.value.kind === 'dashboard' || activeTab.value.kind === 'local-text') {
    return;
  }

  if (!webviewReady.value) return;

  try {
    webview.executeJavaScript(script).catch(() => {});
  } catch (e) {
    // ignore synchronous errors when webview not ready
  }
}

// 简单的 webview 导航与缩放辅助
function webviewBack() {
  const w = webviewRef.value;
  try {
    if (w && typeof w.goBack === 'function') w.goBack();
  } catch (e) {}
}

function webviewForward() {
  const w = webviewRef.value;
  try {
    if (w && typeof w.goForward === 'function') w.goForward();
  } catch (e) {}
}

function webviewReload() {
  const w = webviewRef.value;
  try {
    if (w && typeof w.reload === 'function') w.reload();
  } catch (e) {}
}

async function setSiteZoom(factor) {
  siteZoom.value = Math.max(0.3, Math.min(3, Number(factor) || 1));
  const w = webviewRef.value;
  try {
    if (w && typeof w.setZoomFactor === 'function') {
      await w.setZoomFactor(siteZoom.value);
    } else {
      const script = `window.__glass_reader_zoom = ${siteZoom.value}; document.body.style.zoom = ${siteZoom.value};`;
      runWebviewJS(script);
    }
  } catch (e) {}
}

function zoomIn() {
  setSiteZoom(siteZoom.value + 0.1);
}
function zoomOut() {
  setSiteZoom(siteZoom.value - 0.1);
}
function resetZoom() {
  setSiteZoom(1);
}

// 存储通过 insertCSS 注入后返回的 key，便于后续移除和替换
const insertedCssKeys = {};

// 向 webview 注入或替换样式，优先使用 insertCSS（可绕过 CSP），无法使用时回退到 style 标签注入
async function runWebviewCss(styleId, css) {
  const webview = webviewRef.value;
  if (!webview || activeTab.value.kind === 'dashboard' || activeTab.value.kind === 'local-text') {
    return;
  }
  try {
    // 如果 webview 未准备好，跳过所有对 webview API 的调用，避免在 dom-ready 前触发错误
    if (!webviewReady.value) return;

    // 如果之前注入过，尝试移除旧样式
    const prevKey = insertedCssKeys[styleId];
    if (prevKey && typeof webview.removeInsertedCSS === 'function') {
      try {
        await webview.removeInsertedCSS(prevKey);
      } catch (e) {
        // ignore
      }
      insertedCssKeys[styleId] = null;
    }

    if (!css) {
      // css 为空意味着只需移除旧样式
      // 作为回退，也移除可能存在的 style 标签
      try {
        const rmScript = `(function(){ const n=document.getElementById(${JSON.stringify(styleId)}); if(n) n.remove(); })();`;
        await webview.executeJavaScript(rmScript);
      } catch (e) {}
      return;
    }

    // 优先通过 insertCSS 注入样式
    if (typeof webview.insertCSS === 'function') {
      try {
        const key = await webview.insertCSS(css);
        insertedCssKeys[styleId] = key;
        return;
      } catch (e) {
        // 回退到 style 注入
      }
    }

    // 回退：通过在页面中插入/替换 style 标签（可能受 CSP 限制）
    const script = buildStyleScript(styleId, true, css);
    try {
      await webview.executeJavaScript(script);
    } catch (e) {
      // ignore
    }
  } catch (e) {
    // ignore overall
  }
}

function syncWebviewTransparency() {
  const enabled = settings.pageTransparentMode || settings.forcePageTransparent || settings.fullWindowTransparent;
  const css = settings.forcePageTransparent || settings.fullWindowTransparent ? transparentPageCssAggressive : transparentPageCss;
  runWebviewCss('glass-transparent-style', enabled ? css : '');
}
function syncWebviewNoImage() {
  runWebviewCss('glass-no-image-style', settings.noImageMode ? noImageCss : '');
}

function syncWebviewScrollbars() {
  const css = `
    ::-webkit-scrollbar { width: 12px !important; height: 12px !important; }
    ::-webkit-scrollbar-track { background: transparent !important; }
    ::-webkit-scrollbar-thumb { background: rgba(0,0,0,0.2) !important; border-radius: 6px !important; }
  `;

  // 当 settings.showScrollbars 为 false 时，隐藏网页滚动条
  const hideCss = `
    ::-webkit-scrollbar { display: none !important; }
  `;

  runWebviewCss('glass-scrollbar-style', settings.showScrollbars ? css : hideCss);
}

function syncWebviewTextColor() {
  if (!settings.forceReaderTextColor) {
    runWebviewCss('glass-force-text-color', '');
    return;
  }

  const color = settings.readerTextColor || '#283247';
  const css = `
    body, body * { color: ${color} !important; background: transparent !important; }
    a, a * { color: inherit !important; }
  `;

  runWebviewCss('glass-force-text-color', css);
}

function syncWebviewFontSize() {
  if (!settings.forceReaderFont) {
    runWebviewCss('glass-force-font-size', '');
    return;
  }

  const scale = settings.readerFontScale || 100;
  const css = `
    html, body, body * { font-size: ${scale}% !important; }
  `;

  runWebviewCss('glass-force-font-size', css);
}

function syncWebviewAutoScroll() {
  runWebviewJS(buildAutoScrollScript(settings.autoScrollEnabled, settings.autoScrollSpeed));
}

function stopLocalReaderAutoScroll() {
  if (localScrollTimer) {
    window.clearInterval(localScrollTimer);
    localScrollTimer = null;
  }
}

function syncLocalReaderAutoScroll() {
  stopLocalReaderAutoScroll();

  if (!settings.autoScrollEnabled || activeTab.value.kind !== 'local-text' || !localReaderRef.value) {
    return;
  }

  const interval = Math.max(16, Math.round(320 - settings.autoScrollSpeed * 3));
  const step = Math.max(1, settings.autoScrollSpeed / 6);
  localScrollTimer = window.setInterval(() => {
    if (localReaderRef.value) {
      localReaderRef.value.scrollBy({ top: step, behavior: 'auto' });
    }
  }, interval);
}

function syncReaderEffects() {
  syncWebviewTransparency();
  syncWebviewNoImage();
  syncWebviewScrollbars();
  syncWebviewTextColor();
  syncWebviewFontSize();
  syncWebviewAutoScroll();
  syncLocalReaderAutoScroll();
  // 同步本地阅读器样式以保证立即可见
  applyLocalReaderStyles();
}

// 将样式立即应用到本地阅读视图（local-text），保证在滑动/选色时有即时反馈
function applyLocalReaderStyles() {
  try {
    if (!localReaderRef.value) return;

    // 文字颜色
    if (settings.forceReaderTextColor) {
      localReaderRef.value.style.color = settings.readerTextColor || '';
    } else {
      localReaderRef.value.style.color = '';
    }

    // 字号（通过根元素比例控制）
    if (settings.forceReaderFont) {
      localReaderRef.value.style.fontSize = `${settings.readerFontScale}%`;
    } else {
      localReaderRef.value.style.fontSize = '';
    }
  } catch (e) {
    // ignore
  }
}

function onReaderColorInput(e) {
  const v = e.target.value;
  patchSetting('readerTextColor', v);
  if (!settings.forceReaderTextColor) patchSetting('forceReaderTextColor', true);
  // 立即生效
  syncWebviewTextColor();
  applyLocalReaderStyles();
}

function onFontScaleInput(e) {
  const v = Number(e.target.value || 100);
  patchSetting('readerFontScale', v);
  if (!settings.forceReaderFont) patchSetting('forceReaderFont', true);
  // 立即生效
  syncWebviewFontSize();
  applyLocalReaderStyles();
  // 更新弹窗位置与值
  updatePopPosition(v, 80, 160);
}

function onAutoScrollSpeedInput(e) {
  const v = Number(e.target.value || 22);
  patchSetting('autoScrollSpeed', v);
  if (!settings.autoScrollEnabled) patchSetting('autoScrollEnabled', true);
  // 立即生效
  syncWebviewAutoScroll();
  syncLocalReaderAutoScroll();
  // 更新弹窗位置与值
  updatePopPosition(v, 5, 80);
}

function handleWebviewDomReady() {
  webviewReady.value = true;
  syncReaderEffects();
  // 尝试读取并同步当前页面缩放比例
  const w = webviewRef.value;
  try {
    if (w && typeof w.getZoomFactor === 'function') {
      w.getZoomFactor()
        .then((f) => {
          siteZoom.value = f || 1;
        })
        .catch(() => {});
    } else {
      // 回退到页面变量
      w.executeJavaScript('window.__glass_reader_zoom || 1')
        .then((f) => {
          siteZoom.value = f || 1;
        })
        .catch(() => {});
    }
  } catch (e) {}
}

watch(() => settings.pageTransparentMode, syncReaderEffects);
// 监听强制网页透明开关，确保其变更能立即同步到 webview
watch(() => settings.forcePageTransparent, syncReaderEffects);
watch(() => settings.noImageMode, syncReaderEffects);
watch(() => settings.autoScrollEnabled, syncReaderEffects);
watch(() => settings.autoScrollSpeed, syncReaderEffects);
watch(
  () => settings.readerTextColor,
  (v) => {
    syncWebviewTextColor();
    applyLocalReaderStyles();
  }
);
watch(
  () => settings.forceReaderTextColor,
  (v) => {
    syncWebviewTextColor();
    applyLocalReaderStyles();
  }
);
watch(() => settings.showScrollbars, syncWebviewScrollbars);
watch(
  () => settings.readerFontScale,
  (v) => {
    syncWebviewFontSize();
    applyLocalReaderStyles();
  }
);
watch(
  () => settings.forceReaderFont,
  (v) => {
    syncWebviewFontSize();
    applyLocalReaderStyles();
  }
);

watch(
  () => activeTabId.value,
  async () => {
    // 切换 tab 时重置 ready 状态，等待新的 webview dom-ready
    webviewReady.value = false;
    await nextTick();
    syncReaderEffects();
  }
);

onBeforeUnmount(() => {
  stopLocalReaderAutoScroll();
  window.removeEventListener('pointerup', onRangePointerUp);
});

onMounted(() => {
  window.addEventListener('pointerup', onRangePointerUp);
});
</script>

<template>
  <section class="main-page">
    <section
      v-if="settings.showTabBar"
      class="tabbar panel no-drag">
      <div class="tabs">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          class="tab-chip"
          :class="{ active: tab.id === activeTabId }"
          @click="selectTab(tab.id)">
          <span>{{ tab.title }}</span>
          <span
            class="close-mark"
            @click.stop="closeTab(tab.id)">
            x
          </span>
        </button>

        <button
          class="tab-add"
          @click="addNewTab">
          +
        </button>
      </div>
    </section>

    <section class="browser-stage">
      <div class="workspace-shell">
        <section class="reader-stage">
          <!-- <div class="reader-toolbar panel no-drag">
            <div>
              <strong>{{ activeTab.title }}</strong>
              <span>{{ activeTab.subtitle }}</span>
            </div>
            <div class="reader-badges">
              <span class="reader-badge">{{ activeTabMeta }}</span>
              <span class="reader-badge">透明度 {{ settings.transparency }}%</span>
              <span class="reader-badge">自动滚动 {{ settings.autoScrollEnabled ? '开启' : '关闭' }}</span>
            </div>
          </div> -->

          <div
            class="page-frame panel no-drag"
            :class="{ 'is-webview': activeTab.kind !== 'dashboard' }">
            <template v-if="activeTab.kind === 'dashboard'">
              <recommended-page />
            </template>

            <template v-else-if="activeTab.kind === 'local-text'">
              <article
                ref="localReaderRef"
                class="local-reader">
                <header class="local-reader-head">
                  <strong>{{ activeTab.fileName }}</strong>
                  <span>本地文本阅读视图</span>
                </header>
                <pre class="local-reader-content">{{ activeTab.content }}</pre>
              </article>
            </template>

            <template v-else>
              <div class="webview-wrap">
                <!-- <div class="webview-meta">
                  <strong>{{ activeTab.title }}</strong>
                  <span>{{ activeTab.url }}</span>
                </div> -->
                <webview
                  :key="activeTab.id"
                  ref="webviewRef"
                  class="webview-frame"
                  :src="activeTab.url"
                  nodeintegration="false"
                  enableblinkfeatures="ResizeObserver"
                  @dom-ready="handleWebviewDomReady"
                  allowpopups></webview>
              </div>
            </template>
            <!-- 底部工具栏（网页模式下显示） -->
            <div
              v-if="activeTab.kind !== 'dashboard' && activeTab.kind !== 'local-text'"
              :class="['bottom-toolbar-container', settings.toolbarDocked ? 'docked' : 'overlay']">

              <div v-if="!settings.toolbarPinned && !settings.toolbarVisible" class="toolbar-handle" @click="patchSetting('toolbarVisible', true)"></div>

              <div class="bottom-toolbar" :class="{ hidden: !settings.toolbarVisible }">
                <div class="toolbar-left">
                  <button class="icon-btn" @click="webviewBack" title="后退">◀</button>
                  <button class="icon-btn" @click="webviewForward" title="前进">▶</button>
                  <button class="icon-btn" @click="webviewReload" title="刷新">⟳</button>
                </div>

                <div class="toolbar-center">
                  <div class="toggle-with-pop" @mouseenter="() => showPop('color', settings.readerTextColor, 0, 1)" @mouseleave="hidePopIfNotDragging">
                    <button class="icon-btn" :class="{ on: settings.forceReaderTextColor }" @click="patchSetting('forceReaderTextColor', !settings.forceReaderTextColor)" title="强制文字色">字</button>
                    <div class="hover-pop" :class="{ visible: popActiveKey === 'color' || draggingKey === 'color' }">
                      <input class="mini-color" type="color" :value="settings.readerTextColor" @input="onReaderColorInput" />
                    </div>
                  </div>

                  <div class="toggle-with-pop" @mouseenter="() => showPop('font', settings.readerFontScale, 80, 160)" @mouseleave="hidePopIfNotDragging">
                    <button class="icon-btn" :class="{ on: settings.forceReaderFont }" @click="patchSetting('forceReaderFont', !settings.forceReaderFont)" title="强制字号">大</button>
                    <div class="hover-pop" :class="{ visible: popActiveKey === 'font' || draggingKey === 'font' }">
                      <input ref="fontRangeRef" class="mini-range" type="range" min="80" max="160" :value="settings.readerFontScale" @input="onFontScaleInput" @pointerdown="() => onRangePointerDown('font')" />
                      <div class="range-default">默认 100%</div>
                      <div class="range-anchor" title="回到默认" @click="setFontDefault" :style="{ left: computeLeftFromInput('font', 100, 80, 160) }"></div>
                      <div v-if="draggingKey === 'font'" class="pop-value" :style="{ left: popLeft }">{{ popValue }}%</div>
                    </div>
                  </div>

                  <button class="icon-btn" :class="{ on: settings.noImageMode }" @click="patchSetting('noImageMode', !settings.noImageMode)" title="显示/隐藏图片">🖼</button>
                  <button class="icon-btn" :class="{ on: settings.showScrollbars }" @click="patchSetting('showScrollbars', !settings.showScrollbars)" title="显示/隐藏滚动条">≡</button>

                  <div class="toggle-with-pop" @mouseenter="() => showPop('auto', settings.autoScrollSpeed, 5, 80)" @mouseleave="hidePopIfNotDragging">
                    <button class="icon-btn" :class="{ on: settings.autoScrollEnabled }" @click="patchSetting('autoScrollEnabled', !settings.autoScrollEnabled)" title="自动滚动">⇵</button>
                    <div class="hover-pop" :class="{ visible: popActiveKey === 'auto' || draggingKey === 'auto' }">
                      <input ref="autoRangeRef" class="mini-range" type="range" min="5" max="80" :value="settings.autoScrollSpeed" @input="onAutoScrollSpeedInput" @pointerdown="() => onRangePointerDown('auto')" />
                      <div class="range-default">默认 22</div>
                      <div class="range-anchor" title="回到默认" @click="setAutoDefault" :style="{ left: computeLeftFromInput('auto', 22, 5, 80) }"></div>
                      <div v-if="draggingKey === 'auto'" class="pop-value" :style="{ left: popLeft }">{{ popValue }}</div>
                    </div>
                  </div>
                </div>

                <div class="toolbar-right">
                  <button class="icon-btn" @click="zoomOut" title="缩小">-</button>
                  <span class="zoom-label">{{ Math.round(siteZoom * 100) }}%</span>
                  <button class="icon-btn" @click="zoomIn" title="放大">+</button>
                  <button class="icon-btn" @click="resetZoom" title="重置">1x</button>
                  <button class="icon-btn hide-toolbar-btn" @click="toggleToolbarPinned" :title="settings.toolbarPinned ? '切换到移入显示/移出隐藏' : '切换到始终显示'">
                    <component :is="settings.toolbarPinned ? View : Hide" style="width:18px;height:18px;" />
                  </button>
                </div>
              </div>
            </div>
          </div>
        </section>
      </div>
    </section>
  </section>
</template>

<style scoped>
.page-frame {
  position: relative;
}

/* toolbar base: layout only - positioning moved to .overlay / .docked */
.bottom-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border-radius: 8px;
  z-index: 60;
  transition:
    transform 0.18s ease,
    opacity 0.18s ease;
}

/* overlay: positioned inside page frame, floats above webview */
.bottom-toolbar.overlay {
  position: absolute;
  left: 12px;
  right: 12px;
  bottom: 12px;
  background: rgba(255, 255, 255, 0.86);
  backdrop-filter: blur(6px);
  box-shadow: 0 6px 18px rgba(16, 23, 32, 0.08);
}

/* docked: fixed to application bottom (outside webview), spans width */
.bottom-toolbar-container.overlay {
  position: absolute;
  left: 12px;
  right: 12px;
  bottom: 12px;
}

/* docked: fixed to application bottom (outside webview), spans width */
.bottom-toolbar-container.docked {
  position: fixed;
  left: 12px;
  right: 12px;
  bottom: 12px;
  margin: 0 auto;
  max-width: calc(100% - 24px);
}

/* visual shell for the toolbar itself */
.bottom-toolbar {
  background: rgba(255,255,255,0.92);
  backdrop-filter: blur(8px);
  box-shadow: 0 6px 18px rgba(16,23,32,0.08);
}

/* 当隐藏时移动到视图外，但容器保留小手柄触发悬浮显示 */
.bottom-toolbar.hidden {
  transform: translateY(calc(100% + 8px));
  opacity: 0;
  pointer-events: none;
}

/* 悬浮手柄：在工具栏隐藏时保留用于移入显示或点击展开 */
.toolbar-handle {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  bottom: 6px;
  width: 40px;
  height: 6px;
  border-radius: 6px;
  background: rgba(0,0,0,0.08);
  cursor: pointer;
  z-index: 1200;
}
.bottom-toolbar-container:hover .bottom-toolbar.hidden { transform: translateY(0); opacity: 1; pointer-events: auto; }

.icon-btn {
  border: 0;
  background: transparent;
  padding: 6px 8px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
}

.icon-btn.on {
  background: rgba(64, 158, 255, 0.1);
  color: #409eff;
}
.zoom-label {
  min-width: 48px;
  text-align: center;
  font-weight: 600;
}

/* mini color input */
.mini-color {
  width: 28px;
  height: 28px;
  padding: 0;
  border: 1px solid rgba(0, 0, 0, 0.06);
  border-radius: 6px;
  cursor: pointer;
  background: none;
  -webkit-appearance: none;
  appearance: none;
  box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.03);
}
.mini-color::-moz-color-swatch {
  border-radius: 6px;
  border: none;
}

/* mini range slider */
.mini-range {
  width: 110px;
  height: 28px;
  background: transparent;
  -webkit-appearance: none;
  appearance: none;
}
.mini-range::-webkit-slider-runnable-track {
  height: 6px;
  background: rgba(0, 0, 0, 0.06);
  border-radius: 6px;
}
.mini-range::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 14px;
  height: 14px;
  margin-top: -4px;
  background: #409eff;
  border-radius: 50%;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
  cursor: pointer;
}
.mini-range::-moz-range-track {
  height: 6px;
  background: rgba(0, 0, 0, 0.06);
  border-radius: 6px;
}
.mini-range::-moz-range-thumb {
  width: 14px;
  height: 14px;
  background: #409eff;
  border-radius: 50%;
  border: none;
}
.mini-range:focus {
  outline: none;
}
.mini-color:focus {
  outline: none;
  box-shadow: 0 0 0 3px rgba(64, 158, 255, 0.12);
}

/* wrapper for inline small sliders */
.mini-range-wrap {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  margin-left: 6px;
}
.range-default {
  font-size: 11px;
  color: rgba(0, 0, 0, 0.46);
}

/* hover pop for toggles */
.toggle-with-pop {
  position: relative;
  display: inline-flex;
  align-items: center;
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
  min-width: 140px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  align-items: center;
}
.hover-pop.visible,
.toggle-with-pop:hover .hover-pop {
  opacity: 1;
  pointer-events: auto;
  transform: translateX(-50%) translateY(0);
}
.hover-pop .mini-range {
  width: 160px;
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
  white-space: nowrap;
  pointer-events: none;
}

/* default anchor on slider track */
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

/* hide button in toolbar */
.hide-toolbar-btn {
  margin-left: 8px;
  padding: 6px 10px;
  border-radius: 6px;
  background: rgba(0,0,0,0.04);
  color: rgba(0,0,0,0.75);
  font-weight: 600;
}
.hide-toolbar-btn:hover { background: rgba(0,0,0,0.08); }
</style>
