using HotBox.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace HotBox.Client.Services;

public class WebRtcService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<WebRtcService> _logger;
    private DotNetObjectReference<WebRtcService>? _dotNetRef;

    public WebRtcService(IJSRuntime jsRuntime, ILogger<WebRtcService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    // ----- Events for UI/state to subscribe to -----

    /// <summary>Raised when a local ICE candidate is gathered and needs to be sent to a peer via signaling.</summary>
    public event Action<string, string>? OnIceCandidateReady;

    /// <summary>Raised when the connection state of a peer connection changes.</summary>
    public event Action<string, string>? OnPeerConnectionStateChanged;

    /// <summary>Raised when a remote audio track is received from a peer.</summary>
    public event Action<string>? OnRemoteTrackReceived;

    // ----- Initialization -----

    /// <summary>
    /// Gets user media (audio) via the AudioInterop JS module and stores the local stream.
    /// Creates the DotNetObjectReference used for JS-to-.NET callbacks.
    /// </summary>
    public async Task InitializeAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);

        try
        {
            await _jsRuntime.InvokeVoidAsync("AudioInterop.getUserMedia", (string?)null);
            _logger.LogInformation("WebRTC service initialized with local audio stream");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebRTC service");
            throw;
        }
    }

    // ----- Peer connection management -----

    /// <summary>
    /// Creates an RTCPeerConnection for the specified peer via JS interop.
    /// Passes the DotNetObjectReference so JavaScript can call back into .NET for ICE candidates and state changes.
    /// </summary>
    public async Task CreatePeerConnectionAsync(string peerId, IceServerInfo[] iceServers)
    {
        EnsureInitialized();

        try
        {
            await _jsRuntime.InvokeVoidAsync("WebRtcInterop.createPeerConnection", _dotNetRef, peerId, iceServers);
            _logger.LogDebug("Created peer connection for {PeerId}", peerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create peer connection for {PeerId}", peerId);
            throw;
        }
    }

    /// <summary>
    /// Creates an SDP offer for the specified peer and returns the offer string.
    /// </summary>
    public async Task<string> CreateOfferAsync(string peerId)
    {
        EnsureInitialized();

        try
        {
            var offer = await _jsRuntime.InvokeAsync<string>("WebRtcInterop.createOffer", peerId);
            _logger.LogDebug("Created SDP offer for {PeerId}", peerId);
            return offer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SDP offer for {PeerId}", peerId);
            throw;
        }
    }

    /// <summary>
    /// Creates an SDP answer for the specified peer and returns the answer string.
    /// </summary>
    public async Task<string> CreateAnswerAsync(string peerId)
    {
        EnsureInitialized();

        try
        {
            var answer = await _jsRuntime.InvokeAsync<string>("WebRtcInterop.createAnswer", peerId);
            _logger.LogDebug("Created SDP answer for {PeerId}", peerId);
            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SDP answer for {PeerId}", peerId);
            throw;
        }
    }

    /// <summary>
    /// Sets the remote SDP description on the peer connection.
    /// </summary>
    public async Task SetRemoteDescriptionAsync(string peerId, string type, string sdp)
    {
        EnsureInitialized();

        try
        {
            await _jsRuntime.InvokeVoidAsync("WebRtcInterop.setRemoteDescription", peerId, type, sdp);
            _logger.LogDebug("Set remote description ({Type}) for {PeerId}", type, peerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set remote description for {PeerId}", peerId);
            throw;
        }
    }

    /// <summary>
    /// Adds an ICE candidate received from a remote peer.
    /// </summary>
    public async Task AddIceCandidateAsync(string peerId, string candidateJson)
    {
        EnsureInitialized();

        try
        {
            await _jsRuntime.InvokeVoidAsync("WebRtcInterop.addIceCandidate", peerId, candidateJson);
            _logger.LogDebug("Added ICE candidate for {PeerId}", peerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add ICE candidate for {PeerId}", peerId);
            throw;
        }
    }

    /// <summary>
    /// Closes a single peer connection and cleans up associated resources.
    /// </summary>
    public async Task CloseConnectionAsync(string peerId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("WebRtcInterop.closePeerConnection", peerId);
            _logger.LogDebug("Closed peer connection for {PeerId}", peerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close peer connection for {PeerId}", peerId);
        }
    }

    /// <summary>
    /// Closes all peer connections and stops the local media stream.
    /// </summary>
    public async Task CloseAllConnectionsAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("WebRtcInterop.closeAllConnections");
            await _jsRuntime.InvokeVoidAsync("AudioInterop.stopLocalStream");
            _logger.LogInformation("Closed all peer connections and stopped local stream");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close all peer connections");
        }
    }

    /// <summary>
    /// Enables or disables the local microphone audio track.
    /// </summary>
    public async Task SetMicrophoneEnabledAsync(bool enabled)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("AudioInterop.setAudioEnabled", enabled);
            _logger.LogDebug("Microphone {State}", enabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set microphone enabled state to {Enabled}", enabled);
        }
    }

    /// <summary>
    /// Mutes or unmutes all remote audio playback elements.
    /// When deafened, remote audio should not be heard.
    /// </summary>
    public async Task SetRemoteAudioMutedAsync(bool muted)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("WebRtcInterop.setRemoteAudioMuted", muted);
            _logger.LogDebug("Remote audio {State}", muted ? "muted" : "unmuted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set remote audio muted state to {Muted}", muted);
        }
    }

    // ----- JS-invokable callbacks -----

    /// <summary>
    /// Called from JavaScript when a local ICE candidate is gathered.
    /// Forwards the candidate to the signaling layer via the OnIceCandidateReady event.
    /// </summary>
    [JSInvokable]
    public void OnIceCandidate(string peerId, string candidateJson)
    {
        _logger.LogDebug("ICE candidate gathered for {PeerId}", peerId);
        OnIceCandidateReady?.Invoke(peerId, candidateJson);
    }

    /// <summary>
    /// Called from JavaScript when the RTCPeerConnection state changes.
    /// Updates voice state via the OnPeerConnectionStateChanged event.
    /// </summary>
    [JSInvokable]
    public void OnConnectionStateChanged(string peerId, string state)
    {
        _logger.LogDebug("Peer connection state changed for {PeerId}: {State}", peerId, state);
        OnPeerConnectionStateChanged?.Invoke(peerId, state);
    }

    /// <summary>
    /// Called from JavaScript when a remote audio track is received.
    /// Informational — indicates the remote stream is playing.
    /// </summary>
    [JSInvokable]
    public void OnRemoteTrack(string peerId)
    {
        _logger.LogDebug("Remote track received from {PeerId}", peerId);
        OnRemoteTrackReceived?.Invoke(peerId);
    }

    // ----- IAsyncDisposable -----

    public async ValueTask DisposeAsync()
    {
        try
        {
            await CloseAllConnectionsAsync();
        }
        catch (JSDisconnectedException)
        {
            // Expected during app shutdown when the JS runtime is already disconnected.
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    // ----- Private helpers -----

    private void EnsureInitialized()
    {
        if (_dotNetRef is null)
        {
            throw new InvalidOperationException(
                "WebRTC service is not initialized. Call InitializeAsync before using peer connection methods.");
        }
    }
}
