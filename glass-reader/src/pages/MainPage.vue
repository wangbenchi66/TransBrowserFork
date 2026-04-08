<script setup>
import { nextTick, onBeforeUnmount, ref, watch } from 'vue';
import { useDesktopApp } from '../composables/useDesktopApp';
import recommendedPage from './RecommendedPage.vue';

const { settings, activeTab, activeTabId, tabs, addNewTab, selectTab, closeTab } = useDesktopApp();

const webviewRef = ref(null);
const webviewReady = ref(false);
const localReaderRef = ref(null);
let localScrollTimer = null;

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
  const enabled = settings.pageTransparentMode || settings.forcePageTransparent;
  const css = settings.forcePageTransparent ? transparentPageCssAggressive : transparentPageCss;
  runWebviewCss('glass-transparent-style', enabled ? css : '');
}

function syncWebviewScrollbars() {
  // 已移除：不再向 webview 注入滚动条样式，webview 保持原生行为
}

function syncWebviewNoImage() {
  runWebviewCss('glass-no-image-style', settings.noImageMode ? noImageCss : '');
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
  syncWebviewAutoScroll();
  syncLocalReaderAutoScroll();
}

function handleWebviewDomReady() {
  webviewReady.value = true;
  syncReaderEffects();
}

watch(() => settings.pageTransparentMode, syncReaderEffects);
// 监听强制网页透明开关，确保其变更能立即同步到 webview
watch(() => settings.forcePageTransparent, syncReaderEffects);
watch(() => settings.noImageMode, syncReaderEffects);
watch(() => settings.autoScrollEnabled, syncReaderEffects);
watch(() => settings.autoScrollSpeed, syncReaderEffects);

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
          </div>
        </section>
      </div>
    </section>
  </section>
</template>
