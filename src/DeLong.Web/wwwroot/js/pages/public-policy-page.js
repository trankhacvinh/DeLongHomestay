// Policy pages (2026-11 layout): turns the policy's own rich text into numbered sections.
// A section starts at a heading (h1-h3) or at a paragraph written only in bold, such as
// "I. Điều kiện…" or "1. Thời gian…". Roman-numbered or h1/h2 headings become sections and
// the numbered ones inside them become sub-headings; with no such top level, numbered bold
// lines become the sections. Nothing is saved: the stored HTML stays exactly as the admin wrote it,
// and the page is left untouched while the inline editor is available.
(function () {
    const body = document.querySelector('[data-policy-body]');
    if (!body || document.body.classList.contains('public-editor-enabled')) return;

    let root = body;
    while (root.children.length === 1 && /^(DIV|SECTION|ARTICLE)$/.test(root.firstElementChild.tagName)
        && ![...root.childNodes].some(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim())) {
        root = root.firstElementChild;
    }

    const clean = text => text.replace(/\s+/g, ' ').trim();
    const roman = /^[IVXLC]+\s*[.)\-–:]\s*/;
    const numbered = /^\d+(\.\d+)*\s*[.)\-–:]\s*/;

    function boldOnly(element) {
        if (!/^(P|DIV)$/.test(element.tagName)) return false;
        const text = clean(element.textContent);
        if (!text || text.length > 160) return false;
        const bold = clean([...element.querySelectorAll('strong,b')].map(node => node.textContent).join(' '));
        return bold.length > 0 && bold === text;
    }

    function headingLevel(node) {
        if (node.nodeType !== Node.ELEMENT_NODE) return 0;
        const text = clean(node.textContent);
        if (!text) return 0;
        if (/^H[1-2]$/.test(node.tagName)) return 1;
        if (/^H[3-6]$/.test(node.tagName)) return roman.test(text) ? 1 : 2;
        if (!boldOnly(node)) return 0;
        return roman.test(text) ? 1 : 2;
    }

    // The page lead (Hero text) often repeats as the first line of the policy text: show it once.
    const lead = clean(document.querySelector('.lux-policy-lead')?.textContent || '');
    const first = root.firstElementChild;
    if (lead && first && first.tagName === 'P' && clean(first.textContent) === lead) first.remove();

    const nodes = [...root.childNodes].filter(node => node.nodeType === Node.ELEMENT_NODE || clean(node.textContent || ''));
    const levels = nodes.map(headingLevel);
    const top = levels.includes(1) ? 1 : (levels.includes(2) ? 2 : 0);
    if (!top) return;

    const fragment = document.createDocumentFragment();
    let current = null;
    let index = 0;
    nodes.forEach((node, position) => {
        const level = levels[position];
        if (level === top) {
            index += 1;
            const section = document.createElement('section');
            section.className = 'lux-policy-section';
            const number = document.createElement('span');
            number.className = 'lux-policy-section-index';
            number.setAttribute('aria-hidden', 'true');
            number.textContent = String(index).padStart(2, '0');
            const inner = document.createElement('div');
            const title = document.createElement('h2');
            title.className = 'lux-policy-section-title';
            title.textContent = clean(node.textContent).replace(roman, '').replace(numbered, '').replace(/:$/, '');
            current = document.createElement('div');
            current.className = 'lux-policy-section-body';
            inner.append(title, current);
            section.append(number, inner);
            fragment.append(section);
            return;
        }
        if (level && level > top && node.nodeType === Node.ELEMENT_NODE) node.classList.add('lux-policy-sub');
        (current || fragment).append(node);
    });
    root.replaceChildren(fragment);
    body.classList.add('is-sectioned');
})();
