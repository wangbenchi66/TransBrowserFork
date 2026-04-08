import { computed, reactive, ref } from 'vue'

const desktopApi = typeof window !== 'undefined' ? window.desktop : null

const quickLinks = [
    { name: '微信读书', tag: 'WR', tone: 'green', desc: '沉浸阅读', url: 'https://weread.qq.com' },
    { name: '知乎', tag: 'ZH', tone: 'blue', desc: '高质量问答', url: 'https://www.zhihu.com' },
    { name: '哔哩哔哩', tag: 'B', tone: 'pink', desc: '视频社区', url: 'https://www.bilibili.com' },
    { name: 'GitHub', tag: 'GH', tone: 'neutral', desc: '开发者社区', url: 'https://github.com' },
]

const favoriteSites = ref([
    { name: '百度', tag: 'BD', url: 'https://www.baidu.com' },
    { name: '豆瓣读书', tag: 'DB', url: 'https://book.douban.com' },
    { name: '少数派', tag: 'SS', url: 'https://sspai.com' },
    { name: '掘金', tag: 'JG', url: 'https://juejin.cn' },
])

const recentVisits = ref([
    { title: '微信读书', url: 'https://weread.qq.com' },
    { title: '知乎热榜', url: 'https://www.zhihu.com/hot' },
    { title: '哔哩哔哩首页', url: 'https://www.bilibili.com' },
])

const defaultSettings = {
    transparency: 0,
    showInTaskbar: true,
    autoHide: false,
    showTabBar: true,
    noImageMode: false,
    transparentBackground: false,
    antiScreenshotMode: false,
    mobileMode: false,
    hoverHeaderMode: false,
    pageTransparentMode: false,
    grayscaleMode: false,
    clickThroughMode: false,
    closeToTray: false,
    disableWindowShadow: false,
    alwaysOnTop: false,
    statusBarColor: '#f5f5f7',
    defaultUrl: 'about:blank',
    bossKey: 'Alt+Q',
    decreaseTransparencyShortcut: 'Alt+Up',
    increaseTransparencyShortcut: 'Alt+Down',
    clickThroughShortcut: 'Ctrl+Alt+T',
}

const settings = reactive({ ...defaultSettings })
const urlInput = ref(defaultSettings.defaultUrl)
const searchKeyword = ref('')
const statusMessage = ref('Alt+Q 可快速隐藏或恢复窗口')
const activeTabId = ref(1)
const tabs = ref([
    {
        id: 1,
        title: '新',
        url: 'about:blank',
        subtitle: '新标签页',
        kind: 'dashboard',
    },
])

const leftToggleKeys = [
    { key: 'showInTaskbar', label: '系统任务栏显示' },
    { key: 'closeToTray', label: '关闭时最小化到托盘' },
    { key: 'autoHide', label: '失焦隐藏' },
    { key: 'showTabBar', label: '显示标签栏' },
    { key: 'noImageMode', label: '无图模式' },
    { key: 'transparentBackground', label: '软件背景透明' },
    { key: 'antiScreenshotMode', label: '防截屏模式' },
]

const rightToggleKeys = [
    { key: 'alwaysOnTop', label: '窗口始终置顶' },
    { key: 'mobileMode', label: '手机模式' },
    { key: 'hoverHeaderMode', label: '标题栏悬停' },
    { key: 'pageTransparentMode', label: '网页背景透明' },
    { key: 'grayscaleMode', label: '灰度模式' },
    { key: 'clickThroughMode', label: '鼠标穿透' },
    { key: 'disableWindowShadow', label: '禁用窗体阴影' },
]

const activeTab = computed(() => tabs.value.find((tab) => tab.id === activeTabId.value) ?? tabs.value[0])

const filteredRecentVisits = computed(() => {
    const keyword = searchKeyword.value.trim()
    if (!keyword) {
        return recentVisits.value
    }

    return recentVisits.value.filter((item) => item.title.includes(keyword) || item.url.includes(keyword))
})

const shellClasses = computed(() => ({
    'mode-grayscale': settings.grayscaleMode,
    'mode-mobile': settings.mobileMode,
    'mode-hover-header': settings.hoverHeaderMode,
    'mode-transparent-page': settings.pageTransparentMode,
    'mode-no-image': settings.noImageMode,
}))

const themeVars = computed(() => ({
    '--header-tint': settings.statusBarColor,
    '--shell-alpha': String(settings.transparentBackground ? Math.max(0.78, 0.98 - settings.transparency / 240) : 0.98),
    '--surface-alpha': String(settings.transparentBackground ? Math.max(0.84, 0.98 - settings.transparency / 260) : 0.97),
    '--page-alpha': String(settings.pageTransparentMode ? Math.max(0.76, 0.97 - settings.transparency / 240) : 0.96),
}))

let syncTimer = null
let removeSettingsListener = null
let initialized = false

const immediateSyncKeys = new Set([
    'showInTaskbar',
    'closeToTray',
    'autoHide',
    'showTabBar',
    'noImageMode',
    'transparentBackground',
    'antiScreenshotMode',
    'alwaysOnTop',
    'mobileMode',
    'hoverHeaderMode',
    'pageTransparentMode',
    'grayscaleMode',
    'clickThroughMode',
    'disableWindowShadow',
    'statusBarColor',
])

function normalizeUrl(rawUrl) {
    const trimmed = rawUrl.trim()
    if (!trimmed) {
        return 'about:blank'
    }

    if (trimmed === 'about:blank') {
        return trimmed
    }

    return /^https?:\/\//i.test(trimmed) ? trimmed : `https://${trimmed}`
}

function getTitleFromUrl(url) {
    if (url === 'about:blank') {
        return '新标签页'
    }

    try {
        return new URL(url).hostname.replace(/^www\./, '')
    } catch {
        return '未命名页面'
    }
}

function syncSettingsNow() {
    if (!desktopApi?.updateSettings) {
        return Promise.resolve()
    }

    return desktopApi.updateSettings({ ...settings }).then((nextSettings) => {
        Object.assign(settings, nextSettings)
    }).catch(() => {
        statusMessage.value = '设置同步失败，请重试'
    })
}

function scheduleSettingsSync() {
    window.clearTimeout(syncTimer)
    syncTimer = window.setTimeout(syncSettingsNow, 120)
}

function patchSetting(key, value) {
    settings[key] = value

    if (key === 'clickThroughMode' && value) {
        statusMessage.value = `已开启鼠标穿透，可按 ${settings.clickThroughShortcut} 关闭`
    }

    if (key === 'alwaysOnTop') {
        statusMessage.value = value ? '窗口已置顶' : '窗口已取消置顶'
    }

    if (key === 'closeToTray') {
        statusMessage.value = value ? '关闭主窗口时将最小化到托盘' : '关闭主窗口时将直接退出应用'
    }

    if (key === 'showInTaskbar') {
        statusMessage.value = value ? '已显示任务栏图标' : '已隐藏任务栏图标'
    }

    if (immediateSyncKeys.has(key)) {
        syncSettingsNow()
        return
    }

    scheduleSettingsSync()
}

function toggleSetting(key) {
    patchSetting(key, !settings[key])
}

function handleOpenUrl() {
    const normalizedUrl = normalizeUrl(urlInput.value)
    const nextId = Date.now()
    const title = getTitleFromUrl(normalizedUrl)

    tabs.value.push({
        id: nextId,
        title: title === '新标签页' ? '新' : title,
        url: normalizedUrl,
        subtitle: normalizedUrl,
        kind: normalizedUrl === 'about:blank' ? 'dashboard' : 'page',
    })

    activeTabId.value = nextId
    settings.defaultUrl = normalizedUrl
    scheduleSettingsSync()

    if (normalizedUrl !== 'about:blank') {
        recentVisits.value.unshift({ title, url: normalizedUrl })
        const deduped = []
        const seen = new Set()
        recentVisits.value.forEach((item) => {
            if (!seen.has(item.url)) {
                seen.add(item.url)
                deduped.push(item)
            }
        })
        recentVisits.value = deduped.slice(0, 10)
    }

    statusMessage.value = `已在新标签打开 ${normalizedUrl}`
}

function addNewTab() {
    const nextId = Date.now()
    tabs.value.push({
        id: nextId,
        title: '新',
        url: 'about:blank',
        subtitle: '新标签页',
        kind: 'dashboard',
    })
    activeTabId.value = nextId
}

function selectTab(tabId) {
    activeTabId.value = tabId
    const currentTab = tabs.value.find((tab) => tab.id === tabId)
    if (currentTab) {
        urlInput.value = currentTab.url
    }
}

function closeTab(tabId) {
    if (tabs.value.length === 1) {
        tabs.value = [{ id: 1, title: '新', url: 'about:blank', subtitle: '新标签页', kind: 'dashboard' }]
        activeTabId.value = 1
        urlInput.value = 'about:blank'
        return
    }

    tabs.value = tabs.value.filter((tab) => tab.id !== tabId)
    if (activeTabId.value === tabId) {
        activeTabId.value = tabs.value[0].id
        urlInput.value = tabs.value[0].url
    }
}

function useQuickLink(site) {
    urlInput.value = site.url
    handleOpenUrl()
}

function useFavoriteSite(site) {
    urlInput.value = site.url
    handleOpenUrl()
}

function openRecentVisit(item) {
    urlInput.value = item.url
    handleOpenUrl()
}

function handleMinimize() {
    if (!desktopApi?.minimizeWindow) {
        statusMessage.value = '当前环境不支持最小化窗口'
        return
    }

    desktopApi.minimizeWindow().catch(() => {
        statusMessage.value = '最小化失败，请重试'
    })
}

function handleClose() {
    if (!desktopApi?.closeWindow) {
        statusMessage.value = '当前环境不支持关闭窗口'
        return
    }

    desktopApi.closeWindow().catch(() => {
        statusMessage.value = '关闭窗口失败，请重试'
    })
}

function openSettingsWindow() {
    if (!desktopApi?.openSettingsWindow) {
        statusMessage.value = '当前环境不支持打开独立设置窗口'
        return
    }

    desktopApi.openSettingsWindow().catch(() => {
        statusMessage.value = '打开设置窗口失败，请重试'
    })
}

function initializeDesktopApp() {
    if (initialized) {
        return
    }

    initialized = true

    if (desktopApi?.getSettings) {
        desktopApi.getSettings().then((nextSettings) => {
            Object.assign(settings, nextSettings)
            urlInput.value = settings.defaultUrl
        })
    }

    if (desktopApi?.onSettingsChanged) {
        removeSettingsListener = desktopApi.onSettingsChanged((nextSettings) => {
            Object.assign(settings, nextSettings)
        })
    }
}

function disposeDesktopApp() {
    window.clearTimeout(syncTimer)
    removeSettingsListener?.()
    removeSettingsListener = null
    initialized = false
}

export function useDesktopApp() {
    return {
        quickLinks,
        favoriteSites,
        recentVisits,
        settings,
        urlInput,
        searchKeyword,
        statusMessage,
        activeTabId,
        tabs,
        leftToggleKeys,
        rightToggleKeys,
        activeTab,
        filteredRecentVisits,
        shellClasses,
        themeVars,
        patchSetting,
        toggleSetting,
        handleOpenUrl,
        addNewTab,
        selectTab,
        closeTab,
        useQuickLink,
        useFavoriteSite,
        openRecentVisit,
        handleMinimize,
        handleClose,
        openSettingsWindow,
        initializeDesktopApp,
        disposeDesktopApp,
    }
}
