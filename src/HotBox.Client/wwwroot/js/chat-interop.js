window.chatInterop = {
    scrollToBottom: function (selector) {
        const el = document.querySelector(selector);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    },

    scrollToBottomIfNearEnd: function (selector, threshold) {
        const el = document.querySelector(selector);
        if (!el) return;

        const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
        if (distanceFromBottom <= (threshold || 150)) {
            el.scrollTop = el.scrollHeight;
        }
    }
};
