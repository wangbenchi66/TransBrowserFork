<template>
  <el-button v-bind="$attrs" :type="type" :size="size" :plain="plain" :circle="circle">
    <slot />
  </el-button>
</template>

<script setup>
const props = defineProps({
  type: { type: String, default: undefined },
  size: { type: String, default: undefined },
  plain: { type: Boolean, default: false },
  circle: { type: Boolean, default: false }
})
</script>
