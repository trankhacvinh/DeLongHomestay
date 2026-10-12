const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const source = fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/public-shell-runtime.js', 'utf8');
const block = source.slice(source.indexOf('    function applyNav('), source.indexOf('    function applyFooter('));
function node(tagName) { return { tagName: tagName.toUpperCase(), dataset: {}, remove() { this.parent.children.splice(this.parent.children.indexOf(this), 1); } }; }
test('rebuilding navigation keeps one FAQ after all links, including repeated updates', () => {
    const faq = node('BUTTON'), old = node('A');
    const container = { children: [], querySelector: () => faq, appendChild(n) { if (n.parent) n.remove(); n.parent = this; this.children.push(n); } };
    container.appendChild(old); container.appendChild(faq);
    const doc = { querySelector: () => null, createElement: node };
    const apply = new Function('document', 'navigationItems', 'keyForAnchor', 'resolveUrl', `${block}; return applyNav;`)(doc, shell => shell.navigationItems, () => 'home', url => url);
    const shell = { navigationItems: [{ id: 'home', label: 'Trang chủ', url: '/' }, { id: 'lookup', label: 'Tra cứu', url: '/booking/lookup' }] };
    for (let i = 0; i < 2; i++) {
        apply(container, shell);
        assert.deepEqual(container.children.map(x => x.tagName), ['A', 'A', 'BUTTON']);
        assert.equal(container.children[2], faq);
    }
});

test('embedded booking never initializes the website editor or reserves its toolbar', () => {
    const editor = fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/public-visual-editor-v2.js', 'utf8');
    require('node:vm').runInNewContext(editor, {
        document: { body: { classList: { contains: name => ['public-body', 'public-embedded-page'].includes(name) } } },
        window: { DeLongApi: {} }
    });
});
