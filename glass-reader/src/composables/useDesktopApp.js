import { computed, reactive, ref } from 'vue'

const desktopApi = typeof window !== 'undefined' ? window.desktop : null

const quickLinks = [
    { name: '微信读书', tag: '书', tone: 'green', desc: '沉浸式看书', url: 'https://weread.qq.com' },
    { name: 'Bilibili', tag: '站', tone: 'pink', desc: '视频摸鱼', url: 'https://www.bilibili.com' },
    { name: '抖音', tag: '抖', tone: 'ink', desc: '短视频信息流', url: 'https://www.douyin.com' },
    { name: '知乎', tag: '知', tone: 'blue', desc: '问答与热榜', url: 'https://www.zhihu.com' },
]

const recommendedSites = ref([
    { name: '微信读书', tag: 'WR', tone: 'green', category: '阅读', hint: '小说与出版书', url: 'https://weread.qq.com' },
    { name: '番茄小说', tag: 'FQ', tone: 'orange', category: '阅读', hint: '网文阅读', url: 'https://fanqienovel.com' },
    { name: '晋江文学城', tag: 'JJ', tone: 'rose', category: '阅读', hint: '女性向小说', url: 'https://wap.jjwxc.net' },
    { name: 'Bilibili', tag: 'BL', tone: 'pink', category: '娱乐', hint: '视频与直播', url: 'https://www.bilibili.com' },
    { name: '抖音', tag: 'DY', tone: 'ink', category: '娱乐', hint: '短视频', url: 'https://www.douyin.com' },
    { name: '小红书', tag: 'XH', tone: 'rose', category: '娱乐', hint: '生活方式', url: 'https://www.xiaohongshu.com' },
    { name: '粉笔', tag: 'FB', tone: 'blue', category: '学习', hint: '刷题备考', url: 'https://www.fenbi.com' },
    { name: 'GitHub', tag: 'GH', tone: 'slate', category: '学习', hint: '技术文档', url: 'https://github.com' },
])

const favoriteSites = ref([
    { id: 1, name: '百度', tag: 'BD', url: 'https://www.baidu.com' },
    { id: 2, name: '豆瓣读书', tag: 'DB', url: 'https://book.douban.com' },
    { id: 3, name: '少数派', tag: 'SS', url: 'https://sspai.com' },
    { id: 4, name: '掘金', tag: 'JG', url: 'https://juejin.cn' },
])

const localDocuments = ref([])

const recentVisits = ref([
    { title: '微信读书', url: 'https://weread.qq.com', type: 'site' },
    { title: '知乎热榜', url: 'https://www.zhihu.com/hot', type: 'site' },
    { title: 'Bilibili 首页', url: 'https://www.bilibili.com', type: 'site' },
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
    forcePageTransparent: false,
    grayscaleMode: false,
    clickThroughMode: false,
    closeToTray: false,
    disableWindowShadow: false,
    alwaysOnTop: false,
    autoScrollEnabled: false,
    autoScrollSpeed: 22,
    readerTextColor: '#283247',
    readerFontScale: 100,
    statusBarColor: '#f5f5f7',
    defaultUrl: 'about:blank',
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
const customSiteName = ref('')
const customSiteUrl = ref('')
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
    { key: 'transparentBackground', label: '软件背景透明' },
    { key: 'antiScreenshotMode', label: '防截屏模式' },
]

const rightToggleKeys = [
    { key: 'alwaysOnTop', label: '窗口始终置顶' },
    { key: 'mobileMode', label: '手机模式' },
    { key: 'hoverHeaderMode', label: '标题栏悬停' },
    { key: 'pageTransparentMode', label: '网页背景透明' },
    { key: 'forcePageTransparent', label: '强制网页透明' },
    { key: 'grayscaleMode', label: '灰度模式' },
    { key: 'noImageMode', label: '无图模式' },
    { key: 'clickThroughMode', label: '鼠标穿透' },
    { key: 'disableWindowShadow', label: '禁用窗体阴影' },
]

const controlToggleKeys = [
    { key: 'pageTransparentMode', label: '网页透明' },
    { key: 'transparentBackground', label: '窗口透明' },
    { key: 'noImageMode', label: '隐藏图片' },
    { key: 'autoHide', label: '移出隐藏' },
    { key: 'alwaysOnTop', label: '窗口置顶' },
]

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

    return recommendedSites.value.filter((site) => [site.name, site.category, site.hint, site.url].join(' ').toLowerCase().includes(keyword))
})

const dashboardMetrics = computed(() => ([
    { label: '推荐站点', value: String(recommendedSites.value.length).padStart(2, '0') },
    { label: '我的站点', value: String(favoriteSites.value.length).padStart(2, '0') },
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
    if (settings.fullWindowTransparent) {
        return {
            '--header-tint': settings.statusBarColor,
            '--shell-alpha': '0',
            '--surface-alpha': '0',
            '--page-alpha': '0',
            '--reader-text-color': settings.readerTextColor,
            '--reader-font-scale': `${settings.readerFontScale}%`,
        }
    }

    return {
        '--header-tint': settings.statusBarColor,
        '--shell-alpha': String(settings.transparentBackground
            ? Math.max(0.05, 0.95 - settings.transparency / 90)
            : Math.max(0.70, 0.98 - settings.transparency / 130)),
        '--surface-alpha': String(settings.transparentBackground
            ? Math.max(0.08, 0.92 - settings.transparency / 95)
            : Math.max(0.75, 0.97 - settings.transparency / 140)),
        '--page-alpha': String(settings.pageTransparentMode
            ? Math.max(0.04, 0.94 - settings.transparency / 88)
            : Math.max(0.72, 0.96 - settings.transparency / 145)),
        '--reader-text-color': settings.readerTextColor,
        '--reader-font-scale': `${settings.readerFontScale}%`,
    }
})

let syncTimer = null
let removeSettingsListener = null
let initialized = false
// 当 fullWindowTransparent 启用时，我们可能会临时打开 pageTransparentMode
let _prevPageTransparent = null

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
    'disableWindowShadow',
    'statusBarColor',
    'autoScrollEnabled',
    'autoScrollSpeed',
    'readerTextColor',
    'readerFontScale',
    'forcePageTransparent',
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

function getSiteTag(name) {
    return name.replace(/\s+/g, '').slice(0, 2).toUpperCase() || 'ST'
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
    urlInput.value = 'about:blank'
}

function selectTab(tabId) {
    activeTabId.value = tabId
    const currentTab = tabs.value.find((tab) => tab.id === tabId)
    if (currentTab) {
        urlInput.value = currentTab.url
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
    createPageTab(site.url, { title: site.name, subtitle: site.desc })
}

function useFavoriteSite(site) {
    urlInput.value = site.url
    createPageTab(site.url, { title: site.name, subtitle: site.url })
}

function useRecommendedSite(site) {
    urlInput.value = site.url
    createPageTab(site.url, { title: site.name, subtitle: `${site.category} · ${site.hint}` })
}

function openRecentVisit(item) {
    if ((item.type ?? 'site') === 'file') {
        const documentItem = localDocuments.value.find((doc) => `local://${doc.fileName}` === item.url || doc.url === item.url)
        if (documentItem) {
            openLocalDocument(documentItem)
            return
        }
    }

    urlInput.value = item.url
    createPageTab(item.url, { title: item.title })
}

function addFavoriteSite() {
    const name = customSiteName.value.trim()
    const url = normalizeUrl(customSiteUrl.value)

    if (!name || url === 'about:blank') {
        statusMessage.value = '请填写站点名称和有效网址'
        return
    }

    favoriteSites.value.unshift({
        id: Date.now(),
        name,
        tag: getSiteTag(name),
        url,
    })

    favoriteSites.value = favoriteSites.value.slice(0, 12)
    customSiteName.value = ''
    customSiteUrl.value = ''
    statusMessage.value = `已添加我的站点：${name}`
}

function removeFavoriteSite(siteId) {
    favoriteSites.value = favoriteSites.value.filter((site) => site.id !== siteId)
    statusMessage.value = '已移除站点'
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

    localDocuments.value = localDocuments.value.filter((item) => item.id !== documentId)
    tabs.value = tabs.value.filter((tab) => tab.documentId !== documentId)

    if (!tabs.value.length) {
        tabs.value = [{ id: 1, title: '工作台', url: 'about:blank', subtitle: '新标签页', kind: 'dashboard' }]
        activeTabId.value = 1
    } else if (!tabs.value.some((tab) => tab.id === activeTabId.value)) {
        activeTabId.value = tabs.value[0].id
    }
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
        recommendedSites,
        filteredRecommendedSites,
        favoriteSites,
        localDocuments,
        recentVisits,
        settings,
        urlInput,
        searchKeyword,
        siteSearchKeyword,
        customSiteName,
        customSiteUrl,
        statusMessage,
        activeTabId,
        tabs,
        leftToggleKeys,
        rightToggleKeys,
        controlToggleKeys,
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
        useQuickLink,
        useFavoriteSite,
        useRecommendedSite,
        openRecentVisit,
        addFavoriteSite,
        removeFavoriteSite,
        uploadLocalFiles,
        openLocalDocument,
        removeLocalDocument,
        handleMinimize,
        handleMaximize,
        handleClose,
        openSettingsWindow,
        initializeDesktopApp,
        disposeDesktopApp,
    }
}
