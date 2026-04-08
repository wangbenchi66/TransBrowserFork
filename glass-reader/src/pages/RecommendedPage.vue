<script setup>
import { computed, ref } from 'vue';
import { useDesktopApp } from '../composables/useDesktopApp';

const {
  quickLinks,
  filteredRecommendedSites,
  filteredRecentVisits,
  useQuickLink,
  useRecommendedSite,
  openRecentVisit,
  siteSearchKeyword,
  searchKeyword,
  dashboardMetrics,
  urlInput,
  handleOpenUrl,
  uploadLocalFiles
} = useDesktopApp();

const recommendedPreview = computed(() => filteredRecommendedSites.value.slice(0, 8));
const quickPreview = computed(() => quickLinks.value?.slice(0, 6) ?? []);

const fileInputRef = ref(null);

function triggerFileSelect() {
  fileInputRef.value?.click();
}

async function handleFileChange(event) {
  await uploadLocalFiles(event.target.files);
  event.target.value = '';
}
</script>

<template>
  <div class="recommended-page">
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
    <section class="metrics">
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

    <section class="quicklinks">
      <h2>快速入口</h2>
      <div class="grid">
        <button
          v-for="site in quickPreview"
          :key="site.name"
          class="card"
          @click="useQuickLink(site)">
          <span
            class="tag"
            :class="site.tone"
            >{{ site.tag }}</span
          >
          <div class="meta">
            <strong>{{ site.name }}</strong>
            <small>{{ site.desc }}</small>
          </div>
        </button>
      </div>
    </section>

    <section class="spotlight">
      <h2>推荐站点</h2>
      <div class="controls">
        <input
          v-model="siteSearchKeyword"
          placeholder="搜索站点"
          class="mini-input" />
      </div>

      <div class="grid">
        <button
          v-for="site in recommendedPreview"
          :key="site.name"
          class="card"
          @click="useRecommendedSite(site)">
          <span
            class="tag"
            :class="site.tone"
            >{{ site.tag }}</span
          >
          <div class="meta">
            <strong>{{ site.name }}</strong>
            <small>{{ site.category }} · {{ site.hint }}</small>
          </div>
        </button>
      </div>
    </section>

    <section class="history">
      <h2>最近访问</h2>
      <div class="controls">
        <input
          v-model="searchKeyword"
          placeholder="搜索最近访问"
          class="mini-input" />
      </div>

      <div class="list">
        <button
          v-for="item in filteredRecentVisits"
          :key="`${item.type}-${item.url}`"
          class="history-item"
          @click="openRecentVisit(item)">
          <strong>{{ item.title }}</strong>
          <span class="url">{{ item.url }}</span>
        </button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.recommended-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.metrics .metric-grid {
  display: flex;
  gap: 8px;
}
.metric-card {
  background: #fff;
  padding: 10px;
  border-radius: 8px;
  min-width: 80px;
  text-align: center;
}
.spotlight,
.history,
.quicklinks {
  background: #fff;
  padding: 12px;
  border-radius: 8px;
}
.grid {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.card {
  display: flex;
  gap: 8px;
  align-items: center;
  padding: 8px;
  border-radius: 6px;
  border: 1px solid #eee;
  background: #fafafa;
  cursor: pointer;
}
.tag {
  width: 36px;
  height: 36px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  font-weight: 700;
}
.list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.history-item {
  text-align: left;
  padding: 8px;
  border-radius: 6px;
  border: 1px solid #f0f0f0;
  background: #fff;
  cursor: pointer;
}
.url {
  display: block;
  color: #666;
  font-size: 12px;
}
.controls {
  margin-bottom: 8px;
}
.mini-input {
  padding: 6px 8px;
  border-radius: 6px;
  border: 1px solid #ddd;
}

.nav-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}
.url-input {
  flex: 1;
  padding: 6px 8px;
  border-radius: 6px;
  border: 1px solid #ddd;
}
.open-btn,
.secondary-btn {
  padding: 6px 10px;
  border-radius: 6px;
  border: 1px solid #ddd;
  background: #f5f5f5;
  cursor: pointer;
}
.hidden-input {
  display: none;
}
</style>
