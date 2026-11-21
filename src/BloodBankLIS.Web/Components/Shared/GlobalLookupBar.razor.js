// "/" or Ctrl+K focuses the top-bar lookup, unless the user is already typing in a field.
let handler = null;

export function registerHotkey(input) {
    unregisterHotkey();
    handler = (e) => {
        if (!input || !input.isConnected) {
            return;
        }

        const target = e.target;
        const typing = target && (target.isContentEditable
            || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
        const ctrlK = (e.ctrlKey || e.metaKey) && (e.key === "k" || e.key === "K");
        if (ctrlK || (e.key === "/" && !typing)) {
            e.preventDefault();
            input.focus();
            input.select();
        }
    };
    document.addEventListener("keydown", handler);
}

export function unregisterHotkey() {
    if (handler) {
        document.removeEventListener("keydown", handler);
        handler = null;
    }
}

export function blur(input) {
    input?.blur();
}
