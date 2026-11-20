/*
 * Theme 7 · Mềm mại. No libraries. Blocks that start below the fold fade in once as they scroll into view; what the
 * guest sees first is never hidden. Leaf shadows and the lamp glow are plain CSS animations.
 * Reduced motion: nothing is hidden or animated. Booking scripts are shared by every theme and untouched.
 */
(function () {
    "use strict";
    if (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    if (!("IntersectionObserver" in window)) return;

    function start() {
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                var el = entry.target;
                el.classList.add("t7-shown");
                requestAnimationFrame(function () { el.classList.remove("t7-wait"); });
                io.unobserve(el);
            });
        }, { rootMargin: "0px 0px -6% 0px", threshold: 0.08 });
        var fold = window.innerHeight;
        Array.prototype.forEach.call(document.querySelectorAll("[data-t7-reveal]"), function (el, i) {
            if (el.getBoundingClientRect().top < fold) return;
            el.classList.add("t7-wait");
            el.style.transitionDelay = (i % 4) * 0.08 + "s";
            io.observe(el);
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();
