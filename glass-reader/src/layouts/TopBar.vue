<script setup>
import { defineProps } from 'vue';
import BaseButton from '../components/BaseButton.vue';

const props = defineProps({
  settings: Object,
  headerHoverActive: Boolean,
  openSettingsModal: Function,
  togglePin: Function,
  handleMinimize: Function,
  handleMaximize: Function,
  handleClose: Function
});
</script>

<template>
  <header class="topbar drag-region">
    <div class="brand-group no-drag">
      <div class="brand-block">
        <strong>Trans Glass</strong>
      </div>
    </div>

    <div
      class="title-drag"
      aria-hidden="true"></div>

    <div class="window-controls no-drag">
      <BaseButton
        class="control-btn settings-gear"
        aria-label="设置"
        @click="props.openSettingsModal">
        ⚙
      </BaseButton>
      <BaseButton
        class="control-btn pin-toggle"
        :class="{ active: props.settings.alwaysOnTop }"
        aria-label="窗口置顶"
        @click="props.togglePin">
        置
      </BaseButton>
      <BaseButton
        class="control-btn"
        aria-label="最小化"
        @click="props.handleMinimize">
        -
      </BaseButton>
      <BaseButton
        class="control-btn"
        aria-label="最大化"
        @click="props.handleMaximize">
        □
      </BaseButton>
      <BaseButton
        class="control-btn close"
        aria-label="关闭"
        @click="props.handleClose">
        x
      </BaseButton>
    </div>
  </header>
</template>

<style scoped>
/* TopBar 使用应用的全局样式 */
</style>
