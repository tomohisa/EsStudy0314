/**
 * クリップボード操作のためのユーティリティ関数
 */
window.clipboardUtils = {
    /**
     * テキストをクリップボードにコピーする
     * @param {string} text コピーするテキスト
     * @returns {Promise<boolean>} コピーが成功したかどうか
     */
    copyToClipboard: function (text) {
        return new Promise((resolve, reject) => {
            // 方法1: モダンなClipboard API (Chrome, Edge, Firefoxなど)
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text)
                    .then(() => resolve(true))
                    .catch((error) => {
                        console.warn("Clipboard API failed:", error);
                        // フォールバックを試みる
                        this.fallbackCopyToClipboard(text, resolve, reject);
                    });
            } else {
                // フォールバック方法
                this.fallbackCopyToClipboard(text, resolve, reject);
            }
        });
    },

    /**
     * クリップボードコピーのフォールバック実装
     * (Safari対応のため特に重要)
     */
    fallbackCopyToClipboard: function (text, resolve, reject) {
        try {
            // 一時的なテキストエリア要素を作成
            const textArea = document.createElement('textarea');
            textArea.value = text;
            
            // 視覚的に見えないようにするが、フォーカスは可能にする
            textArea.style.position = 'fixed';
            textArea.style.top = '0';
            textArea.style.left = '0';
            textArea.style.width = '2em';
            textArea.style.height = '2em';
            textArea.style.padding = '0';
            textArea.style.border = 'none';
            textArea.style.outline = 'none';
            textArea.style.boxShadow = 'none';
            textArea.style.background = 'transparent';
            
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            
            // execCommandを使ってコピー (Safariで動作可能)
            const success = document.execCommand('copy');
            document.body.removeChild(textArea);
            
            if (success) {
                resolve(true);
            } else {
                reject(new Error('execCommand copy failed'));
            }
        } catch (err) {
            reject(err);
        }
    }
};