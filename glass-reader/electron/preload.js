import { contextBridge, ipcRenderer } from 'electron'

contextBridge.exposeInMainWorld('desktop', {
    platform: process.platform,
    minimizeWindow: () => ipcRenderer.invoke('window:minimize'),
    closeWindow: () => ipcRenderer.invoke('window:close'),
    openSettingsWindow: () => ipcRenderer.invoke('window:open-settings'),
    getSettings: () => ipcRenderer.invoke('settings:get'),
    updateSettings: (settings) => ipcRenderer.invoke('settings:update', settings),
    onSettingsChanged: (callback) => {
        const handler = (_, settings) => callback(settings)
        ipcRenderer.on('settings:changed', handler)

        return () => {
            ipcRenderer.removeListener('settings:changed', handler)
        }
    },
    onOpenSettingsRequest: (callback) => {
        const handler = () => callback()
        ipcRenderer.on('ui:open-settings', handler)

        return () => {
            ipcRenderer.removeListener('ui:open-settings', handler)
        }
    },
})
