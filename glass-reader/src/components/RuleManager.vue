<script setup>
import { nextTick, onMounted, reactive, ref, watch } from 'vue';
import { useDesktopApp } from '../composables/useDesktopApp';

const { ruleProviders } = useDesktopApp();

const props = defineProps({
  autoOpen: { type: Boolean, default: false },
  initialType: { type: String, default: 'site' },
  initialPattern: { type: String, default: '' }
});

const providerKeys = ['site', 'toolbar', 'settings'];
const selectedType = ref('site');
const rules = ref([]);
const editing = ref(false);

const form = reactive({
  id: null,
  pattern: '',
  matchType: 'hostname',
  enabled: true,
  // site
  preventBlankTargets: true,
  removeImages: false,
  customCss: '',
  customJs: '',
  // toolbar
  toolbarDocked: null,
  toolbarPinned: null,
  toolbarVisible: null,
  toolbarDisabled: null,
  hideHandle: false,
  iconOnly: false,
  // settings
  customSettingsJson: '{}'
});

function loadRules() {
  try {
    const provider = ruleProviders[selectedType.value];
    if (!provider || typeof provider.getRules !== 'function') {
      rules.value = [];
      return;
    }
    rules.value = provider.getRules() || [];
  } catch (e) {
    rules.value = [];
  }
}

watch(
  selectedType,
  () => {
    loadRules();
  },
  { immediate: true }
);

// 如果从外部传入 autoOpen 与 initialPattern，则在打开时预填表单
watch(
  () => props.autoOpen,
  (v) => {
    if (!v) return;
    selectedType.value = props.initialType || 'site';
    nextTick(() => {
      resetForm();
      form.pattern = props.initialPattern || '';
      editing.value = true;
    });
  },
  { immediate: true }
);

onMounted(() => {
  if (props.autoOpen) {
    selectedType.value = props.initialType || 'site';
    nextTick(() => {
      resetForm();
      form.pattern = props.initialPattern || '';
      editing.value = true;
    });
  }
});

function toForm(item) {
  form.id = item?.id ?? null;
  form.pattern = item?.pattern ?? '';
  form.matchType = item?.matchType ?? 'hostname';
  form.enabled = item?.enabled !== undefined ? !!item.enabled : true;

  if (selectedType.value === 'site') {
    form.preventBlankTargets = item?.preventBlankTargets !== false;
    form.removeImages = !!item?.removeImages;
    form.customCss = item?.customCss ?? '';
    form.customJs = item?.customJs ?? '';
  } else if (selectedType.value === 'toolbar') {
    form.toolbarDocked = item?.toolbarDocked ?? null;
    form.toolbarPinned = item?.toolbarPinned ?? null;
    form.toolbarVisible = item?.toolbarVisible ?? null;
    form.toolbarDisabled = item?.toolbarDisabled ?? null;
    form.hideHandle = !!item?.hideHandle;
    form.iconOnly = !!item?.iconOnly;
  } else {
    try {
      form.customSettingsJson = JSON.stringify(item?.customSettings || {}, null, 2);
    } catch (e) {
      form.customSettingsJson = '{}';
    }
  }
}

function resetForm() {
  form.id = null;
  form.pattern = '';
  form.matchType = 'hostname';
  form.enabled = true;
  form.preventBlankTargets = true;
  form.removeImages = false;
  form.customCss = '';
  form.customJs = '';
  form.toolbarDocked = null;
  form.toolbarPinned = null;
  form.toolbarVisible = null;
  form.toolbarDisabled = null;
  form.hideHandle = false;
  form.iconOnly = false;
  form.customSettingsJson = '{}';
  editing.value = false;
}

function addNew() {
  resetForm();
  editing.value = true;
}

function editRule(r) {
  toForm(r);
  editing.value = true;
}

function saveRule() {
  const provider = ruleProviders[selectedType.value];
  if (!provider) return;

  const partial = { pattern: form.pattern, matchType: form.matchType, enabled: form.enabled };
  if (selectedType.value === 'site') {
    partial.preventBlankTargets = !!form.preventBlankTargets;
    partial.removeImages = !!form.removeImages;
    partial.customCss = form.customCss;
    partial.customJs = form.customJs;
  } else if (selectedType.value === 'toolbar') {
    partial.toolbarDocked = form.toolbarDocked;
    partial.toolbarPinned = form.toolbarPinned;
    partial.toolbarVisible = form.toolbarVisible;
    partial.toolbarDisabled = form.toolbarDisabled;
    partial.hideHandle = form.hideHandle;
    partial.iconOnly = form.iconOnly;
  } else {
    try {
      partial.customSettings = JSON.parse(form.customSettingsJson || '{}');
    } catch (e) {
      alert('自定义设置 JSON 解析失败');
      return;
    }
  }

  try {
    if (form.id) {
      provider.editRule(form.id, partial);
    } else {
      provider.addRule(partial);
    }
    if (typeof provider.persistRules === 'function') provider.persistRules();
  } catch (e) {
    console.warn('saveRule failed', e);
  }

  loadRules();
  resetForm();
}

function removeRule(r) {
  if (!confirm(`确定删除规则 ${r.pattern} 吗？`)) return;
  const provider = ruleProviders[selectedType.value];
  if (!provider) return;
  try {
    provider.removeRule(r.id);
    if (typeof provider.persistRules === 'function') provider.persistRules();
  } catch (e) {}
  loadRules();
}
</script>

<template>
  <div class="rule-manager">
    <div class="rule-header">
      <div class="type-switch">
        <button
          v-for="t in ['site', 'toolbar', 'settings']"
          :key="t"
          :class="{ active: selectedType === t }"
          @click="selectedType = t">
          {{ t }}
        </button>
      </div>
      <div class="actions">
        <button
          class="primary-btn"
          @click="addNew">
          新增规则
        </button>
      </div>
    </div>

    <div class="rule-list">
      <div
        v-if="!rules.length"
        class="empty">
        无规则
      </div>
      <ul v-else>
        <li
          v-for="r in rules"
          :key="r.id"
          class="rule-item">
          <div class="meta">
            <strong>{{ r.pattern }}</strong>
            <small>{{ r.matchType }}</small>
            <small v-if="!r.enabled">（已禁用）</small>
          </div>
          <div class="ops">
            <button
              class="secondary-btn"
              @click.prevent="editRule(r)">
              编辑
            </button>
            <button
              class="secondary-btn"
              @click.prevent="removeRule(r)">
              删除
            </button>
          </div>
        </li>
      </ul>
    </div>

    <div
      v-if="editing"
      class="rule-form">
      <label
        >匹配类型
        <select v-model="form.matchType">
          <option value="hostname">hostname</option>
          <option value="url">url</option>
          <option value="regex">regex</option>
        </select>
      </label>

      <label
        >模式
        <input
          v-model="form.pattern"
          placeholder="例如 example.com 或 *.example.com" />
      </label>

      <label
        ><input
          type="checkbox"
          v-model="form.enabled" />
        启用</label
      >

      <div
        v-if="selectedType === 'site'"
        class="site-settings">
        <label
          ><input
            type="checkbox"
            v-model="form.preventBlankTargets" />
          阻止新窗口（target=_blank）</label
        >
        <label
          ><input
            type="checkbox"
            v-model="form.removeImages" />
          隐藏图片</label
        >
        <label
          >自定义 CSS
          <textarea
            v-model="form.customCss"
            rows="4" />
        </label>
        <label
          >自定义 JS
          <textarea
            v-model="form.customJs"
            rows="4" />
        </label>
      </div>

      <div
        v-if="selectedType === 'toolbar'"
        class="toolbar-settings">
        <label
          >固定停靠
          <select v-model="form.toolbarDocked">
            <option :value="null">不覆盖</option>
            <option :value="true">停靠</option>
            <option :value="false">悬浮</option>
          </select>
        </label>
        <label
          >固定显示
          <select v-model="form.toolbarPinned">
            <option :value="null">不覆盖</option>
            <option :value="true">固定</option>
            <option :value="false">移入显示</option>
          </select>
        </label>
        <label
          >图标模式
          <input
            type="checkbox"
            v-model="form.iconOnly"
        /></label>
        <label
          >隐藏手柄
          <input
            type="checkbox"
            v-model="form.hideHandle"
        /></label>
        <label
          >禁用工具栏
          <input
            type="checkbox"
            v-model="form.toolbarDisabled"
        /></label>
      </div>

      <div
        v-if="selectedType === 'settings'"
        class="settings-settings">
        <label
          >自定义设置（JSON）
          <textarea
            v-model="form.customSettingsJson"
            rows="6" />
        </label>
      </div>

      <div class="form-actions">
        <button
          class="primary-btn"
          @click.prevent="saveRule">
          保存
        </button>
        <button
          class="secondary-btn"
          @click.prevent="resetForm">
          取消
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rule-manager {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.rule-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.type-switch button {
  margin-right: 6px;
  padding: 6px 8px;
  border-radius: 6px;
}
.type-switch button.active {
  background: #f0f6ff;
  border: 1px solid rgba(47, 120, 255, 0.12);
}
.rule-list ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.rule-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.96);
  border: 1px solid rgba(220, 224, 235, 0.85);
}
.rule-form label {
  display: block;
  margin-top: 8px;
}
.rule-form textarea,
.rule-form input,
.rule-form select {
  width: 100%;
  padding: 6px;
  border-radius: 6px;
  border: 1px solid #ddd;
}
.form-actions {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}
.primary-btn {
  background: #409eff;
  color: #fff;
  padding: 6px 10px;
  border-radius: 6px;
}
.secondary-btn {
  background: transparent;
  border: 1px solid #e6e6e6;
  padding: 6px 8px;
  border-radius: 6px;
}
</style>
