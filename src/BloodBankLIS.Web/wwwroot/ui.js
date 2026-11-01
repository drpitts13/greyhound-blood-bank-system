window.bbUi = {
    scrollIntoView: function (id) {
        var el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    },
    // Focus a field and select its text, so a barcode scan replaces a suggested value.
    focusSelect: function (id, preventScroll) {
        var el = document.getElementById(id);
        if (!el) return;
        el.focus({ preventScroll: !!preventScroll });
        if (el.select) el.select();
    }
};

// Bring newly shown editors and new error messages into view. Without this, a "New..."
// form opened below a long table, or a save error rendered at the top of the page,
// appears off-screen and the click looks like it did nothing.
(function () {
    function offScreen(el) {
        var r = el.getBoundingClientRect();
        return r.top < 0 || r.top > window.innerHeight - 40;
    }

    function check(node) {
        if (!(node instanceof Element)) return;
        var target = null;
        if (node.matches('[data-scroll-on-show], .alert-danger')) target = node;
        else target = node.querySelector('[data-scroll-on-show], .alert-danger');
        if (target && offScreen(target)) {
            target.scrollIntoView({ behavior: 'smooth', block: target.hasAttribute('data-scroll-on-show') ? 'start' : 'center' });
        }
    }

    function start() {
        new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var added = mutations[i].addedNodes;
                for (var j = 0; j < added.length; j++) check(added[j]);
            }
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (document.body) start();
    else document.addEventListener('DOMContentLoaded', start);
})();

// Enter moves to the next step. Inside a [data-enter-flow] container, Enter in an input marked
// [data-enter-step] focuses the next visible, enabled step, so a wristband or unit scan (which ends with
// Enter) flows into the next field instead of submitting. After the last step, focus goes to the
// container itself and NEVER to a button: the next scan's Enter must not issue, receive, or document
// anything. The action stays a deliberate click (or Tab then Enter).
(function () {
    function usable(el) {
        return !el.disabled && !el.readOnly && el.offsetParent !== null;
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' || e.isComposing || e.shiftKey || e.ctrlKey || e.altKey || e.metaKey) return;
        var el = e.target;
        if (!(el instanceof HTMLInputElement) || !el.hasAttribute('data-enter-step')) return;
        var flow = el.closest('[data-enter-flow]');
        if (!flow) return;

        e.preventDefault();
        var steps = Array.prototype.filter.call(flow.querySelectorAll('input[data-enter-step]'), usable);
        var next = steps[steps.indexOf(el) + 1];
        // Moving focus blurs this field, which fires its change event, so change-bound lookups still run.
        if (next) {
            next.focus();
            if (next.select) next.select();
            return;
        }

        if (!flow.hasAttribute('tabindex')) flow.setAttribute('tabindex', '-1');
        flow.focus({ preventScroll: true });
        flow.classList.add('bb-enter-done');
        setTimeout(function () { flow.classList.remove('bb-enter-done'); }, 1600);
    }, true);
})();

// "?" (outside a text field) shows the keyboard shortcuts; Esc or "?" closes them.
(function () {
    var rows = [
        ['/', 'Focus the find box (also Ctrl+K)'],
        ['Enter', 'In the find box: open the record, or list matches'],
        ['↑ ↓, Enter', 'Choose a match in the find box list'],
        ['Esc', 'Close the find box list or this panel'],
        ['Enter (issue, ward receipt, bedside)', 'Move to the next identifier or unit-label field. After the last field, focus returns to the form; Issue, Receive, and Document are never pressed by Enter alone'],
        ['?', 'Show or hide this panel']
    ];
    var panel = null;

    function typing(t) {
        return t && (t.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].indexOf(t.tagName) >= 0);
    }

    function close() {
        if (panel) { panel.remove(); panel = null; }
    }

    function open() {
        panel = document.createElement('div');
        panel.className = 'bb-dialog-backdrop bb-shortcuts';
        var box = document.createElement('div');
        box.className = 'bb-dialog';
        box.setAttribute('role', 'dialog');
        box.setAttribute('aria-modal', 'true');
        box.setAttribute('aria-label', 'Keyboard shortcuts');
        box.style.width = 'min(560px, 100%)';
        var h = document.createElement('h3');
        h.textContent = 'Keyboard shortcuts';
        box.appendChild(h);
        var table = document.createElement('table');
        table.className = 'table';
        rows.forEach(function (r) {
            var tr = document.createElement('tr');
            var k = document.createElement('td');
            var kbd = document.createElement('kbd');
            kbd.textContent = r[0];
            k.appendChild(kbd);
            var d = document.createElement('td');
            d.textContent = r[1];
            tr.appendChild(k);
            tr.appendChild(d);
            table.appendChild(tr);
        });
        box.appendChild(table);
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-secondary btn-sm';
        btn.textContent = 'Close';
        btn.addEventListener('click', close);
        box.appendChild(btn);
        panel.appendChild(box);
        panel.addEventListener('click', function (e) { if (e.target === panel) close(); });
        document.body.appendChild(panel);
        btn.focus();
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && panel) { close(); return; }
        if (e.key !== '?' || e.ctrlKey || e.altKey || e.metaKey || typing(e.target)) return;
        e.preventDefault();
        if (panel) close(); else open();
    });
})();
