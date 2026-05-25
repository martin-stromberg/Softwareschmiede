(function () {
    function resolveElement(selector) {
        if (!selector || typeof selector !== "string") {
            return null;
        }

        return document.querySelector(selector);
    }

    window.softwareschmiedeLogScroll = {
        getMetrics: function (selector) {
            const element = resolveElement(selector);
            if (!element) {
                return [0, 0, 0, 0];
            }

            return [
                element.scrollTop ?? 0,
                element.scrollHeight ?? 0,
                element.clientHeight ?? 0,
                1
            ];
        },
        scrollToEnd: function (selector) {
            const element = resolveElement(selector);
            if (!element) {
                return false;
            }

            element.scrollTop = element.scrollHeight;
            return true;
        }
    };
})();
