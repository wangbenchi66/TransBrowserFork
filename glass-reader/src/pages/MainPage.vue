<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useDesktopApp } from '../composables/useDesktopApp';
import recommendedPage from './RecommendedPage.vue';
import BottomToolbar from './parts/BottomToolbar.vue';
import TabBar from './parts/TabBar.vue';

const { settings, activeTab, activeTabId, tabs, addNewTab, selectTab, closeTab, patchSetting, updateTabMetadata, siteRules, ruleProviders } = useDesktopApp();

const webviewRef = ref(null);
const webviewReady = ref(false);
// 覆盖 webview 的 User-Agent，部分站点会屏蔽 Electron UA 导致连接被关闭
const userAgent = ref('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36');
const localReaderRef = ref(null);
let localScrollTimer = null;
const siteZoom = ref(1);
// bottom toolbar pop state is managed inside BottomToolbar component

const currentHost = computed(() => {
  try {
    const u = new URL(activeTab?.url || '');
    return (u.hostname || '').replace(/^www\./i, '');
  } catch (e) {
    return '';
  }
});

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
  min-width: 180px;
  z-index: 1300;
}
`;

// 更强力的透明样式（保留图片/视频/canvas/svg）
const transparentPageCssAggressive = `
      }
.hover-pop .mini-range {
  width: 140px;
}
#root,
#__next,
  top: -34px;
  background: transparent !important;
  background-color: transparent !important;
}

html::before,
html::after,
body::before,
body::after {
  z-index: 1400;
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

// computeLeft helpers moved into BottomToolbar

// 格式化标签标题：保证单行显示，超过指定字符数时截断并添加省略号
function formatTabTitle(title) {
  const s = (title ?? '').toString().trim();
  const limit = 10; // 超过 10 个字符则截断
  if (!s) return '';
  return s.length <= limit ? s : s.slice(0, limit) + '…';
}

// pop handlers moved into BottomToolbar

// default helpers moved into BottomToolbar

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

function disableToolbar() {
  // 永久关闭工具栏的显示（直到用户在设置中重新启用）
  try {
    patchSetting('toolbarDisabled', true);
    patchSetting('toolbarVisible', false);
    patchSetting('toolbarPinned', false);
  } catch (e) {}
}

// 原始的 JS 注入方法（用于自动滚动等需要执行脚本的场景）
function runWebviewJS(script) {
  const webview = webviewRef.value;
  if (!webview || activeTab.value.kind === 'dashboard' || activeTab.value.kind === 'local-text') {
    return Promise.resolve();
  }

  if (!webviewReady.value) return;

  try {
    // 返回 executeJavaScript 的 Promise 并记录错误，便于调试注入失败的原因
    try {
      const p = webview.executeJavaScript(script);
      if (p && typeof p.then === 'function') {
        return p.catch((err) => {
          try {
            console.error('[runWebviewJS] executeJavaScript error', err);
          } catch (e) {}
        });
      }
      return Promise.resolve();
    } catch (err) {
      try {
        console.error('[runWebviewJS] executeJavaScript sync error', err);
      } catch (e) {}
      return Promise.resolve();
    }
  } catch (e) {
    try {
      console.error('[runWebviewJS] unexpected error', e);
    } catch (err) {}
    return Promise.resolve();
  }
}

// 拦截 webview 尝试打开新窗口的事件，强制在当前 webview 中打开链接
function onWebviewNewWindow(e) {
  try {
    if (e && typeof e.preventDefault === 'function') e.preventDefault();
  } catch (err) {}

  const url = (e && (e.url || (e.detail && e.detail.url))) || '';
  if (!url) return;

  const w = webviewRef.value;
  try {
    // 优先使用 loadURL（若可用），否则回退到设置 src
    if (w && typeof w.loadURL === 'function') {
      w.loadURL(url);
    } else if (w) {
      w.src = url;
    }
  } catch (err) {
    // ignore
  }
}

// 有时页面会触发 will-navigate，确保在当前 webview 内部导航
function onWebviewWillNavigate(e) {
  // 这里不阻止导航，但可用于插入日志或同步 tab url
}

// 注入到 webview 内部，强制移除或拦截所有 target="_blank" 行为，确保在当前页面打开链接
function forceDisableBlankTargets() {
  const script = `(() => {
    try {
      // 覆盖 window.open，避免通过脚本打开新窗口
      window.open = function(url) {
        if (!url) return null;
        try { location.assign(url); } catch(e) {}
        return null;
      };

      // 捕获点击，优先处理 target="_blank" 的锚点
      document.addEventListener('click', function(evt) {
        try {
          const el = evt.target && evt.target.closest && evt.target.closest('a');
          if (!el) return;
          const href = el.getAttribute && (el.getAttribute('href') || el.href);
          if (!href) return;
          const targ = (el.getAttribute && el.getAttribute('target') || el.target || '').toLowerCase();
          if (targ === '_blank') {
            evt.preventDefault();
            try { el.removeAttribute('target'); } catch(e) {}
            try { location.assign(href); } catch(e) {}
          }
        } catch(e) {}
      }, true);

      // 初始移除已有的 target="_blank"
      try {
        document.querySelectorAll && document.querySelectorAll('a[target="_blank"]').forEach(a => { try { a.removeAttribute('target'); } catch(e) {} });
      } catch(e) {}

      // 监听动态插入或修改的元素，移除 target 属性
      try {
        const mo = new MutationObserver(function(muts) {
          try {
            muts.forEach(m => {
              if (m.addedNodes && m.addedNodes.length) {
                m.addedNodes.forEach(n => {
                  if (n && n.nodeType === 1) {
                    try {
                      if (n.tagName === 'A' && (n.target || '').toLowerCase() === '_blank') n.removeAttribute('target');
                      const inner = n.querySelectorAll && n.querySelectorAll('a[target="_blank"]');
                      inner && inner.forEach(a => { try { a.removeAttribute('target'); } catch(e) {} });
                    } catch(e) {}
                  }
                });
              }
              if (m.type === 'attributes' && m.target && m.target.tagName === 'A' && m.attributeName === 'target') {
                try { if ((m.target.getAttribute('target') || '').toLowerCase() === '_blank') m.target.removeAttribute('target'); } catch(e) {}
              }
            });
          } catch(e) {}
        });
        mo.observe(document.documentElement || document, { childList: true, subtree: true, attributes: true, attributeFilter: ['target'] });
      } catch(e) {}
    } catch(e) {}
    return true;
  })();`;

  runWebviewJS(script);
}

// 从当前 webview 读取页面元数据并更新对应 tab（标题 / 副标题）
async function updateTabFromWebview() {
  const w = webviewRef.value;
  const tabId = activeTabId.value;
  if (!w || !webviewReady.value) return;
  try {
    if (typeof w.executeJavaScript !== 'function') return;
    const script = `(function(){try{return {title:document.title||'',desc:(document.querySelector('meta[name="description"]')&&document.querySelector('meta[name="description"]').getAttribute('content'))||''}}catch(e){return {title:'',desc:''}}})();`;
    const res = await w.executeJavaScript(script);
    if (!res) return;
    try {
      updateTabMetadata(tabId, { title: res.title || undefined, subtitle: res.desc || undefined });
    } catch (e) {}
  } catch (e) {}
}

function onPageTitleUpdated(e) {
  try {
    updateTabFromWebview();
  } catch (e) {}
}

function onDidNavigate(e) {
  try {
    updateTabFromWebview();
  } catch (e) {}
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
  const w = webviewRef.value;
  // 优先使用 activeTab.url；若不存在则回退到 webview 的 URL（getURL 或 src）
  let pageUrl = '';
  try {
    if (activeTab && activeTab.url) {
      pageUrl = activeTab.url;
    } else if (w) {
      if (typeof w.getURL === 'function') {
        try {
          pageUrl = w.getURL() || '';
        } catch (e) {
          pageUrl = w.src || '';
        }
      } else {
        pageUrl = w.src || '';
      }
    }
  } catch (e) {
    pageUrl = activeTab?.url || '';
  }

  try {
    console.log('[webview] dom-ready', { url: pageUrl, kind: activeTab?.kind, webviewRefPresent: !!webviewRef.value, webviewReady: webviewReady.value });
  } catch (e) {}
  syncReaderEffects();
  // 尝试读取并同步当前页面缩放比例
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
  // 注册拦截新窗口事件，防止在外部打开新窗口
  try {
    if (w && typeof w.addEventListener === 'function') {
      try {
        w.removeEventListener('new-window', onWebviewNewWindow);
      } catch (e) {}
      try {
        w.removeEventListener('will-navigate', onWebviewWillNavigate);
      } catch (e) {}
      try {
        w.removeEventListener('page-title-updated', onPageTitleUpdated);
      } catch (e) {}
      try {
        w.removeEventListener('did-navigate', onDidNavigate);
      } catch (e) {}
      try {
        w.removeEventListener('did-navigate-in-page', onDidNavigate);
      } catch (e) {}
      w.addEventListener('new-window', onWebviewNewWindow);
      w.addEventListener('will-navigate', onWebviewWillNavigate);
      w.addEventListener('page-title-updated', onPageTitleUpdated);
      w.addEventListener('did-navigate', onDidNavigate);
      w.addEventListener('did-navigate-in-page', onDidNavigate);
      try {
        w.addEventListener('console-message', (evt) => {
          try {
            console.log('[webview.console]', evt && evt.message ? evt.message : evt);
          } catch (e) {}
        });
      } catch (e) {}
      try {
        w.addEventListener('ipc-message', (evt) => {
          try {
            console.log('[webview.ipc]', evt && evt.channel ? evt.channel : evt, evt && evt.args ? evt.args : undefined);
          } catch (e) {}
        });
      } catch (e) {}
    }
  } catch (e) {}

  // 根据各类规则决定是否注入拦截 _blank 的脚本，以及应用站点自定义 CSS/JS
  try {
    let combined = null;
    try {
      combined = ruleProviders && typeof ruleProviders.getCombinedRulesForUrl === 'function' ? ruleProviders.getCombinedRulesForUrl(pageUrl || '') : null;
    } catch (e) {
      combined = null;
    }

    const siteRulesList = combined?.site || [];
    const toolbarRulesList = combined?.toolbar || [];

    const allowNewWindow = toolbarRulesList.some((r) => r.toolbarDisabled === false) || siteRulesList.some((r) => r.preventBlankTargets === false);

    if (!allowNewWindow) {
      try {
        forceDisableBlankTargets();
      } catch (e) {}
    }

    // 应用站点自定义样式/脚本：仅支持规则的 apply(helper) 单一方法
    try {
      for (const r of siteRulesList) {
        try {
          if (typeof r.apply === 'function') {
            try {
              try {
                console.log('[site-rules] applying rule', r.id, r.pattern);
              } catch (e) {}
              const maybe = r.apply({
                runWebviewCss: (idSuffix, css) => runWebviewCss(`site-custom-css-${r.id}${idSuffix ? `-${idSuffix}` : ''}`, css),
                runWebviewJS: (js) => runWebviewJS(js),
                webview: w,
                settings,
                activeTab: activeTab,
                rule: r
              });
              if (maybe && typeof maybe.then === 'function') maybe.catch(() => {});
            } catch (e) {}
          }
        } catch (e) {}
      }
    } catch (e) {}

    // 记录加载失败以便排查网络错误（例如 ERR_CONNECTION_CLOSED）
    try {
      if (w && typeof w.addEventListener === 'function') {
        try {
          w.removeEventListener('did-fail-load', onWebviewFailLoad);
        } catch (e) {}

        w.addEventListener('did-fail-load', onWebviewFailLoad);
      }
    } catch (e) {}
  } catch (e) {}

  // 工具栏规则逻辑已移动至顶层，避免模板访问到未定义的变量
}

// 计算并维护对当前页面的工具栏覆盖规则（优先使用 toolbarRules）
const effectiveToolbar = ref({
  docked: settings.toolbarDocked,
  pinned: settings.toolbarPinned,
  visible: settings.toolbarVisible,
  disabled: settings.toolbarDisabled,
  hideHandle: false,
  iconOnly: false
});

function updateEffectiveToolbar() {
  try {
    const w = webviewRef.value;
    let pageUrl = '';
    try {
      if (activeTab && activeTab.url) pageUrl = activeTab.url;
      else if (w) pageUrl = typeof w.getURL === 'function' ? w.getURL() || '' : w.src || '';
    } catch (e) {
      pageUrl = activeTab?.url || '';
    }

    const combined = ruleProviders && typeof ruleProviders.getCombinedRulesForUrl === 'function' ? ruleProviders.getCombinedRulesForUrl(pageUrl || '') : null;
    const toolbarList = combined?.toolbar || [];
    const override = toolbarList.length ? toolbarList[0] : null;
    effectiveToolbar.value.docked = override?.toolbarDocked ?? settings.toolbarDocked;
    effectiveToolbar.value.pinned = override?.toolbarPinned ?? settings.toolbarPinned;
    effectiveToolbar.value.visible = override?.toolbarVisible ?? settings.toolbarVisible;
    effectiveToolbar.value.disabled = override?.toolbarDisabled ?? settings.toolbarDisabled;
    effectiveToolbar.value.hideHandle = override?.hideHandle ?? false;
    effectiveToolbar.value.iconOnly = override?.iconOnly ?? false;
  } catch (e) {
    // ignore
  }
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

// 当工具栏相关设置变更时，重新计算 effectiveToolbar
watch(
  () => [settings.toolbarVisible, settings.toolbarPinned, settings.toolbarDisabled, settings.toolbarDocked],
  () => {
    try {
      updateEffectiveToolbar();
    } catch (e) {}
  }
);

watch(
  () => activeTabId.value,
  async () => {
    // 切换 tab 时重置 ready 状态，等待新的 webview dom-ready
    webviewReady.value = false;
    await nextTick();
    syncReaderEffects();
    try {
      updateEffectiveToolbar();
    } catch (e) {}
  }
);

onBeforeUnmount(() => {
  stopLocalReaderAutoScroll();
});

onMounted(() => {
  try {
    updateEffectiveToolbar();
  } catch (e) {}
});
</script>

<template>
  <section class="main-page">
    <TabBar
      v-if="settings.showTabBar"
      :tabs="tabs"
      :activeTabId="activeTabId"
      :formatTabTitle="formatTabTitle"
      :selectTab="selectTab"
      :closeTab="closeTab"
      :addNewTab="addNewTab" />

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
                  :useragent="userAgent"
                  nodeintegration="false"
                  enableblinkfeatures="ResizeObserver"
                  @dom-ready="handleWebviewDomReady"
                  allowpopups></webview>
              </div>
            </template>
            <!-- 底部工具栏（网页模式下显示） -->
            <BottomToolbar
              v-if="activeTab.kind !== 'dashboard' && activeTab.kind !== 'local-text'"
              :settings="settings"
              :siteZoom="siteZoom"
              :patchSetting="patchSetting"
              :webviewBack="webviewBack"
              :webviewForward="webviewForward"
              :webviewReload="webviewReload"
              :zoomIn="zoomIn"
              :zoomOut="zoomOut"
              :resetZoom="resetZoom"
              :toggleToolbarPinned="toggleToolbarPinned"
              :disableToolbar="disableToolbar"
              :toolbar-visible="effectiveToolbar.visible"
              :toolbar-pinned="effectiveToolbar.pinned"
              :toolbar-disabled="effectiveToolbar.disabled"
              :toolbar-icon-only="effectiveToolbar.iconOnly"
              :toolbar-docked="effectiveToolbar.docked"
              :hide-handle="effectiveToolbar.hideHandle" />
          </div>
        </section>
      </div>
    </section>

    <!-- 站点规则快速编辑弹窗已移除（由自定义规则文件管理） -->
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
  background: rgba(255, 255, 255, 0.92);
  backdrop-filter: blur(8px);
  box-shadow: 0 6px 18px rgba(16, 23, 32, 0.08);
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
  width: 56px;
  height: 8px;
  border-radius: 999px;
  background: linear-gradient(90deg, rgba(0, 0, 0, 0.06), rgba(0, 0, 0, 0.03));
  border: 1px solid rgba(0, 0, 0, 0.06);
  cursor: pointer;
  z-index: 1300;
  box-shadow: 0 6px 18px rgba(16, 23, 32, 0.06);
  transition:
    transform 150ms ease,
    opacity 150ms;
}
.toolbar-handle:hover {
  transform: translateX(-50%) translateY(-3px);
  opacity: 0.98;
}
.bottom-toolbar-container:hover .bottom-toolbar.hidden {
  transform: translateY(0);
  opacity: 1;
  pointer-events: auto;
}

/* 当工具栏被用户关闭时，禁止通过移入显示 */
.bottom-toolbar-container.no-hover:hover .bottom-toolbar.hidden {
  transform: translateY(calc(100% + 8px));
  opacity: 0;
  pointer-events: none;
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

/* 站点规则编辑弹窗样式 */
.rule-modal-layer {
  position: fixed;
  left: 0;
  top: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.32);
  z-index: 2400;
}
.rule-modal {
  width: 760px;
  max-width: calc(100% - 48px);
  max-height: calc(100% - 48px);
  overflow: auto;
  padding: 12px;
}
.rule-modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}
.rule-modal-body {
  background: #fff;
  padding: 12px;
  border-radius: 8px;
}
.icon-btn:hover {
  background: rgba(16, 23, 32, 0.04);
  transform: translateY(-1px);
}
.hide-toolbar-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
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

/* Tabs: 保证标签只显示一行，超过 10 个字符截断显示省略号 */
.tabs {
  display: flex;
  gap: 8px;
  align-items: center;
}
.tab-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  border-radius: 8px;
  white-space: nowrap;
  cursor: pointer;
}
.tab-chip .tab-title {
  display: inline-block;
  max-width: 10ch; /* 近似 10 个字符宽度 */
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.range-controls {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 6px;
}
.range-row {
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
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
  background: rgba(0, 0, 0, 0.04);
  color: rgba(0, 0, 0, 0.75);
  font-weight: 600;
}
.hide-toolbar-btn:hover {
  background: rgba(0, 0, 0, 0.08);
}

/* 纯图标样式（工具栏内使用） */
.bottom-toolbar .icon-only {
  background: transparent;
  border: none;
  padding: 4px;
  width: 32px;
  height: 32px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  box-shadow: none;
  transition:
    background 120ms ease,
    transform 120ms ease,
    color 120ms ease;
}
.bottom-toolbar .icon-only:hover {
  background: rgba(16, 23, 32, 0.04);
  transform: translateY(-1px);
}
.bottom-toolbar .icon-only.on {
  color: #409eff;
  background: rgba(64, 158, 255, 0.06);
}
.bottom-toolbar .close-toolbar-btn.icon-only {
  color: #e04842;
}
.bottom-toolbar .close-toolbar-btn.icon-only:hover {
  background: rgba(224, 72, 66, 0.08);
  color: #c0392b;
}
</style>
