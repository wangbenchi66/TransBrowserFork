<script setup>
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue';
import { useDesktopApp } from '../composables/useDesktopApp';

const {
  settings,
  quickLinks,
  filteredRecommendedSites,
  favoriteSites,
  localDocuments,
  urlInput,
  searchKeyword,
  siteSearchKeyword,
  customSiteName,
  customSiteUrl,
  activeTab,
  activeTabId,
  tabs,
  filteredRecentVisits,
  dashboardMetrics,
  controlToggleKeys,
  patchSetting,
  toggleSetting,
  handleOpenUrl,
  addNewTab,
  selectTab,
  closeTab,
  useQuickLink,
  useFavoriteSite,
  useRecommendedSite,
  openRecentVisit,
  addFavoriteSite,
  removeFavoriteSite,
  uploadLocalFiles,
  openLocalDocument,
  removeLocalDocument
} = useDesktopApp();

const webviewRef = ref(null);
const localReaderRef = ref(null);
const fileInputRef = ref(null);
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

const activeTabMeta = computed(() => {
  if (activeTab.value.kind === 'dashboard') {
    return '工作台';
  }

  if (activeTab.value.kind === 'local-text') {
    return '本地文档';
  }

  return '在线网页';
});

const recommendedPreview = computed(() => filteredRecommendedSites.value.slice(0, 8));

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

function runWebviewScript(script) {
  const webview = webviewRef.value;
  if (!webview || activeTab.value.kind === 'dashboard' || activeTab.value.kind === 'local-text') {
    return;
  }

  webview.executeJavaScript(script).catch(() => {});
}

function syncWebviewTransparency() {
  const enabled = settings.pageTransparentMode || settings.forcePageTransparent;
  const css = settings.forcePageTransparent ? transparentPageCssAggressive : transparentPageCss;
  runWebviewScript(buildStyleScript('glass-transparent-style', enabled, css));
}

function syncWebviewNoImage() {
  runWebviewScript(buildStyleScript('glass-no-image-style', settings.noImageMode, noImageCss));
}

function syncWebviewAutoScroll() {
  runWebviewScript(buildAutoScrollScript(settings.autoScrollEnabled, settings.autoScrollSpeed));
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
  syncReaderEffects();
}

function triggerFileSelect() {
  fileInputRef.value?.click();
}

async function handleFileChange(event) {
  await uploadLocalFiles(event.target.files);
  event.target.value = '';
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
      <div class="nav-row panel no-drag">
        <input
          v-model="urlInput"
          class="url-input"
          placeholder="输入网址，回车打开任意网站..."
          @keydown.enter="handleOpenUrl" />
        <button
          class="open-btn"
          @click="handleOpenUrl">
          打开
        </button>
        <button
          class="secondary-btn"
          @click="triggerFileSelect">
          导入文件
        </button>
        <input
          ref="fileInputRef"
          class="hidden-input"
          type="file"
          multiple
          @change="handleFileChange" />
      </div>

      <div class="workspace-shell">
        <aside class="workspace-sidebar panel no-drag">
          <section class="workspace-block hero-block">
            <span class="hero-label">工作台</span>
            <h1>透明网页、本地文档、常用站点一体化</h1>
            <p>参考墨鱼阅读的核心思路，整理成左侧入口库、中间阅读区、右侧控制台的桌面工作流。</p>

            <div class="metric-grid">
              <article
                v-for="metric in dashboardMetrics"
                :key="metric.label"
                class="metric-card">
                <strong>{{ metric.value }}</strong>
                <span>{{ metric.label }}</span>
              </article>
            </div>
          </section>

          <section class="workspace-block">
            <div class="block-head">
              <strong>推荐站点</strong>
              <input
                v-model="siteSearchKeyword"
                class="mini-input"
                placeholder="搜索站点" />
            </div>

            <div class="recommend-grid">
              <button
                v-for="site in recommendedPreview"
                :key="site.name"
                class="recommend-card"
                @click="useRecommendedSite(site)">
                <span
                  class="site-mark"
                  :class="site.tone">
                  {{ site.tag }}
                </span>
                <div>
                  <strong>{{ site.name }}</strong>
                  <small>{{ site.category }} · {{ site.hint }}</small>
                </div>
              </button>
            </div>
          </section>

          <section class="workspace-block">
            <div class="block-head">
              <strong>我的站点</strong>
              <span>{{ favoriteSites.length }}/12</span>
            </div>

            <div class="favorite-form">
              <input
                v-model="customSiteName"
                class="mini-input"
                placeholder="站点名称" />
              <input
                v-model="customSiteUrl"
                class="mini-input"
                placeholder="https://example.com" />
              <button
                class="secondary-btn compact"
                @click="addFavoriteSite">
                添加
              </button>
            </div>

            <div class="chip-list">
              <button
                v-for="site in favoriteSites"
                :key="site.id"
                class="site-chip"
                @click="useFavoriteSite(site)">
                <span>{{ site.name }}</span>
                <em @click.stop="removeFavoriteSite(site.id)">x</em>
              </button>
            </div>
          </section>

          <section class="workspace-block">
            <div class="block-head">
              <strong>本地文档</strong>
              <button
                class="text-link"
                @click="triggerFileSelect">
                上传
              </button>
            </div>

            <div
              v-if="localDocuments.length"
              class="doc-list">
              <button
                v-for="doc in localDocuments"
                :key="doc.id"
                class="doc-item"
                @click="openLocalDocument(doc)">
                <div>
                  <strong>{{ doc.fileName }}</strong>
                  <small>{{ doc.type === 'local-text' ? '文本阅读' : '浏览器内核打开' }}</small>
                </div>
                <em @click.stop="removeLocalDocument(doc.id)">x</em>
              </button>
            </div>
            <div
              v-else
              class="empty-hint">
              支持导入 txt、md、json 等文本文件，其他文件将尝试用浏览器内核打开。
            </div>
          </section>
        </aside>

        <section class="reader-stage">
          <div class="reader-toolbar panel no-drag">
            <div>
              <strong>{{ activeTab.title }}</strong>
              <span>{{ activeTab.subtitle }}</span>
            </div>
            <div class="reader-badges">
              <span class="reader-badge">{{ activeTabMeta }}</span>
              <span class="reader-badge">透明度 {{ settings.transparency }}%</span>
              <span class="reader-badge">自动滚动 {{ settings.autoScrollEnabled ? '开启' : '关闭' }}</span>
            </div>
          </div>

          <div
            class="page-frame panel no-drag"
            :class="{ 'is-webview': activeTab.kind !== 'dashboard' }">
            <template v-if="activeTab.kind === 'dashboard'">
              <div class="dashboard-grid">
                <section class="dashboard-panel spotlight-panel">
                  <div class="page-head">
                    <h1>开始</h1>
                    <p>打开任意网站，或者导入本地文档，把阅读入口都集中到一个桌面壳里。</p>
                  </div>

                  <div class="card-grid">
                    <button
                      v-for="site in quickLinks"
                      :key="site.name"
                      class="site-card"
                      @click="useQuickLink(site)">
                      <span
                        class="site-mark"
                        :class="site.tone">
                        {{ site.tag }}
                      </span>
                      <strong>{{ site.name }}</strong>
                      <small>{{ site.desc }}</small>
                      <em class="site-url">{{ site.url }}</em>
                    </button>
                  </div>
                </section>

                <section class="dashboard-panel history-panel">
                  <div class="block-head">
                    <strong>最近访问</strong>
                    <input
                      v-model="searchKeyword"
                      class="mini-input"
                      placeholder="搜标题或网址" />
                  </div>

                  <div class="history-list compact">
                    <button
                      v-for="item in filteredRecentVisits"
                      :key="`${item.type}-${item.url}`"
                      class="history-item"
                      @click="openRecentVisit(item)">
                      <strong>{{ item.title }}</strong>
                      <span>{{ item.url }}</span>
                    </button>
                  </div>
                </section>
              </div>
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
                <div class="webview-meta">
                  <strong>{{ activeTab.title }}</strong>
                  <span>{{ activeTab.url }}</span>
                </div>
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

        <!-- 右侧控制栏已移除：透明与滚动、最近访问等功能保留在设置中 -->
      </div>
    </section>
  </section>
</template>
