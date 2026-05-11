(function () {
    'use strict';

    let clearTimer = null;

    async function safeWriteClipboard(text) {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                return true;
            }
        } catch (e) {
            console.warn('Clipboard write rejected:', e);
        }
        return false;
    }

    function clearDisplay() {
        const field = document.getElementById('password-field');
        if (field) {
            field.textContent = '********';
        }
    }

    async function displayPassword(password, timeoutSeconds) {
        if (clearTimer) {
            clearTimeout(clearTimer);
            clearTimer = null;
        }

        const field = document.getElementById('password-field');
        if (field) {
            field.textContent = password;
        }

        await safeWriteClipboard(password);

        clearTimer = setTimeout(async () => {
            clearDisplay();
            await safeWriteClipboard('');
            clearTimer = null;
        }, (timeoutSeconds || 30) * 1000);
    }

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden' && clearTimer) {
            clearDisplay();
            clearTimeout(clearTimer);
            clearTimer = null;
            safeWriteClipboard('');
        }
    });

    window.addEventListener('beforeunload', () => {
        if (clearTimer) {
            clearDisplay();
            clearTimeout(clearTimer);
            safeWriteClipboard('');
        }
    });

    window.kd802x = { displayPassword: displayPassword, clear: clearDisplay };
})();
