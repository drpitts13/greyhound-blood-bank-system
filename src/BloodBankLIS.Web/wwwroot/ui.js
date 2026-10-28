window.bbUi = {
    scrollIntoView: function (id) {
        var el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
};
