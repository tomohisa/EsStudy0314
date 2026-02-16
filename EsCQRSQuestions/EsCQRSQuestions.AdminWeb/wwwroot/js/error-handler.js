// Global error handler for AdminWeb
(function() {
    'use strict';

    // Global error handler for unhandled JavaScript errors
    window.addEventListener('error', function(event) {
        console.error('[AdminWeb Global Error]', {
            message: event.message,
            filename: event.filename,
            lineno: event.lineno,
            colno: event.colno,
            error: event.error,
            timestamp: new Date().toISOString(),
            url: window.location.href
        });
        
        // Show user-friendly error notification
        if (event.error) {
            showErrorNotification('予期しないエラーが発生しました: ' + event.message);
        }
    });

    // Global handler for unhandled promise rejections
    window.addEventListener('unhandledrejection', function(event) {
        console.error('[AdminWeb Unhandled Promise Rejection]', {
            reason: event.reason,
            promise: event.promise,
            timestamp: new Date().toISOString(),
            url: window.location.href
        });
        
        // Show user-friendly error notification
        const errorMessage = event.reason?.message || event.reason || 'Unknown promise rejection';
        showErrorNotification('処理中にエラーが発生しました: ' + errorMessage);
    });

    // Network error detection
    window.addEventListener('online', function() {
        console.log('[AdminWeb Network] Connection restored');
        showInfoNotification('ネットワーク接続が復旧しました');
    });

    window.addEventListener('offline', function() {
        console.warn('[AdminWeb Network] Connection lost');
        showErrorNotification('ネットワーク接続が失われました');
    });

    // Enhanced fetch error handling
    const originalFetch = window.fetch;
    window.fetch = function(...args) {
        const url = args[0];
        const options = args[1] || {};
        
        console.log('[AdminWeb Fetch]', {
            url: url,
            method: options.method || 'GET',
            timestamp: new Date().toISOString()
        });
        
        return originalFetch.apply(this, args)
            .then(response => {
                if (!response.ok) {
                    console.error('[AdminWeb Fetch Error]', {
                        url: url,
                        status: response.status,
                        statusText: response.statusText,
                        timestamp: new Date().toISOString()
                    });
                }
                return response;
            })
            .catch(error => {
                console.error('[AdminWeb Fetch Exception]', {
                    url: url,
                    error: error.message,
                    stack: error.stack,
                    timestamp: new Date().toISOString()
                });
                
                // Show user-friendly error for network issues
                if (error.name === 'TypeError' && error.message.includes('fetch')) {
                    showErrorNotification('サーバーとの通信に失敗しました。ネットワーク接続を確認してください。');
                }
                
                throw error;
            });
    };

    // Utility functions for notifications
    function showErrorNotification(message) {
        try {
            // Create a more sophisticated notification than alert
            if (window.bootstrap && document.getElementById('notification-container')) {
                createToastNotification(message, 'error');
            } else {
                // Fallback to alert
                alert('[エラー] ' + message);
            }
        } catch (e) {
            console.error('Failed to show error notification:', e);
        }
    }

    function showInfoNotification(message) {
        try {
            if (window.bootstrap && document.getElementById('notification-container')) {
                createToastNotification(message, 'info');
            } else {
                console.log('[Info] ' + message);
            }
        } catch (e) {
            console.error('Failed to show info notification:', e);
        }
    }

    function createToastNotification(message, type) {
        const container = document.getElementById('notification-container') || createNotificationContainer();
        
        const toastId = 'toast-' + Date.now();
        const toastColor = type === 'error' ? 'danger' : 'info';
        
        const toastHtml = `
            <div id="${toastId}" class="toast align-items-center text-bg-${toastColor} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        ${escapeHtml(message)}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;
        
        container.insertAdjacentHTML('beforeend', toastHtml);
        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement, {
            autohide: true,
            delay: type === 'error' ? 8000 : 4000
        });
        toast.show();
        
        // Clean up after toast is hidden
        toastElement.addEventListener('hidden.bs.toast', function() {
            toastElement.remove();
        });
    }

    function createNotificationContainer() {
        const container = document.createElement('div');
        container.id = 'notification-container';
        container.className = 'toast-container position-fixed top-0 end-0 p-3';
        container.style.zIndex = '9999';
        document.body.appendChild(container);
        return container;
    }

    function escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    // SignalR connection monitoring
    function monitorSignalRConnection() {
        const checkInterval = 5000; // 5 seconds
        let lastConnectionState = null;
        
        setInterval(function() {
            try {
                // Try to access SignalR connection status if available
                if (window.DotNet && window.blazorCulture) {
                    // Blazor is loaded, SignalR status might be available
                    const isOnline = navigator.onLine;
                    if (!isOnline && lastConnectionState !== 'offline') {
                        console.warn('[AdminWeb SignalR] Network offline detected');
                        lastConnectionState = 'offline';
                    } else if (isOnline && lastConnectionState === 'offline') {
                        console.log('[AdminWeb SignalR] Network back online');
                        lastConnectionState = 'online';
                    }
                }
            } catch (e) {
                // Ignore errors during monitoring
            }
        }, checkInterval);
    }

    // Start monitoring when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', monitorSignalRConnection);
    } else {
        monitorSignalRConnection();
    }

    console.log('[AdminWeb Error Handler] Initialized successfully');
})();
