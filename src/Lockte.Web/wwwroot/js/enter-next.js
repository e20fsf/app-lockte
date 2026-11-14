// Enter moves to the next field inside any element marked data-enter-next (UAT วัตถุดิบ รอบ 2: filling a form with the mouse was too slow).
// Dropdowns (read-only inputs, an open list), text areas and buttons keep their own Enter.
document.addEventListener('keydown', function (e) {
    if (e.key !== 'Enter' || e.defaultPrevented || e.isComposing) return;
    var input = e.target;
    if (!(input instanceof HTMLInputElement) || input.readOnly || input.getAttribute('aria-expanded') === 'true') return;
    var container = input.closest('[data-enter-next]');
    if (!container) return;
    var fields = Array.prototype.filter.call(
        container.querySelectorAll('input:not([type=hidden]):not([type=checkbox]):not([disabled]), textarea:not([disabled])'),
        function (f) { return f.offsetParent !== null && f.tabIndex >= 0; });
    var next = fields[fields.indexOf(input) + 1];
    if (!next) return;
    e.preventDefault();
    next.focus();
    if (typeof next.select === 'function' && next.type !== 'number') next.select();
});
