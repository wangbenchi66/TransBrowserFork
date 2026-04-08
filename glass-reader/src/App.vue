<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { useDesktopApp } from './composables/useDesktopApp';
import MainPage from './pages/MainPage.vue';
import SettingsPage from './pages/SettingsPage.vue';

const { settings, statusMessage, shellClasses, themeVars, handleMinimize, handleClose, patchSetting, initializeDesktopApp, disposeDesktopApp } = useDesktopApp();
const desktopApi = typeof window !== 'undefined' ? window.desktop : null;

const isSettingsOpen = ref(false);
let removeOpenSettingsListener = null;

function openSettingsModal() {
  isSettingsOpen.value = true;
}

function closeSettingsModal() {
  isSettingsOpen.value = false;
}

function handleKeydown(event) {
  if (event.key === 'Escape' && isSettingsOpen.value) {
    closeSettingsModal();
  }
}

function togglePin() {
  patchSetting('alwaysOnTop', !settings.alwaysOnTop);
}

onMounted(() => {
  initializeDesktopApp();
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

        <SettingsPage />
      </section>
    </div>
  </div>
</template>
