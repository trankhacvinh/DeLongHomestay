(function () {
    if (!document.body?.classList.contains('public-body')) return;

    const VALUES = {
        desktop: {
            xs: 'clamp(.72rem,.68rem + .15vw,.84rem)',
            sm: 'clamp(.9rem,.84rem + .24vw,1.06rem)',
            md: 'clamp(1.08rem,1rem + .34vw,1.34rem)',
            lg: 'clamp(1.42rem,1.16rem + .9vw,2.05rem)',
            xl: 'clamp(2rem,1.42rem + 2vw,3.35rem)',
            hero: 'clamp(3rem,1.8rem + 4.8vw,6.4rem)'
        },
        tablet: {
            xs: '.75rem', sm: '.9rem', md: '1.1rem', lg: '1.55rem', xl: '2.35rem', hero: 'clamp(2.6rem,7vw,4.7rem)'
        },
        mobile: {
            xs: '.72rem', sm: '.86rem', md: '1rem', lg: '1.32rem', xl: '1.9rem', hero: 'clamp(2.2rem,12vw,3.5rem)'
        }
    };
    const KEYS = Object.keys(VALUES.desktop);
    const selector = '[class*="cp-size-"],[class*="cp-tablet-size-"],[class*="cp-mobile-size-"]';
    let queued = false;

    function keyFor(element, breakpoint) {
        const prefix = breakpoint === 'desktop' ? 'cp-size-' : `cp-${breakpoint}-size-`;
        return KEYS.find(key => element.classList.contains(`${prefix}${key}`)) || '';
    }

    function previewBreakpoint(element) {
        if (element.classList.contains('cp-style-preview-mobile')) return 'mobile';
        if (element.classList.contains('cp-style-preview-tablet')) return 'tablet';
        if (window.innerWidth <= 680) return 'mobile';
        if (window.innerWidth <= 1024) return 'tablet';
        return 'desktop';
    }

    function naturalSize(element) {
        const removed = [];
        ['desktop', 'tablet', 'mobile'].forEach(breakpoint => {
            const prefix = breakpoint === 'desktop' ? 'cp-size-' : `cp-${breakpoint}-size-`;
            KEYS.forEach(key => {
                const className = `${prefix}${key}`;
                if (element.classList.contains(className)) {
                    removed.push(className);
                    element.classList.remove(className);
                }
            });
        });
        const hadInline = element.dataset.cpRuntimeFontSize === '1';
        const previousValue = element.style.getPropertyValue('font-size');
        const previousPriority = element.style.getPropertyPriority('font-size');
        if (hadInline) element.style.removeProperty('font-size');
        const value = getComputedStyle(element).fontSize;
        removed.forEach(className => element.classList.add(className));
        if (hadInline && previousValue) element.style.setProperty('font-size', previousValue, previousPriority || 'important');
        return value;
    }

    function syncElement(element) {
        if (!(element instanceof HTMLElement)) return;
        const desktop = keyFor(element, 'desktop');
        const tablet = keyFor(element, 'tablet');
        const mobile = keyFor(element, 'mobile');
        if (!desktop && !tablet && !mobile) {
            if (element.dataset.cpRuntimeFontSize === '1') {
                element.style.removeProperty('font-size');
                element.style.removeProperty('line-height');
                delete element.dataset.cpRuntimeFontSize;
                delete element.dataset.cpRuntimeLineHeight;
            }
            return;
        }

        const breakpoint = previewBreakpoint(element);
        let source = 'desktop';
        let key = desktop;
        if (breakpoint === 'tablet' && tablet) { source = 'tablet'; key = tablet; }
        if (breakpoint === 'mobile' && mobile) { source = 'mobile'; key = mobile; }

        // Tablet/Mobile intentionally inherit Desktop directly. If Desktop is "Mặc định",
        // measure the component's natural typography with editor size classes temporarily removed.
        const value = key ? VALUES[source][key] : naturalSize(element);
        if (value) {
            element.style.setProperty('font-size', value, 'important');
            element.dataset.cpRuntimeFontSize = '1';
        }

        if (key === 'hero') {
            element.style.setProperty('line-height', source === 'mobile' ? '1' : source === 'tablet' ? '.98' : '.96', 'important');
            element.dataset.cpRuntimeLineHeight = '1';
        } else if (element.dataset.cpRuntimeLineHeight === '1') {
            element.style.removeProperty('line-height');
            delete element.dataset.cpRuntimeLineHeight;
        }
    }

    function sync(root) {
        if (root instanceof HTMLElement && root.matches(selector)) syncElement(root);
        (root.querySelectorAll ? root.querySelectorAll(selector) : []).forEach(syncElement);
    }

    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(() => {
            queued = false;
            sync(document);
        });
    }

    new MutationObserver(records => {
        if (records.some(record => record.type === 'childList' || record.attributeName === 'class')) queue();
    }).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });

    let resizeTimer = 0;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(queue, 80);
    }, { passive: true });

    queue();
})();
