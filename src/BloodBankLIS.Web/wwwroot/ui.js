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
