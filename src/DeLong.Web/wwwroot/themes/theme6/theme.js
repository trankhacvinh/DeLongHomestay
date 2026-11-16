/*
 * Theme 6 · Cinema. GSAP + ScrollTrigger:
 *  1. a countdown leader plays once on the screen, then focus racks from the seats to the picture;
 *  2. scrolling the hero walks through the seat backs (foreground occlusion, blur growing as they pass the lens)
 *     while the film gate opens until the picture fills the screen;
 *  3. dust drifting in the projector beam (2D canvas, paused off screen);
 *  4. a velvet curtain opens in front of the rooms as they scroll in;
 *  5. the highlights roll like end credits through a focus plane: sharp in the middle of the screen, soft above and below.
 * Reduced motion: none of it; the page reads as a still poster. Booking scripts are shared and untouched.
 */
(function () {
    "use strict";
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var gsap = window.gsap, ST = window.ScrollTrigger;
    var small = window.matchMedia("(max-width: 760px)").matches;

    function leaderThenFocus(hero) {
        var leader = hero.querySelector("[data-t6-leader]"), count = hero.querySelector("[data-t6-count]");
        var card = hero.querySelector("[data-t6-card]"), seats = hero.querySelector("[data-t6-seats]"), shot = hero.querySelector("[data-t6-shot]");
        var actions = hero.querySelector("[data-t6-actions]");
        if (!leader || !count) return;
        hero.classList.add("is-leader");
        gsap.set(card, { autoAlpha: 0 });
        gsap.set(actions, { autoAlpha: 0, y: 16 });
        var tl = gsap.timeline();
        [3, 2, 1].forEach(function (n, i) {
            tl.call(function () { count.textContent = String(n); }, null, i * 0.55)
              .fromTo(leader, { "--t6-sweep": "0deg" }, { "--t6-sweep": "360deg", duration: 0.55, ease: "none" }, i * 0.55);
        });
        tl.call(function () { hero.classList.remove("is-leader"); });
        // Rack focus: the seats start sharp and the screen soft, then focus pulls to the screen.
        tl.fromTo(seats, { "--t6-seat-blur": "0px" }, { "--t6-seat-blur": (small ? 4 : 6) + "px", duration: 1.6, ease: "power2.inOut" }, ">")
          .fromTo(shot, { "--t6-shot-blur": "14px" }, { "--t6-shot-blur": "0px", duration: 1.6, ease: "power2.inOut" }, "<")
          .fromTo(card, { autoAlpha: 0, filter: "blur(10px)" }, { autoAlpha: 1, filter: "blur(0px)", duration: 1.3, ease: "power2.out", clearProps: "filter" }, "<0.3")
          .to(actions, { autoAlpha: 1, y: 0, duration: 0.9, ease: "power2.out" }, "<0.4");
        return tl;
    }

    function walkIn(hero) {
        if (!ST) return;
        var stage = hero.querySelector(".t6-hero-stage"), gate = hero.querySelector("[data-t6-gate]");
        var seats = hero.querySelector("[data-t6-seats]"), card = hero.querySelector("[data-t6-card]");
        var strips = hero.querySelectorAll(".t6-strip"), beam = hero.querySelector(".t6-beam"), actions = hero.querySelector("[data-t6-actions]");
        var shot = hero.querySelector("[data-t6-shot]");
        // Layout sizes (offset*) ignore the transforms GSAP is applying, so these stay right on refresh.
        function fill() { return Math.max(stage.clientWidth / gate.offsetWidth, stage.clientHeight / gate.offsetHeight) * 1.04; }
        function centre() { return stage.clientHeight / 2 - (gate.offsetTop + gate.offsetHeight / 2); }
        var tl = gsap.timeline({
            defaults: { ease: "none" },
            scrollTrigger: { trigger: hero, start: "top top", end: small ? "+=70%" : "+=110%", pin: stage, scrub: 0.6, invalidateOnRefresh: true }
        });
        tl.fromTo(seats, { yPercent: 0, scale: 1 }, { yPercent: 95, scale: 1.45, duration: 1 }, 0)
          .to(seats, { "--t6-seat-blur": "18px", duration: 0.6 }, 0)
          .to(actions, { autoAlpha: 0, y: 40, duration: 0.3 }, 0)
          .to(strips, { autoAlpha: 0, duration: 0.35 }, 0.1)
          .to(beam, { autoAlpha: 0, duration: 0.5 }, 0)
          .to(card, { autoAlpha: 0, y: -30, duration: 0.45 }, 0.05)
          .to(gate, { scale: fill, y: centre, duration: 1 }, 0)
          .fromTo(shot, { "--t6-shot-scale": 1 }, { "--t6-shot-scale": 1.08, duration: 1 }, 0);
    }

    function dust(canvas) {
        var ctx = canvas.getContext("2d");
        if (!ctx) return;
        var W = 0, H = 0, motes = [], visible = false, raf = 0, last = 0;
        function size() {
            var r = canvas.getBoundingClientRect(), dpr = Math.min(window.devicePixelRatio || 1, 1.5);
            W = r.width; H = r.height;
            canvas.width = Math.round(W * dpr); canvas.height = Math.round(H * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            var n = small ? 36 : 80;
            motes = [];
            for (var i = 0; i < n; i++) motes.push({ x: Math.random(), y: Math.random(), s: 0.4 + Math.random() * 1.4, v: 0.004 + Math.random() * 0.01, p: Math.random() * 6.28 });
        }
        var color = getComputedStyle(document.body).getPropertyValue("--t6-screen").trim() || "#f2e6cf";
        function frame(t) {
            raf = 0;
            if (!visible || document.hidden) return;
            raf = requestAnimationFrame(frame);
            var dt = last ? Math.min((t - last) / 1000, 0.05) : 0.016; last = t;
            ctx.clearRect(0, 0, W, H);
            ctx.fillStyle = color;
            motes.forEach(function (m) {
                m.y -= m.v * dt * 6; m.x += Math.sin(t / 1600 + m.p) * 0.00025;
                if (m.y < 0) { m.y = 1; m.x = Math.random(); }
                // Only motes inside the cone catch the light.
                var spread = 0.06 + m.y * 0.16, inBeam = Math.abs(m.x - 0.5) < spread;
                if (!inBeam) return;
                ctx.globalAlpha = (0.25 + 0.5 * Math.abs(Math.sin(t / 900 + m.p))) * (1 - Math.abs(m.x - 0.5) / spread);
                ctx.beginPath(); ctx.arc(m.x * W, m.y * H, m.s, 0, 6.283); ctx.fill();
            });
            ctx.globalAlpha = 1;
        }
        function start() { if (!raf && visible && !document.hidden) { last = 0; raf = requestAnimationFrame(frame); } }
        size();
        window.addEventListener("resize", size);
        new IntersectionObserver(function (en) { visible = en[0].isIntersecting; start(); }).observe(canvas);
        document.addEventListener("visibilitychange", start);
    }

    function curtain(section) {
        if (!ST) return;
        var l = section.querySelector('[data-t6-curtain="l"]'), r = section.querySelector('[data-t6-curtain="r"]');
        var trig = { trigger: section, start: "top 88%", end: small ? "top 30%" : "top 15%", scrub: 0.5 };
        gsap.fromTo([l, r], { xPercent: 0, "--t6-curtain-blur": "0px" }, {
            xPercent: function (i) { return i === 0 ? -105 : 105; }, "--t6-curtain-blur": "10px", ease: "power1.in", scrollTrigger: trig
        });
    }

    /* Depth of field on the credits: the distance from the middle of the viewport sets the blur. */
    function focusPlane(root) {
        var items = Array.prototype.slice.call(root.querySelectorAll("[data-t6-focus]"));
        if (!items.length) return;
        var ticking = false, active = false;
        function update() {
            ticking = false;
            var vh = window.innerHeight, mid = vh / 2;
            items.forEach(function (el) {
                var r = el.getBoundingClientRect(), c = r.top + r.height / 2;
                var d = Math.abs(c - mid) / mid;
                var blur = Math.min(7, Math.max(0, d - 0.22) * 9), fade = Math.max(0.25, 1 - Math.max(0, d - 0.3) * 0.9);
                el.style.setProperty("--t6-blur", blur.toFixed(2) + "px");
                el.style.setProperty("--t6-fade", fade.toFixed(3));
            });
        }
        function onScroll() { if (active && !ticking) { ticking = true; requestAnimationFrame(update); } }
        new IntersectionObserver(function (en) { active = en[0].isIntersecting; if (active) onScroll(); }, { rootMargin: "100px" }).observe(root);
        window.addEventListener("scroll", onScroll, { passive: true });
        window.addEventListener("resize", onScroll);
    }

    function init() {
        if (reduceMotion || !gsap) return;
        if (ST) gsap.registerPlugin(ST);
        document.documentElement.classList.add("t6-js");
        var hero = document.querySelector("[data-t6-hero]");
        if (hero) {
            walkIn(hero);
            // The countdown waits for the page to finish loading so it is not spent while images still arrive.
            if (document.readyState === "complete") leaderThenFocus(hero);
            else { hero.classList.add("is-leader"); window.addEventListener("load", function () { leaderThenFocus(hero); }); }
        }
        Array.prototype.forEach.call(document.querySelectorAll("[data-t6-dust]"), dust);
        Array.prototype.forEach.call(document.querySelectorAll("[data-t6-rooms]"), curtain);
        Array.prototype.forEach.call(document.querySelectorAll("[data-t6-credits]"), focusPlane);
        if (ST) window.addEventListener("load", function () { ST.refresh(); });
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init); else init();
})();
