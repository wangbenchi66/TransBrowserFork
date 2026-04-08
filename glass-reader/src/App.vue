<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { useDesktopApp } from './composables/useDesktopApp';
import MainPage from './pages/MainPage.vue';

const {
  settings,
  statusMessage,
  shellClasses,
  themeVars,
  handleMinimize,
  handleMaximize,
  handleClose,
  patchSetting,
  initializeDesktopApp,
  disposeDesktopApp,
  leftToggleKeys,
  rightToggleKeys,
  toggleSetting,
  urlInput,
  handleOpenUrl
} = useDesktopApp();
const desktopApi = typeof window !== 'undefined' ? window.desktop : null;

const isSettingsOpen = ref(false);
let removeOpenSettingsListener = null;

function openSettingsModal() {
  // 当打开设置时，临时确保窗口可接收鼠标事件，避免被鼠标穿透挡住设置交互
  isSettingsOpen.value = true;
  if (desktopApi?.setIgnoreMouseEvents) {
    try {
      desktopApi.setIgnoreMouseEvents(false);
    } catch (e) {}
  }
}

function closeSettingsModal() {
  isSettingsOpen.value = false;
  // 关闭设置后，恢复当前持久设置中的 clickThroughMode 状态
  if (desktopApi?.getSettings && desktopApi?.setIgnoreMouseEvents) {
    try {
      desktopApi
        .getSettings()
        .then((s) => {
          try {
            desktopApi.setIgnoreMouseEvents(!!s.clickThroughMode);
          } catch (e) {}
        })
        .catch(() => {});
    } catch (e) {}
  }
}

function handleKeydown(event) {
  if (event.key === 'Escape' && isSettingsOpen.value) {
    closeSettingsModal();
  }
}

function togglePin() {
  patchSetting('alwaysOnTop', !settings.alwaysOnTop);
}

function handleTransparencyInput(e) {
  const v = Number(e.target.value || 0);
  console.log('[renderer] handleTransparencyInput', v, { hasSetTransparency: !!desktopApi?.setTransparency, hasUpdateSettings: !!desktopApi?.updateSettings });
  if (desktopApi?.log) {
    try {
      desktopApi.log(`[renderer] handleTransparencyInput ${v} hasSetTransparency=${!!desktopApi?.setTransparency}`);
    } catch (err) {}
  }
  patchSetting('transparency', v);

  if (desktopApi?.setTransparency) {
    desktopApi.setTransparency(v).catch(() => {});
  }

  if (desktopApi?.updateSettings) {
    desktopApi
      .updateSettings({ ...settings })
      .then((ns) => {
        console.log('[renderer] updateSettings result', ns);
      })
      .catch((err) => {
        console.warn('[renderer] updateSettings failed', err);
      });
  }
}

async function testSetTransparency() {
  if (!desktopApi?.setTransparency) {
    console.warn('[renderer] desktopApi.setTransparency not available');
    return;
  }

  try {
    const res = await desktopApi.setTransparency(50);
    console.log('[renderer] testSetTransparency result', res);
    if (desktopApi?.log) {
      try {
        desktopApi.log(`[renderer] testSetTransparency result ${JSON.stringify(res)}`);
      } catch (e) {}
    }
  } catch (err) {
    console.warn('[renderer] testSetTransparency failed', err);
  }
}

function openDefaultUrl() {
  urlInput.value = settings.defaultUrl;
  handleOpenUrl();
}

onMounted(() => {
  initializeDesktopApp();
  console.log('[renderer] desktopApi present', !!desktopApi, { setTransparency: !!desktopApi?.setTransparency, updateSettings: !!desktopApi?.updateSettings, log: !!desktopApi?.log });
  window.addEventListener('keydown', handleKeydown);

  if (desktopApi?.onOpenSettingsRequest) {
    removeOpenSettingsListener = desktopApi.onOpenSettingsRequest(() => {
      openSettingsModal();
    });
  }
});

onBeforeUnmount(() => {
  disposeDesktopApp();
  window.removeEventListener('keydown', handleKeydown);
  removeOpenSettingsListener?.();
  removeOpenSettingsListener = null;
});
</script>

<template>
  <div
    class="window-shell"
    :class="shellClasses"
    :style="themeVars">
    <header class="topbar drag-region">
      <div class="brand-group no-drag">
        <button
          class="pin-btn"
          :class="{ active: settings.alwaysOnTop }"
          @click="togglePin">
          P
        </button>
        <div class="brand-block">
          <strong>Trans Glass</strong>
          <span>{{ statusMessage }}</span>
        </div>
      </div>

      <div class="nav-switch no-drag">
        <button
          class="nav-btn"
          :class="{ active: settings.fullWindowTransparent }"
          @click="patchSetting('fullWindowTransparent', !settings.fullWindowTransparent)">
          透明
        </button>
        <button
          class="nav-btn active"
          @click="openSettingsModal">
          设置
        </button>
      </div>

      <div class="window-controls no-drag">
        <button
          class="control-btn"
          aria-label="最小化"
          @click="handleMinimize">
          -
        </button>
        <button
          class="control-btn"
          aria-label="最大化"
          @click="handleMaximize">
          □
        </button>
        <button
          class="control-btn close"
          aria-label="关闭"
          @click="handleClose">
          x
        </button>
      </div>
    </header>

    <main class="page-host">
      <MainPage />
    </main>

    <div
      v-if="isSettingsOpen"
      class="settings-modal-layer no-drag"
      @click.self="closeSettingsModal">
      <section class="settings-modal panel">
        <div class="settings-modal-header">
          <div>
            <strong>设置中心</strong>
            <span>当前修改会直接作用于主窗口</span>
          </div>
          <button
            class="control-btn close"
            aria-label="关闭设置"
            @click="closeSettingsModal">
            x
          </button>
        </div>

        <section class="settings-page panel no-drag">
          <div class="settings-header">
            <div>
              <h2>设置中心</h2>
              <p>所有开关都会实时同步到主窗口</p>
            </div>
            <span>{{ settings.transparency }}% 透明度</span>
          </div>

          <section class="settings-block slider-block section-panel">
            <label class="field-label">透明度</label>
            <input
              :value="settings.transparency"
              class="slider"
              type="range"
              min="0"
              max="85"
              @input="handleTransparencyInput($event)" />
            <div style="margin-top: 8px">
              <button
                class="primary-btn"
                @click="testSetTransparency">
                测试透明50%
              </button>
            </div>
          </section>

          <section class="settings-block section-panel">
            <div class="field-label">阅读控制</div>
            <div class="shortcut-grid settings-grid-compact">
              <label>
                <span>自动滚动速度</span>
                <input
                  :value="settings.autoScrollSpeed"
                  class="slider"
                  type="range"
                  min="5"
                  max="80"
                  @input="patchSetting('autoScrollSpeed', Number($event.target.value))" />
              </label>
              <label>
                <span>本地文字字号</span>
                <input
                  :value="settings.readerFontScale"
                  class="slider"
                  type="range"
                  min="80"
                  max="160"
                  @input="patchSetting('readerFontScale', Number($event.target.value))" />
              </label>
            </div>

            <div class="color-row settings-row-gap">
              <span>本地文字颜色</span>
              <input
                :value="settings.readerTextColor"
                class="color-input"
                type="color"
                @input="patchSetting('readerTextColor', $event.target.value)" />
            </div>

            <button
              class="toggle-row settings-row-gap"
              @click="toggleSetting('autoScrollEnabled')">
              <span>自动滚动</span>
              <span
                class="switch"
                :class="{ on: settings.autoScrollEnabled }"></span>
            </button>
          </section>

          <section class="toggle-grid section-panel">
            <div class="toggle-column">
              <h3 class="toggle-title">窗口行为</h3>
              <button
                v-for="item in leftToggleKeys"
                :key="item.key"
                class="toggle-row"
                @click="toggleSetting(item.key)">
                <span>{{ item.label }}</span>
                <span
                  class="switch"
                  :class="{ on: settings[item.key] }"></span>
              </button>
            </div>

            <div class="toggle-column">
              <h3 class="toggle-title">显示与渲染</h3>
              <div class="color-row">
                <span>状态栏背景色</span>
                <input
                  :value="settings.statusBarColor"
                  class="color-input"
                  type="color"
                  @input="patchSetting('statusBarColor', $event.target.value)" />
              </div>

              <button
                v-for="item in rightToggleKeys"
                :key="item.key"
                class="toggle-row"
                @click="toggleSetting(item.key)">
                <span>{{ item.label }}</span>
                <span
                  class="switch"
                  :class="{ on: settings[item.key] }"></span>
              </button>
            </div>
          </section>

          <section class="settings-block section-panel">
            <label class="field-label">默认网址</label>
            <div class="inline-field">
              <input
                :value="settings.defaultUrl"
                class="text-field"
                @input="patchSetting('defaultUrl', $event.target.value)" />
              <button
                class="primary-btn"
                @click="openDefaultUrl">
                打开
              </button>
            </div>
          </section>

          <section class="settings-block shortcut-block section-panel">
            <div class="shortcut-title">全局快捷键</div>
            <div class="shortcut-grid">
              <label>
                <span>老板键</span>
                <input
                  class="text-field"
                  :value="settings.bossKey"
                  readonly />
              </label>
              <label>
                <span>降低透明</span>
                <input
                  class="text-field"
                  :value="settings.decreaseTransparencyShortcut"
                  readonly />
              </label>
              <label>
                <span>提高透明</span>
                <input
                  class="text-field"
                  :value="settings.increaseTransparencyShortcut"
                  readonly />
              </label>
              <label>
                <span>鼠标穿透</span>
                <input
                  class="text-field"
                  :value="settings.clickThroughShortcut"
                  readonly />
              </label>
            </div>
          </section>
        </section>
      </section>
    </div>
  </div>
</template>
