export function getSelection(el) {
    if (!el) {
        return { start: 0, end: 0 };
    }

    return { start: el.selectionStart ?? 0, end: el.selectionEnd ?? 0 };
}

export function setSelection(el, pos) {
    if (!el) {
        return;
    }

    el.focus();
    el.setSelectionRange(pos, pos);
}
