<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { useDesktopApp } from './composables/useDesktopApp';
import TopBar from './layouts/TopBar.vue';
import MainPage from './pages/MainPage.vue';
import TabBar from './pages/parts/TabBar.vue';

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
  tabs,
  activeTabId,
  selectTab,
  closeTab,
  addNewTab,
  toggleSetting,
  urlInput,
  handleOpenUrl
} = useDesktopApp();
const desktopApi = typeof window !== 'undefined' ? window.desktop : null;

const isSettingsOpen = ref(false);
let removeOpenSettingsListener = null;
const editingShortcut = ref(null);
const capturedAccel = ref('');
const autoRangeRef = ref(null);
// 当开启 hoverHeaderMode 时，记录是否处于“悬停激活”状态（由全局鼠标位置控制）
const headerHoverActive = ref(false);

function _onMouseMoveForHeader(e) {
  try {
    if (!settings.hoverHeaderMode) {
      if (headerHoverActive.value) headerHoverActive.value = false;
      return;
    }
    const y = e && typeof e.clientY === 'number' ? e.clientY : 0;
    // 当光标靠近窗口顶部时激活；增加迟滞以避免闪烁
    const THRESHOLD = 80; // 触发高度
    const HYSTERESIS = 24; // 取消触发的附加距离，避免抖动
    if (y <= THRESHOLD) {
      if (!headerHoverActive.value) headerHoverActive.value = true;
    } else if (headerHoverActive.value && y > THRESHOLD + HYSTERESIS) {
      headerHoverActive.value = false;
    }
  } catch (e) {}
}

function formatAcceleratorFromEvent(e) {
  // 返回例如: Ctrl+Alt+T
  const isModifierOnly = (k) => ['Control', 'Shift', 'Alt', 'Meta'].includes(k);
  if (isModifierOnly(e.key)) {
    return null;
  }

  const parts = [];
  if (e.ctrlKey) parts.push('Ctrl');
  if (e.altKey) parts.push('Alt');
  if (e.shiftKey) parts.push('Shift');
  if (e.metaKey) parts.push('Meta');

  const map = {
    ' ': 'Space',
    Escape: 'Esc',
    ArrowUp: 'Up',
    ArrowDown: 'Down',
    ArrowLeft: 'Left',
    ArrowRight: 'Right'
  };

  let keyName = map[e.key] || e.key;
  if (keyName.length === 1) keyName = keyName.toUpperCase();
  // 对于 FunctionKeys F1..F12 保持原样
  parts.push(keyName);
  return parts.join('+');
}

function formatTabTitle(title) {
  const s = (title ?? '').toString().trim();
  const limit = 10; // 超过 10 个字符则截断
  if (!s) return '';
  return s.length <= limit ? s : s.slice(0, limit) + '…';
}

function _onCaptureKeydown(e) {
  e.preventDefault();
  const acc = formatAcceleratorFromEvent(e);
  if (!acc) return;
  capturedAccel.value = acc;
}

function startEditShortcut(key) {
  editingShortcut.value = key;
  capturedAccel.value = settings[key] || '';
  window.addEventListener('keydown', _onCaptureKeydown);
}

function confirmShortcut(key) {
  window.removeEventListener('keydown', _onCaptureKeydown);
  editingShortcut.value = null;
  if (capturedAccel.value) {
    patchSetting(key, capturedAccel.value);
    if (desktopApi?.log) {
      try {
        desktopApi.log(`[renderer] shortcut set ${key} -> ${capturedAccel.value}`);
      } catch (e) {}
    }
  }
  capturedAccel.value = '';
}

function cancelShortcut() {
  window.removeEventListener('keydown', _onCaptureKeydown);
  editingShortcut.value = null;
  capturedAccel.value = '';
}

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
    if (editingShortcut.value) {
      cancelShortcut();
      return;
    }
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
  urlInput.value = settings.defaultUrl === 'about:blank' ? '' : settings.defaultUrl;
  handleOpenUrl();
}

onMounted(() => {
  initializeDesktopApp();
  console.log('[renderer] desktopApi present', !!desktopApi, { setTransparency: !!desktopApi?.setTransparency, updateSettings: !!desktopApi?.updateSettings, log: !!desktopApi?.log });
  window.addEventListener('keydown', handleKeydown);
  // 全局鼠标移动监听：用于 hover 标题栏逻辑
  try {
    window.addEventListener('mousemove', _onMouseMoveForHeader);
  } catch (e) {}

  if (desktopApi?.onOpenSettingsRequest) {
    removeOpenSettingsListener = desktopApi.onOpenSettingsRequest(() => {
      openSettingsModal();
    });
  }
});

onBeforeUnmount(() => {
  disposeDesktopApp();
  window.removeEventListener('keydown', handleKeydown);
  window.removeEventListener('keydown', _onCaptureKeydown);
  try {
    window.removeEventListener('mousemove', _onMouseMoveForHeader);
  } catch (e) {}
  removeOpenSettingsListener?.();
  removeOpenSettingsListener = null;
});
</script>

<template>
  <div
    class="window-shell compact-header-tabs"
    :class="[shellClasses, { 'hide-scrollbars': !settings.showScrollbars, 'header-hover-active': headerHoverActive }]"
    :style="themeVars">
    <!-- top hotzone: when hoverHeaderMode enabled, this invisible strip triggers header/tab reveal -->
    <div
      v-if="settings.hoverHeaderMode"
      class="header-hotzone"
      aria-hidden="true"></div>
    <TopBar
      :settings="settings"
      :headerHoverActive="headerHoverActive"
      :openSettingsModal="openSettingsModal"
      :togglePin="togglePin"
      :handleMinimize="handleMinimize"
      :handleMaximize="handleMaximize"
      :handleClose="handleClose" />

    <TabBar
      v-if="settings.showTabBar"
      :tabs="tabs"
      :activeTabId="activeTabId"
      :formatTabTitle="formatTabTitle"
      :selectTab="selectTab"
      :closeTab="closeTab"
      :addNewTab="addNewTab" />

    <main class="page-host">
      <MainPage />
    </main>

    <div
      v-if="isSettingsOpen"
      class="settings-modal-layer settings-floating no-drag"
      @click.self="closeSettingsModal">
      <section class="settings-modal panel">
        <div class="settings-modal-header">
          <div>
            <strong>设置中心</strong>
            <span>当前修改会直接作用于主窗口</span>
          </div>
          <BaseButton
            class="control-btn close"
            aria-label="关闭设置"
            @click="closeSettingsModal">
            x
          </BaseButton>
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
          </section>

          <!-- 阅读控制已移除：相关控制已精简或合并到其它设置 -->

          <!-- 标题与标签默认合并，相关开关在常规列表中管理 -->

          <section class="toggle-grid section-panel">
            <div class="toggle-column">
              <h3 class="toggle-title">窗口行为</h3>
              <BaseButton
                v-for="item in leftToggleKeys"
                :key="item.key"
                class="toggle-row"
                @click="toggleSetting(item.key)">
                <span>{{ item.label }}</span>
                <span
                  class="switch"
                  :class="{ on: settings[item.key] }"></span>
              </BaseButton>
              <BaseButton
                class="toggle-row"
                @click="toggleSetting('toolbarDisabled')">
                <span>禁用工具栏（不再移入显示）</span>
                <span
                  class="switch"
                  :class="{ on: settings.toolbarDisabled }"></span>
              </BaseButton>
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

              <BaseButton
                v-for="item in rightToggleKeys"
                :key="item.key"
                class="toggle-row"
                @click="toggleSetting(item.key)">
                <span>{{ item.label }}</span>
                <span
                  class="switch"
                  :class="{ on: settings[item.key] }"></span>
              </BaseButton>
            </div>
          </section>

          <!-- 站点规则管理已移除（按需在页面内快速编辑弹窗保留） -->

          <section class="settings-block shortcut-block section-panel">
            <div class="shortcut-title">全局快捷键</div>
            <div class="shortcut-grid">
              <label>
                <span>老板键</span>
                <div style="display: flex; gap: 8px; align-items: center">
                  <input
                    class="text-field"
                    :value="editingShortcut === 'bossKey' ? capturedAccel || '按下组合键...' : settings.bossKey"
                    readonly />
                  <el-button
                    round
                    size="large"
                    @click="editingShortcut === 'bossKey' ? confirmShortcut('bossKey') : startEditShortcut('bossKey')">
                    {{ editingShortcut === 'bossKey' ? '保存' : '编辑' }}
                  </el-button>
                  <el-button
                    round
                    size="large"
                    v-if="editingShortcut === 'bossKey'"
                    @click="cancelShortcut">
                    取消
                  </el-button>
                </div>
              </label>

              <label>
                <span>降低透明</span>
                <div style="display: flex; gap: 8px; align-items: center">
                  <input
                    class="text-field"
                    :value="editingShortcut === 'decreaseTransparencyShortcut' ? capturedAccel || '按下组合键...' : settings.decreaseTransparencyShortcut"
                    readonly />
                  <el-button
                    round
                    size="large"
                    @click="editingShortcut === 'decreaseTransparencyShortcut' ? confirmShortcut('decreaseTransparencyShortcut') : startEditShortcut('decreaseTransparencyShortcut')">
                    {{ editingShortcut === 'decreaseTransparencyShortcut' ? '保存' : '编辑' }}
                  </el-button>
                  <el-button
                    round
                    size="large"
                    v-if="editingShortcut === 'decreaseTransparencyShortcut'"
                    @click="cancelShortcut">
                    取消
                  </el-button>
                </div>
              </label>

              <label>
                <span>提高透明</span>
                <div style="display: flex; gap: 8px; align-items: center">
                  <input
                    class="text-field"
                    :value="editingShortcut === 'increaseTransparencyShortcut' ? capturedAccel || '按下组合键...' : settings.increaseTransparencyShortcut"
                    readonly />
                  <el-button
                    round
                    size="large"
                    @click="editingShortcut === 'increaseTransparencyShortcut' ? confirmShortcut('increaseTransparencyShortcut') : startEditShortcut('increaseTransparencyShortcut')">
                    {{ editingShortcut === 'increaseTransparencyShortcut' ? '保存' : '编辑' }}
                  </el-button>
                  <el-button
                    round
                    size="large"
                    v-if="editingShortcut === 'increaseTransparencyShortcut'"
                    @click="cancelShortcut">
                    取消
                  </el-button>
                </div>
              </label>

              <label>
                <span>鼠标穿透</span>
                <div style="display: flex; gap: 8px; align-items: center">
                  <input
                    class="text-field"
                    :value="editingShortcut === 'clickThroughShortcut' ? capturedAccel || '按下组合键...' : settings.clickThroughShortcut"
                    readonly />
                  <el-button
                    round
                    size="large"
                    @click="editingShortcut === 'clickThroughShortcut' ? confirmShortcut('clickThroughShortcut') : startEditShortcut('clickThroughShortcut')">
                    {{ editingShortcut === 'clickThroughShortcut' ? '保存' : '编辑' }}
                  </el-button>
                  <el-button
                    round
                    size="large"
                    v-if="editingShortcut === 'clickThroughShortcut'"
                    @click="cancelShortcut">
                    取消
                  </el-button>
                </div>
              </label>
            </div>
          </section>
        </section>
      </section>
    </div>
  </div>
</template>
