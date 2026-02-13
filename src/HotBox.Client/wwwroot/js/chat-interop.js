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
    },

    getViewportSize: function () {
        return { width: window.innerWidth, height: window.innerHeight };
    },

    getScrollPosition: function (selector) {
        const el = document.querySelector(selector);
        if (!el) {
            return { scrollTop: 0, scrollHeight: 0, clientHeight: 0 };
        }
        return {
            scrollTop: el.scrollTop,
            scrollHeight: el.scrollHeight,
            clientHeight: el.clientHeight
        };
    },

    setScrollTop: function (selector, value) {
        const el = document.querySelector(selector);
        if (el) {
            el.scrollTop = value;
        }
    },

    isNearTop: function (selector, threshold) {
        const el = document.querySelector(selector);
        if (!el) return false;

        const thresholdValue = threshold !== undefined ? threshold : 100;
        return el.scrollTop <= thresholdValue;
    }
};
