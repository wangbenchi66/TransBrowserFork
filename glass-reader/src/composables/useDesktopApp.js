import { computed, reactive, ref } from 'vue'

const desktopApi = typeof window !== 'undefined' ? window.desktop : null

const recommendedSites = ref([
    { id: 1, name: '微信读书', url: 'https://weread.qq.com', system: true },
    { id: 2, name: '番茄小说', url: 'https://fanqienovel.com', system: true },
    { id: 3, name: 'Bilibili', url: 'https://www.bilibili.com', system: true },
    { id: 4, name: '抖音', url: 'https://www.douyin.com', system: true },
    { id: 5, name: '小红书', url: 'https://www.xiaohongshu.com', system: true },
])

const RECOMMENDED_STORAGE_KEY = 'glass_reader_recommended_sites'

// 尝试从 localStorage 或者持久化设置中恢复推荐站点
try {
    const raw = localStorage.getItem(RECOMMENDED_STORAGE_KEY)
    if (raw) {
        const parsed = JSON.parse(raw)
        if (Array.isArray(parsed) && parsed.length) {
            // 保证每项都有 id
            recommendedSites.value = parsed.map((it, idx) => ({ id: it.id ?? (Date.now() + idx), ...it }))
        }
    }
} catch (e) {
    // ignore
}
const localDocuments = ref([])

// 初始最近访问保持空，使用真实浏览行为填充
const recentVisits = ref([])

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
    forcePageTransparent: false,
    grayscaleMode: false,
    clickThroughMode: false,
    closeToTray: false,
    alwaysOnTop: false,
    autoScrollEnabled: false,
    autoScrollSpeed: 22,
    readerTextColor: '#283247',
    readerFontScale: 100,
    statusBarColor: '#f5f5f7',
    showScrollbars: true,
    defaultUrl: '',
    bossKey: 'Alt+Q',
    decreaseTransparencyShortcut: 'Alt+Up',
    increaseTransparencyShortcut: 'Alt+Down',
    clickThroughShortcut: 'Ctrl+Alt+T',
    fullWindowTransparent: false,
}

const settings = reactive({ ...defaultSettings })
const urlInput = ref(defaultSettings.defaultUrl)
const searchKeyword = ref('')
const siteSearchKeyword = ref('')
const statusMessage = ref('Alt+Q 可快速隐藏或恢复窗口')
const activeTabId = ref(1)
const tabs = ref([
    {
        id: 1,
        title: '工作台',
        url: 'about:blank',
        subtitle: '新标签页',
        kind: 'dashboard',
    },
])

const leftToggleKeys = [
    { key: 'showInTaskbar', label: '系统任务栏显示' },
    { key: 'closeToTray', label: '关闭时最小化到托盘' },
    { key: 'autoHide', label: '鼠标移出隐藏' },
    { key: 'showTabBar', label: '显示标签栏' },
    // 将设置中的“软件背景透明”替换为标题栏的“透明”按钮功能
    { key: 'fullWindowTransparent', label: '软件背景透明' },
    { key: 'antiScreenshotMode', label: '防截屏模式' },
]

const rightToggleKeys = [
    { key: 'alwaysOnTop', label: '窗口始终置顶' },
    { key: 'mobileMode', label: '手机模式' },
    { key: 'hoverHeaderMode', label: '标题栏悬停' },
    { key: 'pageTransparentMode', label: '网页背景透明' },
    { key: 'forcePageTransparent', label: '强制网页透明' },
    { key: 'showScrollbars', label: '显示滚动条' },
    { key: 'grayscaleMode', label: '灰度模式' },
    { key: 'noImageMode', label: '无图模式' },
    { key: 'clickThroughMode', label: '鼠标穿透' },
]

// controlToggleKeys 已移除为导出项，UI 不再直接引用该集合

const activeTab = computed(() => tabs.value.find((tab) => tab.id === activeTabId.value) ?? tabs.value[0])

const filteredRecentVisits = computed(() => {
    const keyword = searchKeyword.value.trim()
    if (!keyword) {
        return recentVisits.value
    }

    return recentVisits.value.filter((item) => item.title.includes(keyword) || item.url.includes(keyword))
})

const filteredRecommendedSites = computed(() => {
    const keyword = siteSearchKeyword.value.trim().toLowerCase()
    if (!keyword) {
        return recommendedSites.value
    }

    return recommendedSites.value.filter((site) => [site.name, site.url].join(' ').toLowerCase().includes(keyword))
})

const dashboardMetrics = computed(() => ([
    { label: '推荐站点', value: String(recommendedSites.value.length).padStart(2, '0') },
    { label: '本地文档', value: String(localDocuments.value.length).padStart(2, '0') },
    { label: '最近访问', value: String(recentVisits.value.length).padStart(2, '0') },
]))

const shellClasses = computed(() => ({
    'mode-grayscale': settings.grayscaleMode,
    'mode-mobile': settings.mobileMode,
    'mode-hover-header': settings.hoverHeaderMode,
    'mode-transparent-page': settings.pageTransparentMode,
    'mode-no-image': settings.noImageMode,
}))

const themeVars = computed(() => {
    // 计算不同区域（shell / surface / page）的透明度
    const shellAlpha = settings.transparentBackground
        ? Math.max(0.05, 0.95 - settings.transparency / 90)
        : Math.max(0.70, 0.98 - settings.transparency / 130)

    const surfaceAlpha = settings.transparentBackground
        ? Math.max(0.08, 0.92 - settings.transparency / 95)
        : Math.max(0.75, 0.97 - settings.transparency / 140)

    // 计算页面透明度：在启用“软件背景透明”时，页面内容的透明度
    // 不应随全局透明度滑块变化，否则滑块会同时影响背景和页面。
    // 此时我们让滑块控制窗口整体的不透明度（在主进程通过 setOpacity 应用）。
    let pageAlpha;
    if (settings.fullWindowTransparent) {
        // 固定页面内容的默认可见度，仅受 pageTransparentMode 影响
        pageAlpha = settings.pageTransparentMode ? Math.max(0.04, 0.94) : Math.max(0.72, 0.96);
    } else {
        pageAlpha = settings.pageTransparentMode
            ? Math.max(0.04, 0.94 - settings.transparency / 88)
            : Math.max(0.72, 0.96 - settings.transparency / 145);
    }

    if (settings.fullWindowTransparent) {
        return {
            '--header-tint': settings.statusBarColor,
            '--shell-alpha': '0',
            '--surface-alpha': '0',
            '--page-alpha': String(pageAlpha),
            '--reader-text-color': settings.readerTextColor,
            '--reader-font-scale': `${settings.readerFontScale}%`,
        }
    }

    return {
        '--header-tint': settings.statusBarColor,
        '--shell-alpha': String(shellAlpha),
        '--surface-alpha': String(surfaceAlpha),
        '--page-alpha': String(pageAlpha),
        '--reader-text-color': settings.readerTextColor,
        '--reader-font-scale': `${settings.readerFontScale}%`,
    }
})

let syncTimer = null
let removeSettingsListener = null
let initialized = false
// 当 fullWindowTransparent 启用时，我们可能会临时打开 pageTransparentMode


const immediateSyncKeys = new Set([
    'transparency',
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
    'statusBarColor',
    'autoScrollEnabled',
    'autoScrollSpeed',
    'readerTextColor',
    'readerFontScale',
    'forcePageTransparent',
    'showScrollbars',
    'fullWindowTransparent',
    // 全局快捷键需要立即同步并在主进程重新注册
    'bossKey',
    'decreaseTransparencyShortcut',
    'increaseTransparencyShortcut',
    'clickThroughShortcut',
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
        return '工作台'
    }

    try {
        return new URL(url).hostname.replace(/^www\./, '')
    } catch {
        return '未命名页面'
    }
}

/** getSiteTag removed — recommendedSites now only store `name` and `url`. */

function displayInputUrlForUI(url) {
    return url === 'about:blank' ? '' : url
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
    if (key === 'transparency') {
        const nextVal = Math.max(0, Math.min(85, Number(value) || 0))
        settings[key] = nextVal
        console.log('[renderer] patchSetting transparency ->', nextVal, { hasSetTransparency: !!desktopApi?.setTransparency })
        if (desktopApi?.log) {
            try {
                desktopApi.log(`[renderer] patchSetting transparency -> ${nextVal}`)
            } catch (e) { }
        }
        // 优先调用主进程的快捷接口立即应用透明度，保证滑块有即时视觉反馈
        if (desktopApi?.setTransparency) {
            // 发快速 IPC 以立即生效
            desktopApi.setTransparency(nextVal).then((nextSettings) => {
                if (nextSettings && typeof nextSettings === 'object') {
                    Object.assign(settings, nextSettings)
                }
                // 额外调用 updateSettings 以确保持久化和其它设置同步
                if (desktopApi?.updateSettings) {
                    desktopApi.updateSettings({ ...settings }).then((ns) => {
                        if (ns && typeof ns === 'object') {
                            Object.assign(settings, ns)
                        }
                    }).catch(() => {
                        statusMessage.value = '设置同步失败，请重试'
                    })
                }
            }).catch(() => {
                statusMessage.value = '设置同步失败，请重试'
            })
            return
        }
    } else if (key === 'autoScrollSpeed') {
        settings[key] = Math.max(5, Math.min(80, Number(value) || 5))
    } else if (key === 'readerFontScale') {
        settings[key] = Math.max(80, Math.min(160, Number(value) || 100))
    } else {
        settings[key] = value
    }

    if (key === 'clickThroughMode' && settings[key]) {
        statusMessage.value = `已开启鼠标穿透，可按 ${settings.clickThroughShortcut} 关闭`
    }

    if (key === 'alwaysOnTop') {
        statusMessage.value = settings[key] ? '窗口已置顶' : '窗口已取消置顶'
    }

    if (key === 'closeToTray') {
        statusMessage.value = settings[key] ? '关闭主窗口时将最小化到托盘' : '关闭主窗口时将直接退出应用'
    }

    if (key === 'showInTaskbar') {
        statusMessage.value = settings[key] ? '已显示任务栏图标' : '已隐藏任务栏图标'
    }

    if (key === 'autoHide') {
        statusMessage.value = settings[key] ? '鼠标移出后会自动隐藏，移回原区域自动恢复' : '已关闭自动隐藏'
    }

    if (key === 'hoverHeaderMode') {
        statusMessage.value = settings[key] ? '标题栏改为悬停显示' : '标题栏常驻显示'
    }

    if (key === 'autoScrollEnabled') {
        statusMessage.value = settings[key] ? '自动滚动已开启' : '自动滚动已关闭'
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

function pushRecentVisit(entry) {
    recentVisits.value.unshift(entry)
    const deduped = []
    const seen = new Set()

    recentVisits.value.forEach((item) => {
        const dedupeKey = `${item.type ?? 'site'}:${item.url}`
        if (!seen.has(dedupeKey)) {
            seen.add(dedupeKey)
            deduped.push(item)
        }
    })

    recentVisits.value = deduped.slice(0, 12)
}

function openTab(tab) {
    tabs.value.push(tab)
    activeTabId.value = tab.id
}

function createPageTab(url, options = {}) {
    const normalizedUrl = normalizeUrl(url)
    const nextId = Date.now() + Math.floor(Math.random() * 1000)
    const title = options.title ?? getTitleFromUrl(normalizedUrl)

    openTab({
        id: nextId,
        title: title === '工作台' ? '新' : title,
        url: normalizedUrl,
        subtitle: options.subtitle ?? normalizedUrl,
        kind: options.kind ?? (normalizedUrl === 'about:blank' ? 'dashboard' : 'page'),
        documentId: options.documentId,
        objectUrl: options.objectUrl,
        fileName: options.fileName,
        mimeType: options.mimeType,
    })

    settings.defaultUrl = normalizedUrl
    scheduleSettingsSync()

    if (normalizedUrl !== 'about:blank') {
        pushRecentVisit({ title, url: normalizedUrl, type: options.kind === 'local-file' ? 'file' : 'site' })
    }

    statusMessage.value = `已打开 ${title}`
}

function createLocalTextTab(fileName, content, documentId) {
    const nextId = Date.now() + Math.floor(Math.random() * 1000)

    openTab({
        id: nextId,
        title: fileName.replace(/\.[^.]+$/, ''),
        url: `local://${fileName}`,
        subtitle: fileName,
        kind: 'local-text',
        content,
        fileName,
        documentId,
    })

    pushRecentVisit({ title: fileName, url: `local://${fileName}`, type: 'file' })
    statusMessage.value = `已打开本地文档 ${fileName}`
}

function handleOpenUrl() {
    createPageTab(urlInput.value)
}

function addNewTab() {
    const nextId = Date.now() + Math.floor(Math.random() * 1000)
    openTab({
        id: nextId,
        title: '工作台',
        url: 'about:blank',
        subtitle: '新标签页',
        kind: 'dashboard',
    })
    urlInput.value = ''
}

function selectTab(tabId) {
    activeTabId.value = tabId
    const currentTab = tabs.value.find((tab) => tab.id === tabId)
    if (currentTab) {
        urlInput.value = displayInputUrlForUI(currentTab.url)
    }
}

function closeTab(tabId) {
    const closingTab = tabs.value.find((tab) => tab.id === tabId)
    if (closingTab?.objectUrl) {
        URL.revokeObjectURL(closingTab.objectUrl)
    }

    if (tabs.value.length === 1) {
        tabs.value = [{ id: 1, title: '工作台', url: 'about:blank', subtitle: '新标签页', kind: 'dashboard' }]
        activeTabId.value = 1
        urlInput.value = ''
        return
    }

    tabs.value = tabs.value.filter((tab) => tab.id !== tabId)
    if (activeTabId.value === tabId) {
        activeTabId.value = tabs.value[0].id
        urlInput.value = displayInputUrlForUI(tabs.value[0].url)
    }
}



function useRecommendedSite(site) {
    urlInput.value = site.url
    createPageTab(site.url, { title: site.name, subtitle: getTitleFromUrl(site.url) })
}

function openRecentVisit(item) {
    if ((item.type ?? 'site') === 'file') {
        const documentItem = localDocuments.value.find((doc) => `local://${doc.fileName}` === item.url || doc.url === item.url)
        if (documentItem) {
            openLocalDocument(documentItem)
            return
        }
    }
    urlInput.value = displayInputUrlForUI(item.url)
    createPageTab(item.url, { title: item.title })
}

async function uploadLocalFiles(fileList) {
    const files = Array.from(fileList ?? [])
    if (!files.length) {
        return
    }

    for (const file of files) {
        const extension = file.name.split('.').pop()?.toLowerCase() ?? ''
        const documentId = Date.now() + Math.floor(Math.random() * 1000)

        if (['txt', 'md', 'markdown', 'log', 'json'].includes(extension) || file.type.startsWith('text/')) {
            const content = await file.text()
            localDocuments.value.unshift({
                id: documentId,
                fileName: file.name,
                type: 'local-text',
                content,
                size: file.size,
            })
            createLocalTextTab(file.name, content, documentId)
            continue
        }

        const objectUrl = URL.createObjectURL(file)
        localDocuments.value.unshift({
            id: documentId,
            fileName: file.name,
            type: 'local-file',
            url: objectUrl,
            objectUrl,
            mimeType: file.type,
            size: file.size,
        })
        createPageTab(objectUrl, {
            title: file.name.replace(/\.[^.]+$/, ''),
            subtitle: file.name,
            kind: 'page',
            objectUrl,
            fileName: file.name,
            mimeType: file.type,
            documentId,
        })
    }

    localDocuments.value = localDocuments.value.slice(0, 12)
}

function openLocalDocument(documentItem) {
    if (documentItem.type === 'local-text') {
        createLocalTextTab(documentItem.fileName, documentItem.content, documentItem.id)
        return
    }

    createPageTab(documentItem.url, {
        title: documentItem.fileName.replace(/\.[^.]+$/, ''),
        subtitle: documentItem.fileName,
        objectUrl: documentItem.objectUrl,
        fileName: documentItem.fileName,
        mimeType: documentItem.mimeType,
        documentId: documentItem.id,
    })
}

function removeLocalDocument(documentId) {
    const documentItem = localDocuments.value.find((item) => item.id === documentId)
    if (documentItem?.objectUrl) {
        URL.revokeObjectURL(documentItem.objectUrl)
    }
    // removeLocalDocument 已移除为导出函数（UI 未直接调用），保留对对象 URL 的回收逻辑
    localDocuments.value = localDocuments.value.filter((item) => item.id !== documentId)
    tabs.value = tabs.value.filter((tab) => tab.documentId !== documentId)

    if (!tabs.value.length) {
        tabs.value = [{ id: 1, title: '工作台', url: 'about:blank', subtitle: '新标签页', kind: 'dashboard' }]
        activeTabId.value = 1
    } else if (!tabs.value.some((tab) => tab.id === activeTabId.value)) {
        activeTabId.value = tabs.value[0].id
    }
}

function persistRecommendedSites() {
    try {
        localStorage.setItem(RECOMMENDED_STORAGE_KEY, JSON.stringify(recommendedSites.value))
    } catch (e) {
        // ignore
    }

    if (desktopApi?.updateSettings) {
        try {
            desktopApi.updateSettings({ recommendedSites: recommendedSites.value }).catch(() => { })
        } catch (e) { }
    }
}

function addRecommendedSite(site) {
    const normalizedUrl = normalizeUrl((site.url || '').trim())
    const name = (site.name || '').trim() || getTitleFromUrl(normalizedUrl)

    // 重复检测（按 URL 或 名称）
    const exists = recommendedSites.value.some((s) => {
        try {
            return s.url === normalizedUrl || (s.name && s.name.toLowerCase() === name.toLowerCase())
        } catch {
            return false
        }
    })

    if (exists) {
        statusMessage.value = '推荐站点已存在'
        return null
    }

    const next = {
        id: Date.now() + Math.floor(Math.random() * 1000),
        name,
        url: normalizedUrl,
        system: false,
    }

    // 将自定义站点插入到 system 站点之后，保持系统推荐靠前
    const sysCount = recommendedSites.value.filter((s) => s.system).length
    recommendedSites.value.splice(sysCount, 0, next)
    persistRecommendedSites()
    statusMessage.value = `已添加推荐站点：${name}`
    return next
}

function editRecommendedSite(id, site) {
    const idx = recommendedSites.value.findIndex((s) => s.id === id)
    if (idx === -1) return

    // 不允许编辑标记为 system 的内置站点
    if (recommendedSites.value[idx].system) {
        statusMessage.value = '系统站点不可编辑'
        return
    }

    const existing = recommendedSites.value[idx]
    const normalizedUrl = normalizeUrl(site.url ?? existing.url)
    const name = site.name ?? existing.name

    recommendedSites.value[idx] = {
        ...existing,
        name,
        url: normalizedUrl,
    }

    persistRecommendedSites()
}

function removeRecommendedSite(id) {
    const idx = recommendedSites.value.findIndex((s) => s.id === id)
    if (idx === -1) return
    // 不允许删除标记为 system 的内置站点
    if (recommendedSites.value[idx].system) {
        return
    }
    recommendedSites.value.splice(idx, 1)
    persistRecommendedSites()
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

function handleMaximize() {
    if (!desktopApi?.maximizeWindow) {
        statusMessage.value = '当前环境不支持最大化窗口'
        return
    }

    desktopApi.maximizeWindow().catch(() => {
        statusMessage.value = '最大化失败，请重试'
    })
}

function handleClose() {
    if (!desktopApi?.quitWindow && !desktopApi?.closeWindow) {
        statusMessage.value = '当前环境不支持关闭窗口'
        return
    }

    const closeAction = desktopApi.quitWindow ?? desktopApi.closeWindow
    closeAction().catch(() => {
        statusMessage.value = '关闭窗口失败，请重试'
    })
}

function openSettingsWindow() {
    // openSettingsWindow 保留实现但不再作为导出（设置通过内置 modal 控制）
    if (!desktopApi?.openSettingsWindow) {
        statusMessage.value = '当前环境不支持打开设置'
        return
    }

    desktopApi.openSettingsWindow().catch(() => {
        statusMessage.value = '打开设置失败，请重试'
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
            urlInput.value = displayInputUrlForUI(settings.defaultUrl ?? '')
            if (Array.isArray(nextSettings.recommendedSites) && nextSettings.recommendedSites.length) {
                recommendedSites.value = nextSettings.recommendedSites.map((it, idx) => ({ id: it.id ?? (Date.now() + idx), ...it }))
            }
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
        filteredRecommendedSites,
        recommendedSites,
        settings,
        urlInput,
        searchKeyword,
        siteSearchKeyword,
        statusMessage,
        activeTabId,
        tabs,
        leftToggleKeys,
        rightToggleKeys,
        activeTab,
        filteredRecentVisits,
        dashboardMetrics,
        shellClasses,
        themeVars,
        patchSetting,
        toggleSetting,
        handleOpenUrl,
        addNewTab,
        selectTab,
        closeTab,
        useRecommendedSite,
        openRecentVisit,
        uploadLocalFiles,
        openLocalDocument,
        addRecommendedSite,
        editRecommendedSite,
        removeRecommendedSite,
        handleMinimize,
        handleMaximize,
        handleClose,
        initializeDesktopApp,
        disposeDesktopApp,
    }
}
