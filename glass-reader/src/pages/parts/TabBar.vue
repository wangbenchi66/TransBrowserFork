<script setup>
import { Close } from '@element-plus/icons-vue';
import { defineProps, ref, computed } from 'vue';
import { ElMessage } from 'element-plus';
import BaseButton from '../../components/BaseButton.vue';
import ContextMenu from '../../components/ContextMenu.vue';

const props = defineProps({
  tabs: Array,
  activeTabId: [String, Number],
  formatTabTitle: Function,
  selectTab: Function,
  closeTab: Function,
  addNewTab: Function
  ,createPageTab: Function
  ,closeTabsToLeft: Function
  ,closeTabsToRight: Function
  ,closeAllTabs: Function
  ,updateTabMetadata: Function
});

const menuVisible = ref(false)
const menuX = ref(0)
const menuY = ref(0)
const menuTarget = ref(null)

const menuItems = computed(() => getMenuItems(menuTarget.value))

function getMenuItems(tab) {
  const pinned = !!(tab && tab.pinned)
  return [
    { key: 'copy', label: '复制链接', iconKey: 'view' },
    { key: 'copy_title', label: '复制标题', iconKey: 'view' },
    { key: 'close', label: '关闭标签', iconKey: 'close' },
    { key: 'close_left', label: '关闭左侧标签', iconKey: 'left' },
    { key: 'close_right', label: '关闭右侧标签', iconKey: 'right' },
    { key: 'close_all', label: '关闭全部标签', iconKey: 'close' }
  ]
}

function copyToClipboard(text) {
  if (!text) return Promise.reject(new Error('empty'))
  if (navigator && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
    return navigator.clipboard.writeText(text)
  }
  return new Promise((resolve, reject) => {
    try {
      const ta = document.createElement('textarea')
      ta.style.position = 'fixed'
      ta.style.left = '-9999px'
      ta.value = text
      document.body.appendChild(ta)
      ta.select()
      const ok = document.execCommand('copy')
      document.body.removeChild(ta)
      if (ok) resolve()
      else reject(new Error('execCopyFailed'))
    } catch (e) {
      reject(e)
    }
  })
}

function showMenu(e, tab) {
  try {
    e.preventDefault();
    e.stopPropagation();
  } catch (e) {}
  menuTarget.value = tab
  menuX.value = e.clientX || 0
  menuY.value = e.clientY || 0
  menuVisible.value = true
}

function onMenuSelect(item) {
  const tab = menuTarget.value
  menuVisible.value = false
  menuTarget.value = null
  if (!item || !tab) return
  if (item.key === 'copy') {
    const url = tab.url || ''
    if (!url) {
      ElMessage({ message: '此标签没有可复制的链接', type: 'warning' })
      return
    }
    copyToClipboard(url).then(() => ElMessage({ message: '已复制链接', type: 'success' })).catch(() => ElMessage({ message: '复制失败', type: 'error' }))
  } else if (item.key === 'copy_title') {
    const title = tab.title || ''
    if (!title) { ElMessage({ message: '无标题可复制', type: 'warning' }); return }
    copyToClipboard(title).then(() => ElMessage({ message: '已复制标题', type: 'success' })).catch(() => ElMessage({ message: '复制失败', type: 'error' }))
  } else if (item.key === 'close') {
    try { props.closeTab(tab.id) } catch (e) {}
  } else if (item.key === 'close_left') {
    try { if (typeof props.closeTabsToLeft === 'function') props.closeTabsToLeft(tab.id) } catch (e) {}
  } else if (item.key === 'close_right') {
    try { if (typeof props.closeTabsToRight === 'function') props.closeTabsToRight(tab.id) } catch (e) {}
  } else if (item.key === 'close_all') {
    try { if (typeof props.closeAllTabs === 'function') props.closeAllTabs() } catch (e) {}
  }
}
</script>

<template>
  <section class="tabbar panel">
    <div class="tabs">
      <BaseButton
        v-for="tab in props.tabs"
        :key="tab.id"
        class="tab-chip"
        :class="{ active: String(tab.id) === String(props.activeTabId) }"
        :title="tab.title || tab.url"
        @click="props.selectTab(tab.id)"
        @contextmenu.prevent.stop="showMenu($event, tab)">
        <span class="tab-title">{{ props.formatTabTitle(tab.title) }}</span>
        <span v-if="tab.pinned" class="pin-mark" title="已固定">📌</span>
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
    <ContextMenu
      :visible="menuVisible"
      :x="menuX"
      :y="menuY"
      :items="menuItems"
      @select="onMenuSelect"
      @close="menuVisible = false" />
  </section>
</template>

<style scoped>
/* 仅用于 tabbar 的局部样式（应用已有样式表为主） */
.pin-mark {
  margin-left: 6px;
  font-size: 12px;
  opacity: 0.9;
}
</style>
