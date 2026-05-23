(function () {
    const deferredQueue = [];
    let interactionHooked = false;

    async function playWithAudioElement(base64Content, mimeType) {
        const src = `data:${mimeType};base64,${base64Content}`;
        const audio = new Audio(src);
        audio.preload = "auto";
        await audio.play();
    }

    async function playDefaultTone() {
        const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextCtor) {
            throw new Error("AudioContext nicht verfügbar.");
        }

        const context = new AudioContextCtor();
        if (context.state === "suspended") {
            await context.resume();
        }

        const oscillator = context.createOscillator();
        const gainNode = context.createGain();
        oscillator.type = "sine";
        oscillator.frequency.value = 880;
        gainNode.gain.value = 0.12;

        oscillator.connect(gainNode);
        gainNode.connect(context.destination);
        oscillator.start();
        oscillator.stop(context.currentTime + 0.2);
    }

    async function playInternal(base64Content, mimeType) {
        if (base64Content && mimeType) {
            await playWithAudioElement(base64Content, mimeType);
            return "played";
        }

        await playDefaultTone();
        return "played";
    }

    async function tryReplayDeferred() {
        if (deferredQueue.length === 0) {
            return;
        }

        const items = deferredQueue.splice(0, deferredQueue.length);
        for (const item of items) {
            try {
                await playInternal(item.base64Content, item.mimeType);
            } catch (error) {
                if (error && error.name === "NotAllowedError") {
                    deferredQueue.push(item);
                }
            }
        }
    }

    function ensureInteractionHook() {
        if (interactionHooked) {
            return;
        }

        const events = ["click", "keydown", "pointerdown", "touchstart"];
        for (const eventName of events) {
            document.addEventListener(eventName, () => {
                void tryReplayDeferred();
            }, true);
        }

        interactionHooked = true;
    }

    window.softwareschmiedeNotifications = {
        playAlert: async function (base64Content, mimeType) {
            try {
                return await playInternal(base64Content, mimeType);
            } catch (error) {
                if (error && error.name === "NotAllowedError") {
                    deferredQueue.push({ base64Content, mimeType });
                    ensureInteractionHook();
                    return "deferred";
                }

                return "failed";
            }
        }
    };
})();
