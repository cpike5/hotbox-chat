// Search keyboard shortcut interop for Blazor WASM
// Listens for Ctrl+K / Cmd+K globally and invokes a .NET method
window.searchInterop = {
    _dotNetRef: null,

    register: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        document.addEventListener('keydown', this._handler);
    },

    unregister: function () {
        document.removeEventListener('keydown', this._handler);
        this._dotNetRef = null;
    },

    focusElement: function (element) {
        if (element) {
            element.focus();
        }
    },

    _handler: function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            if (window.searchInterop._dotNetRef) {
                window.searchInterop._dotNetRef.invokeMethodAsync('OnSearchShortcut');
            }
        }
    }
};
