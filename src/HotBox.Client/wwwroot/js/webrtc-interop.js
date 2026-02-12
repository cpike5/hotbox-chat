/**
 * WebRtcInterop — thin JavaScript wrapper around browser RTCPeerConnection APIs.
 * Called from .NET via IJSRuntime.InvokeAsync<T>("WebRtcInterop.functionName", args).
 *
 * Responsibilities:
 *   - Create and manage RTCPeerConnection instances (one per remote peer)
 *   - Forward ICE candidates and connection-state changes back to .NET
 *   - Handle SDP offer/answer creation and remote description setting
 *   - Manage remote audio playback elements
 *   - Clean up connections and audio elements on close/leave
 *
 * All business logic lives in .NET. This file is intentionally thin.
 */
window.WebRtcInterop = {
    /** @type {Map<string, RTCPeerConnection>} */
    _peerConnections: new Map(),

    /** @type {Map<string, HTMLAudioElement>} */
    _remoteAudioElements: new Map(),

    /** @type {Map<string, object>} .NET object references keyed by peerId */
    _dotNetRefs: new Map(),

    /**
     * Initialize the WebRTC service by acquiring user media (audio).
     * Called from .NET during WebRtcService.InitializeAsync().
     * @param {object} dotNetRef — DotNetObjectReference (reserved for future callbacks during init)
     * @returns {Promise<void>}
     */
    initialize: async function (dotNetRef) {
        await window.AudioInterop.getUserMedia(null);
    },

    /**
     * Enable or disable the local microphone audio track.
     * Delegates to AudioInterop.setAudioEnabled.
     * @param {boolean} enabled
     */
    setMicrophoneEnabled: function (enabled) {
        window.AudioInterop.setAudioEnabled(enabled);
    },

    /**
     * Close a single peer connection by peerId.
     * Alias for closePeerConnection — matches the name used by .NET interop calls.
     * @param {string} peerId
     */
    closeConnection: function (peerId) {
        this.closePeerConnection(peerId);
    },

    /**
     * Create an RTCPeerConnection for a remote peer, wire up event handlers
     * that call back into .NET, and add local audio tracks if available.
     *
     * @param {object} dotNetRef — DotNetObjectReference for callbacks
     * @param {string} peerId   — unique identifier for the remote peer
     * @param {Array<{urls: string, username?: string, credential?: string}>} iceServers
     */
    createPeerConnection: function (dotNetRef, peerId, iceServers) {
        // Close any existing connection for this peer before creating a new one.
        if (this._peerConnections.has(peerId)) {
            this.closePeerConnection(peerId);
        }

        const config = { iceServers: iceServers || [] };
        const pc = new RTCPeerConnection(config);

        // Store references.
        this._peerConnections.set(peerId, pc);
        this._dotNetRefs.set(peerId, dotNetRef);

        // Add local audio tracks if a local stream is already available.
        const localStream = window.AudioInterop.getLocalStream();
        if (localStream) {
            localStream.getTracks().forEach(track => pc.addTrack(track, localStream));
        }

        // ----- Event handlers (thin: forward to .NET) -----

        pc.onicecandidate = (event) => {
            if (event.candidate) {
                dotNetRef.invokeMethodAsync('OnIceCandidate', peerId, JSON.stringify(event.candidate));
            }
        };

        pc.ontrack = (event) => {
            let audio = this._remoteAudioElements.get(peerId);
            if (!audio) {
                audio = new Audio();
                audio.autoplay = true;
                this._remoteAudioElements.set(peerId, audio);
            }
            audio.srcObject = event.streams[0];
            dotNetRef.invokeMethodAsync('OnRemoteTrack', peerId);
        };

        pc.onconnectionstatechange = () => {
            dotNetRef.invokeMethodAsync('OnConnectionStateChanged', peerId, pc.connectionState);
        };
    },

    /**
     * Create an SDP offer, set it as the local description, and return the SDP string.
     * @param {string} peerId
     * @returns {Promise<string>} SDP offer string
     */
    createOffer: async function (peerId) {
        const pc = this._peerConnections.get(peerId);
        if (!pc) {
            throw new Error(`No peer connection found for peerId: ${peerId}`);
        }

        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        return pc.localDescription.sdp;
    },

    /**
     * Create an SDP answer, set it as the local description, and return the SDP string.
     * @param {string} peerId
     * @returns {Promise<string>} SDP answer string
     */
    createAnswer: async function (peerId) {
        const pc = this._peerConnections.get(peerId);
        if (!pc) {
            throw new Error(`No peer connection found for peerId: ${peerId}`);
        }

        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        return pc.localDescription.sdp;
    },

    /**
     * Set the remote SDP description on a peer connection.
     * @param {string} peerId
     * @param {string} type — "offer" or "answer"
     * @param {string} sdp  — SDP string
     * @returns {Promise<void>}
     */
    setRemoteDescription: async function (peerId, type, sdp) {
        const pc = this._peerConnections.get(peerId);
        if (!pc) {
            throw new Error(`No peer connection found for peerId: ${peerId}`);
        }

        const description = new RTCSessionDescription({ type: type, sdp: sdp });
        await pc.setRemoteDescription(description);
    },

    /**
     * Add an ICE candidate received from a remote peer.
     * @param {string} peerId
     * @param {string} candidateJson — JSON-serialized RTCIceCandidateInit
     * @returns {Promise<void>}
     */
    addIceCandidate: async function (peerId, candidateJson) {
        const pc = this._peerConnections.get(peerId);
        if (!pc) {
            throw new Error(`No peer connection found for peerId: ${peerId}`);
        }

        const candidate = new RTCIceCandidate(JSON.parse(candidateJson));
        await pc.addIceCandidate(candidate);
    },

    /**
     * Add local audio tracks from the given stream to an existing peer connection.
     * Use this when the local stream becomes available after the connection was created.
     * @param {string} peerId
     * @param {MediaStream} stream
     */
    addLocalStream: function (peerId, stream) {
        const pc = this._peerConnections.get(peerId);
        if (!pc) {
            throw new Error(`No peer connection found for peerId: ${peerId}`);
        }

        stream.getTracks().forEach(track => pc.addTrack(track, stream));
    },

    /**
     * Close a single peer connection and clean up its audio element.
     * @param {string} peerId
     */
    closePeerConnection: function (peerId) {
        const pc = this._peerConnections.get(peerId);
        if (pc) {
            pc.onicecandidate = null;
            pc.ontrack = null;
            pc.onconnectionstatechange = null;
            pc.close();
            this._peerConnections.delete(peerId);
        }

        const audio = this._remoteAudioElements.get(peerId);
        if (audio) {
            audio.srcObject = null;
            this._remoteAudioElements.delete(peerId);
        }

        this._dotNetRefs.delete(peerId);
    },

    /**
     * Close all peer connections and clean up all audio elements.
     * Called when the user leaves a voice channel.
     */
    closeAllConnections: function () {
        for (const peerId of this._peerConnections.keys()) {
            this.closePeerConnection(peerId);
        }
    },

    /**
     * Mute or unmute all remote audio playback elements.
     * Used when the user toggles deafen -- deafened users should not hear remote audio.
     * @param {boolean} muted -- true to mute remote audio, false to unmute
     */
    setRemoteAudioMuted: function (muted) {
        for (const audio of this._remoteAudioElements.values()) {
            audio.muted = muted;
        }
    }
};
