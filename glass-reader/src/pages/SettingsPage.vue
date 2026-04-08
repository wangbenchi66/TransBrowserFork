<script setup>
import { useDesktopApp } from '../composables/useDesktopApp';

const { settings, leftToggleKeys, rightToggleKeys, patchSetting, toggleSetting, urlInput, handleOpenUrl } = useDesktopApp();

function openDefaultUrl() {
  urlInput.value = settings.defaultUrl;
  handleOpenUrl();
}
</script>

<template>
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
        @input="patchSetting('transparency', Number($event.target.value))" />
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
</template>
