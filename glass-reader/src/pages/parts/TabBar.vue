<script setup>
import { Close } from '@element-plus/icons-vue';
import { defineProps } from 'vue';
import BaseButton from '../../components/BaseButton.vue';

const props = defineProps({
  tabs: Array,
  activeTabId: [String, Number],
  formatTabTitle: Function,
  selectTab: Function,
  closeTab: Function,
  addNewTab: Function
});
</script>

<template>
  <section class="tabbar panel">
    <div class="tabs">
      <BaseButton
        v-for="tab in props.tabs"
        :key="tab.id"
        class="tab-chip"
        :class="{ active: tab.id === props.activeTabId }"
        @click="props.selectTab(tab.id)">
        <span class="tab-title">{{ props.formatTabTitle(tab.title) }}</span>
        <span
          class="close-mark"
          @click.stop="props.closeTab(tab.id)">
          <el-icon><Close /></el-icon>
        </span>
      </BaseButton>

      <BaseButton
        class="tab-add"
        @click="props.addNewTab">
        +
      </BaseButton>
    </div>
  </section>
</template>

<style scoped>
/* 仅用于 tabbar 的局部样式（应用已有样式表为主） */
</style>
