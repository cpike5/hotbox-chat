// Browser Notification API interop for Blazor WASM

window.hotboxNotifications = {
    /**
     * Requests notification permission from the user.
     * @returns {Promise<string>} The permission state: "granted", "denied", or "default".
     */
    requestNotificationPermission: async function () {
        if (!("Notification" in window)) {
            return "denied";
        }
        const result = await Notification.requestPermission();
        return result;
    },

    /**
     * Shows a browser desktop notification.
     * @param {string} title - The notification title.
     * @param {string} body - The notification body text.
     */
    showNotification: function (title, body) {
        if (!("Notification" in window)) {
            return;
        }
        if (Notification.permission === "granted") {
            new Notification(title, { body: body });
        }
    },

    /**
     * Checks whether the document (browser tab) is currently visible.
     * @returns {boolean} True if the document is visible, false otherwise.
     */
    isDocumentVisible: function () {
        return document.visibilityState === "visible";
    }
};
