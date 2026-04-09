
// 示例：为 weread.qq.com 提供多段 CSS 与多段 JS 注入示例
// 导出为默认函数 `main(helper)`，helper 提供：
// - helper.runWebviewCss(idSuffix, css)
// - helper.runWebviewJS(js)
// 你可以多次调用这些方法来注入多段样式或脚本，或将多个脚本合并为一个 IIFE
/**
 * 示例规则：weread.qq.com
 *
 * 目的：展示如何编写完整、可维护且可调试的站点注入脚本（CSS / JS），
 * 并示范常用场景：样式替换、移除广告或遮罩、等待内容加载后再修改 DOM、以及调试技巧。
 *
 * 位置：放在 `src/rule/` 下，构建时由 `src/lib/siteRules.js` 使用 `import.meta.glob` 自动加载。
 * 导出约定（推荐）：
 *  - `export const pattern = [...]` 或 `export const pattern = 'hostname'` 用于匹配站点；
 *  - `export default function main(helper) { ... }`：规则的单一入口，框架在匹配时会调用它并传入 `helper` 对象。
 *
 * 注意：注入脚本会在 WebView 的 `dom-ready` 事件触发时由渲染器调用。
 *       如需更晚的时机（页面脚本跑完或动态内容加载完成），请在注入的脚本内部使用 MutationObserver 或轮询。
 */

// 匹配被处理的主机名或路径：支持字符串、数组或 RegExp（loader 已支持）
export const pattern = ['weread.qq.com', 'book.weread.qq.com'];

// 可选的说明字段（不被 loader 使用，仅作开发者自述）
export const description = '微信读书（Weread）示例规则：清理页面并优化阅读体验。';

// 规则执行的入口
export default function main(helper) {
    // helper 常用字段：runWebviewCss(idSuffix, css), runWebviewJS(js), webview, settings, activeTab, rule
    const runCss = helper && helper.runWebviewCss;
    const runJs = helper && helper.runWebviewJS;
    const webview = helper && helper.webview;
    const settings = helper && helper.settings;
    const activeTab = helper && helper.activeTab;

    try {
        // // 便于在渲染器 Console 中追踪：WebView 的 console-message 被转发，输出会带上 [webview.console]
        // try { console.log('[weread.rule] apply start', activeTab && activeTab.url); } catch (e) { }

        // // -------------------------
        // // 1) 样式注入示例（分段）
        // // 分段注入的好处是可以针对不同功能分别启用/替换/移除，便于调试和维护。
        // // idSuffix 应短且不包含空格，例如 'base','layout','hide-ads' 等。
        // // -------------------------
        // try {
        //     // 基础重置：去除默认 margin、设置背景透明以便应用透明窗体效果
        //     runCss && runCss('base', `
        //             html, body { margin: 0 !important; padding: 0 !important; background: transparent !important; }
        //             * { box-sizing: border-box !important; }
        //         `);

        //     // 布局：限制阅读宽度、居中、增加左右内边距
        //     runCss && runCss('layout', `
        //             .reader-content, .chapter-content { max-width: 760px !important; margin: 0 auto !important; padding: 20px !important; }
        //             .chapter-title { font-size: 20px !important; font-weight: 700 !important; margin-bottom: 12px !important; }
        //         `);

        //     // 排版优化：行高、字号、图片自适应
        //     runCss && runCss('typography', `
        //             .reader-content p { line-height: 1.85 !important; font-size: 18px !important; color: #222 !important; }
        //             .reader-content img { max-width: 100% !important; height: auto !important; }
        //         `);

        //     // 隐藏页眉/页脚/侧边栏等非阅读区域
        //     runCss && runCss('hide-elements', `
        //             .site-header, .site-footer, .sidebar, .recommend-panel, .related { display: none !important; }
        //         `);
        // } catch (e) {
        //     try { console.error('[weread.rule] runCss error', e); } catch (err) { }
        // }

        // // -------------------------
        // // 2) JS 注入示例
        // // - 使用 IIFE（自执行函数）封装，避免污染页面全局作用域
        // // - 在需要等待 DOM 的场景中，使用 MutationObserver 或轮询
        // // -------------------------
        // try {
        //     // A) 尝试立即移除已存在的广告/遮罩元素
        //     runJs && runJs(`(() => {
        //             try {
        //                 console.log('[weread.rule] remove known overlays/ads');
        //                 document.querySelectorAll('[id*=ad],[class*=ad],[class*=banner],[class*=mask],[class*=overlay]').forEach(n => { try { n.remove(); } catch(e){} });
        //             } catch(e) {}
        //             return true;
        //         })();`);

        //     // B) 如果目标内容是异步加载的，使用 MutationObserver 等待并应用修复
        //     runJs && runJs(`(() => {
        //             try {
        //                 function applyWhenReady() {
        //                     const el = document.querySelector('.chapter-content') || document.querySelector('.reader-content');
        //                     if (!el) return false;
        //                     try {
        //                         // 示例修改：确保段落有合适间距
        //                         el.querySelectorAll && el.querySelectorAll('p').forEach(p => { p.style.marginBottom = '12px'; });
        //                     } catch(e) {}
        //                     return true;
        //                 }

        //                 if (!applyWhenReady()) {
        //                     const mo = new MutationObserver((mutations, obs) => {
        //                         if (applyWhenReady()) {
        //                             try { obs.disconnect(); } catch(e) {}
        //                         }
        //                     });
        //                     mo.observe(document.documentElement || document, { childList: true, subtree: true });
        //                 }
        //             } catch(e) {}
        //             return true;
        //         })();`);

        //     // C) 功能增强示例：添加键盘翻页（左右方向键）
        //     runJs && runJs(`(() => {
        //             try {
        //                 if (window.__glassReaderKeybindsInstalled) return true;
        //                 window.__glassReaderKeybindsInstalled = true;
        //                 window.addEventListener('keydown', function(e) {
        //                     try {
        //                         if (e.key === 'ArrowLeft') {
        //                             // 模拟上一页动作：此处仅示例，可替换为实际翻页逻辑
        //                             const prev = document.querySelector('.pager-prev, .prev-chapter');
        //                             if (prev && prev.click) prev.click();
        //                         } else if (e.key === 'ArrowRight') {
        //                             const next = document.querySelector('.pager-next, .next-chapter');
        //                             if (next && next.click) next.click();
        //                         }
        //                     } catch(e) {}
        //                 }, false);
        //             } catch(e) {}
        //             return true;
        //         })();`);
        // } catch (e) {
        //     try { console.error('[weread.rule] runJs error', e); } catch (err) { }
        // }

        // // -------------------------
        // // 3) 直接通过 webview 调用（可选）：如果需要访问 webview 自身的 API
        // //    注意：直接调用 webview.executeJavaScript 返回 Promise，可用于获得执行结果。
        // // -------------------------
        // try {
        //     if (webview && typeof webview.executeJavaScript === 'function') {
        //         try {
        //             webview.executeJavaScript(`console.log('[weread.rule] direct webview.exec');`).catch(() => { });
        //         } catch (e) { }
        //     }
        // } catch (e) { }

        // // -------------------------
        // // 4) 调试辅助（建议：仅在开发时启用）
        // // - 不建议在生产规则中使用 alert()。可改为 console.log 并在渲染器查看 `[webview.console]` 前缀日志。
        // // - 如需快速校验注入是否到达页面：在上面的 runJs 中添加 console.log('[weread.rule] ...')，渲染器会显示。
        // // -------------------------
        // // 示例（已注释）：
        // // runJs && runJs(`(() => { try{ alert('weread rule injected'); }catch(e){}; return true; })();`);

        // try { console.log('[weread.rule] apply finished'); } catch (e) { }

        // 可选：返回 Promise（框架通常不会等待此 Promise 完成，但你可以在内部做异步逻辑）
        // return Promise.resolve();

    } catch (err) {
        try { console.error('[weread.rule] unexpected error', err); } catch (e) { }
    }

    // end
}