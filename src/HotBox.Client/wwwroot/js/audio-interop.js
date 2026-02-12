/**
 * AudioInterop — thin JavaScript wrapper around browser audio/media APIs.
 * Called from .NET via IJSRuntime.InvokeAsync<T>("AudioInterop.functionName", args).
 *
 * Responsibilities:
 *   - Enumerate audio input/output devices
 *   - Acquire a local audio MediaStream (getUserMedia)
 *   - Mute / unmute the local stream
 *   - Stop and release the local stream
 *   - Provide the local stream to WebRtcInterop for peer connections
 */
window.AudioInterop = {
    /** @type {MediaStream|null} */
    _localStream: null,

    /**
     * Enumerate available audio devices (inputs and outputs).
     * @returns {Promise<Array<{deviceId: string, label: string, kind: string}>>}
     */
    enumerateDevices: async function () {
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices
            .filter(d => d.kind === 'audioinput' || d.kind === 'audiooutput')
            .map(d => ({
                deviceId: d.deviceId,
                label: d.label,
                kind: d.kind
            }));
    },

    /**
     * Acquire an audio-only MediaStream, optionally from a specific device.
     * Stores the stream internally so WebRtcInterop can retrieve it.
     * @param {string|null} deviceId — specific audio input device, or null for default
     * @returns {Promise<boolean>} true if stream was acquired successfully
     */
    getUserMedia: async function (deviceId) {
        // Stop any existing stream before acquiring a new one.
        this.stopLocalStream();

        const constraints = {
            audio: deviceId ? { deviceId: { exact: deviceId } } : true,
            video: false
        };

        this._localStream = await navigator.mediaDevices.getUserMedia(constraints);
        return true;
    },

    /**
     * Enable or disable all audio tracks on the local stream (mute/unmute).
     * @param {boolean} enabled
     */
    setAudioEnabled: function (enabled) {
        if (!this._localStream) {
            return;
        }
        this._localStream.getAudioTracks().forEach(track => {
            track.enabled = enabled;
        });
    },

    /**
     * Stop all tracks on the local stream and release the reference.
     */
    stopLocalStream: function () {
        if (this._localStream) {
            this._localStream.getTracks().forEach(track => track.stop());
            this._localStream = null;
        }
    },

    /**
     * Return the stored local MediaStream (used by WebRtcInterop to add tracks
     * to peer connections). May be null if getUserMedia has not been called.
     * @returns {MediaStream|null}
     */
    getLocalStream: function () {
        return this._localStream;
    },

    /**
     * Mute or unmute all remote audio playback elements.
     * Used when the user deafens/undeafens to silence incoming audio.
     * @param {boolean} muted — true to mute remote audio, false to unmute
     */
    setRemoteAudioMuted: function (muted) {
        const audioElements = window.WebRtcInterop._remoteAudioElements;
        if (audioElements) {
            audioElements.forEach(function (audio) {
                audio.muted = muted;
            });
        }
    }
};
