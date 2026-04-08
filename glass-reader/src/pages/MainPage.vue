<script setup>
import { nextTick, ref, watch } from 'vue';
import { useDesktopApp } from '../composables/useDesktopApp';

const {
  settings,
  quickLinks,
  favoriteSites,
  urlInput,
  searchKeyword,
  activeTab,
  activeTabId,
  tabs,
  filteredRecentVisits,
  handleOpenUrl,
  addNewTab,
  selectTab,
  closeTab,
  useQuickLink,
  useFavoriteSite,
  openRecentVisit
} = useDesktopApp();

const webviewRef = ref(null);

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

@media (prefers-color-scheme: dark) {
  html,
  body,
  #app,
  #root,
  #__next,
  #__nuxt {
    background: transparent !important;
    background-color: transparent !important;
  }
}
`;

function buildTransparencyScript(enabled) {
  return `(() => {
    const styleId = 'trans-glass-page-transparent-style';
    const cssText = ${JSON.stringify(transparentPageCss)};
    let styleNode = document.getElementById(styleId);

    if (${enabled ? 'true' : 'false'}) {
      if (!styleNode) {
        styleNode = document.createElement('style');
        styleNode.id = styleId;
        document.documentElement.appendChild(styleNode);
      }

      styleNode.textContent = cssText;
      document.documentElement.style.backgroundColor = 'transparent';

      if (document.body) {
        document.body.style.backgroundColor = 'transparent';
      }

      return true;
    }

    if (styleNode) {
      styleNode.remove();
    }

    document.documentElement.style.backgroundColor = '';

    if (document.body) {
      document.body.style.backgroundColor = '';
    }

    return true;
  })();`;
}

function syncWebviewTransparency() {
  const webview = webviewRef.value;
  if (!webview || activeTab.value.kind === 'dashboard') {
    return;
  }

  webview.executeJavaScript(buildTransparencyScript(settings.pageTransparentMode)).catch(() => {});
}

function handleWebviewDomReady() {
  syncWebviewTransparency();
}

watch(
  () => settings.pageTransparentMode,
  () => {
    syncWebviewTransparency();
  }
);

watch(
  () => activeTabId.value,
  async () => {
    await nextTick();
    syncWebviewTransparency();
  }
);
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
          placeholder="输入网址，回车打开..."
          @keydown.enter="handleOpenUrl" />
        <button
          class="open-btn"
          @click="handleOpenUrl">
          打开
        </button>
      </div>

      <div
        class="page-frame panel no-drag"
        :class="{ 'is-webview': activeTab.kind !== 'dashboard' }">
        <template v-if="activeTab.kind === 'dashboard'">
          <div class="page-head">
            <h1>新标签页</h1>
            <p>透明浏览与快速操作面板</p>
          </div>

          <section class="link-section">
            <div class="section-title">内置快捷</div>
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

          <section class="link-section">
            <div class="section-title">我的网站</div>
            <div class="card-grid favorites">
              <button
                v-for="site in favoriteSites"
                :key="site.name"
                class="site-card favorite"
                @click="useFavoriteSite(site)">
                <span class="site-mark neutral">{{ site.tag }}</span>
                <strong>{{ site.name }}</strong>
                <small>{{ site.url }}</small>
              </button>
            </div>
          </section>

          <section class="link-section recent-section">
            <div class="section-title">最近访问</div>
            <input
              v-model="searchKeyword"
              class="history-input"
              placeholder="搜索历史记录（标题或网址）..." />
            <div class="history-list">
              <button
                v-for="item in filteredRecentVisits"
                :key="item.url"
                class="history-item"
                @click="openRecentVisit(item)">
                <strong>{{ item.title }}</strong>
                <span>{{ item.url }}</span>
              </button>
            </div>
          </section>
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
  </section>
</template>
