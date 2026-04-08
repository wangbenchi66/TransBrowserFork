import { app, BrowserWindow, globalShortcut, ipcMain, Menu, nativeImage, nativeTheme, Tray } from 'electron'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const isDev = !app.isPackaged
const isDevServerRun = process.env.npm_lifecycle_event === 'dev' || process.env.npm_lifecycle_event === 'dev:electron'
let mainWindow = null
let tray = null
let isQuitting = false
const defaultSettings = {
    transparency: 0,//透明度
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
let currentSettings = { ...defaultSettings }

function getSettingsFilePath() {
    return path.join(app.getPath('userData'), 'glass-reader-settings.json')
}

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value))
}

function normalizeColor(color) {
    if (typeof color !== 'string') {
        return defaultSettings.statusBarColor
    }

    return /^#[0-9a-fA-F]{6}$/.test(color) ? color : defaultSettings.statusBarColor
}

function normalizeSettings(partial = {}) {
    return {
        ...defaultSettings,
        ...partial,
        transparency: clamp(Number(partial.transparency ?? defaultSettings.transparency), 0, 85),
        showInTaskbar: Boolean(partial.showInTaskbar ?? defaultSettings.showInTaskbar),
        autoHide: Boolean(partial.autoHide ?? defaultSettings.autoHide),
        showTabBar: Boolean(partial.showTabBar ?? defaultSettings.showTabBar),
        noImageMode: Boolean(partial.noImageMode ?? defaultSettings.noImageMode),
        transparentBackground: Boolean(partial.transparentBackground ?? defaultSettings.transparentBackground),
        antiScreenshotMode: Boolean(partial.antiScreenshotMode ?? defaultSettings.antiScreenshotMode),
        mobileMode: Boolean(partial.mobileMode ?? defaultSettings.mobileMode),
        hoverHeaderMode: Boolean(partial.hoverHeaderMode ?? defaultSettings.hoverHeaderMode),
        pageTransparentMode: Boolean(partial.pageTransparentMode ?? defaultSettings.pageTransparentMode),
        grayscaleMode: Boolean(partial.grayscaleMode ?? defaultSettings.grayscaleMode),
        clickThroughMode: Boolean(partial.clickThroughMode ?? defaultSettings.clickThroughMode),
        closeToTray: Boolean(partial.closeToTray ?? defaultSettings.closeToTray),
        disableWindowShadow: Boolean(partial.disableWindowShadow ?? defaultSettings.disableWindowShadow),
        alwaysOnTop: Boolean(partial.alwaysOnTop ?? defaultSettings.alwaysOnTop),
        statusBarColor: normalizeColor(partial.statusBarColor ?? defaultSettings.statusBarColor),
        defaultUrl: String(partial.defaultUrl ?? defaultSettings.defaultUrl),
        bossKey: String(partial.bossKey ?? defaultSettings.bossKey),
        decreaseTransparencyShortcut: String(partial.decreaseTransparencyShortcut ?? defaultSettings.decreaseTransparencyShortcut),
        increaseTransparencyShortcut: String(partial.increaseTransparencyShortcut ?? defaultSettings.increaseTransparencyShortcut),
        clickThroughShortcut: String(partial.clickThroughShortcut ?? defaultSettings.clickThroughShortcut),
    }
}

function loadSettings() {
    try {
        const raw = fs.readFileSync(getSettingsFilePath(), 'utf-8')
        currentSettings = normalizeSettings(JSON.parse(raw))
    } catch {
        currentSettings = { ...defaultSettings }
    }
}

function saveSettings() {
    try {
        fs.writeFileSync(getSettingsFilePath(), JSON.stringify(currentSettings, null, 2), 'utf-8')
    } catch (error) {
        console.warn('[settings] save failed:', error)
    }
}

function broadcastSettings() {
    if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('settings:changed', currentSettings)
    }
}

function openSettingsPanel() {
    if (!mainWindow || mainWindow.isDestroyed()) {
        createWindow()
    }

    if (!mainWindow || mainWindow.isDestroyed()) {
        return
    }

    if (!mainWindow.isVisible()) {
        mainWindow.show()
    }

    mainWindow.focus()
    mainWindow.webContents.send('ui:open-settings')
}

function applyWindowSettings(win) {
    win.setSkipTaskbar(!currentSettings.showInTaskbar)
    win.setAlwaysOnTop(currentSettings.alwaysOnTop, 'screen-saver')
    win.setContentProtection(currentSettings.antiScreenshotMode)
    win.setIgnoreMouseEvents(currentSettings.clickThroughMode, currentSettings.clickThroughMode ? { forward: true } : undefined)
    win.setOpacity(clamp(1 - currentSettings.transparency / 200, 0.58, 1))

    if (typeof win.setHasShadow === 'function') {
        win.setHasShadow(!currentSettings.disableWindowShadow)
    }

    if (process.platform === 'win32') {
        applyWindowsAcrylic(win, currentSettings.transparentBackground)
    }
}

function toggleMainWindow() {
    if (!mainWindow || mainWindow.isDestroyed()) {
        return
    }

    if (mainWindow.isVisible()) {
        mainWindow.hide()
        return
    }

    mainWindow.show()
    mainWindow.focus()
}

function getWindowUrl(hash = '/') {
    const normalizedHash = hash.startsWith('/') ? hash : `/${hash}`
    if (isDev && isDevServerRun) {
        return `http://127.0.0.1:5173/#${normalizedHash}`
    }

    const indexPath = path.join(__dirname, '../dist/index.html')
    return `file://${indexPath.replace(/\\/g, '/')}#${normalizedHash}`
}

function createTray() {
    if (tray) {
        return
    }

    const trayIcon = nativeImage.createFromPath(process.execPath)
    tray = new Tray(trayIcon)
    tray.setToolTip('Trans Glass')

    const buildMenu = () => Menu.buildFromTemplate([
        {
            label: mainWindow && !mainWindow.isDestroyed() && mainWindow.isVisible() ? '隐藏主窗口' : '显示主窗口',
            click: () => toggleMainWindow(),
        },
        {
            label: '打开设置',
            click: () => openSettingsPanel(),
        },
        { type: 'separator' },
        {
            label: '退出',
            click: () => {
                isQuitting = true
                app.quit()
            },
        },
    ])

    tray.setContextMenu(buildMenu())
    tray.on('double-click', () => toggleMainWindow())
}

function refreshTrayMenu() {
    if (!tray) {
        return
    }

    tray.setContextMenu(Menu.buildFromTemplate([
        {
            label: mainWindow && !mainWindow.isDestroyed() && mainWindow.isVisible() ? '隐藏主窗口' : '显示主窗口',
            click: () => toggleMainWindow(),
        },
        {
            label: '打开设置',
            click: () => openSettingsPanel(),
        },
        { type: 'separator' },
        {
            label: '退出',
            click: () => {
                isQuitting = true
                app.quit()
            },
        },
    ]))
}

function updateTransparency(nextTransparency) {
    currentSettings = normalizeSettings({
        ...currentSettings,
        transparency: nextTransparency,
    })

    saveSettings()

    if (mainWindow && !mainWindow.isDestroyed()) {
        applyWindowSettings(mainWindow)
    }

    broadcastSettings()
}

function registerGlobalShortcuts() {
    globalShortcut.unregisterAll()

    const registrations = [
        [currentSettings.bossKey, () => toggleMainWindow()],
        [currentSettings.decreaseTransparencyShortcut, () => updateTransparency(currentSettings.transparency - 8)],
        [currentSettings.increaseTransparencyShortcut, () => updateTransparency(currentSettings.transparency + 8)],
        [currentSettings.clickThroughShortcut, () => {
            currentSettings = normalizeSettings({
                ...currentSettings,
                clickThroughMode: !currentSettings.clickThroughMode,
            })
            saveSettings()
            if (mainWindow && !mainWindow.isDestroyed()) {
                applyWindowSettings(mainWindow)
            }
            broadcastSettings()
        }],
    ]

    registrations.forEach(([accelerator, handler]) => {
        if (!accelerator) {
            return
        }

        try {
            globalShortcut.register(accelerator, handler)
        } catch (error) {
            console.warn(`[shortcut] register failed for ${accelerator}:`, error)
        }
    })
}

function applyWindowsAcrylic(win, enabled) {
    if (process.platform !== 'win32') return

    try {
        if (typeof win.setBackgroundMaterial === 'function') {
            win.setBackgroundMaterial(enabled ? 'acrylic' : 'none')
        }
    } catch (error) {
        console.warn('[blur] setBackgroundMaterial failed:', error)
    }
}

function createWindow() {
    const win = new BrowserWindow({
        width: 1420,
        height: 860,
        minWidth: 520,
        minHeight: 380,
        frame: false,
        transparent: false,
        hasShadow: false,
        backgroundColor: '#f5f5f7',
        titleBarStyle: 'hidden',
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            contextIsolation: true,
            nodeIntegration: false,
            webviewTag: true,
            sandbox: true,
            enableRemoteModule: false,
            devTools: true,
        },
    })

    mainWindow = win

    nativeTheme.themeSource = 'system'
    applyWindowSettings(win)

    win.loadURL(getWindowUrl('/'))

    win.on('blur', () => {
        if (currentSettings.autoHide && !currentSettings.alwaysOnTop) {
            win.hide()
            refreshTrayMenu()
        }
    })

    win.on('close', (event) => {
        if (currentSettings.closeToTray && !isQuitting) {
            event.preventDefault()
            win.hide()
            refreshTrayMenu()
        }
    })

    win.webContents.on('did-finish-load', () => {
        broadcastSettings()
    })

    win.on('closed', () => {
        if (mainWindow === win) {
            mainWindow = null
        }
    })
}

ipcMain.handle('window:minimize', (event) => {
    const win = BrowserWindow.fromWebContents(event.sender)
    if (win && !win.isDestroyed()) {
        win.minimize()
    }
})

ipcMain.handle('window:maximize', (event) => {
    const win = BrowserWindow.fromWebContents(event.sender)
    if (win && !win.isDestroyed()) {
        if (win.isMaximized()) {
            win.unmaximize()
        } else {
            win.maximize()
        }
    }
})

ipcMain.handle('window:close', (event) => {
    const win = BrowserWindow.fromWebContents(event.sender)
    if (win && !win.isDestroyed()) {
        win.close()
    }
})

ipcMain.handle('window:open-settings', () => {
    openSettingsPanel()
})

ipcMain.handle('settings:get', () => currentSettings)

ipcMain.handle('settings:update', (_, partial) => {
    currentSettings = normalizeSettings({
        ...currentSettings,
        ...partial,
    })

    saveSettings()
    registerGlobalShortcuts()

    if (mainWindow && !mainWindow.isDestroyed()) {
        applyWindowSettings(mainWindow)
    }

    broadcastSettings()
    refreshTrayMenu()
    return currentSettings
})

app.whenReady().then(() => {
    loadSettings()
    createWindow()
    createTray()
    registerGlobalShortcuts()

    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) {
            createWindow()
        }
    })
})

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit()
    }
})

app.on('will-quit', () => {
    isQuitting = true
    globalShortcut.unregisterAll()
})
