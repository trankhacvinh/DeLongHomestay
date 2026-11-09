(function (global) {
    'use strict';

    if (typeof global.flatpickr !== 'function') return;

    const selector = 'input[type="date"], input[type="datetime-local"], input[type="month"]';
    const fields = new Map();
    const openPickers = new Set();
    const valueProperty = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
    const locale = global.flatpickr.l10ns.vn || global.flatpickr.l10ns.default;
    global.flatpickr.setDefaults({ locale, time_24hr: true, disableMobile: true });

    function parseDate(value, format) {
        if (value instanceof Date) return Number.isNaN(value.getTime()) ? undefined : value;
        const text = String(value || '').trim();
        let parts;
        let year, month, day = 1, hour = 0, minute = 0;
        if (format === 'm/Y') {
            parts = /^(\d{2})\/(\d{4})$/.exec(text);
            if (parts) [, month, year] = parts.map(Number);
        } else if (format.startsWith('d/m/Y')) {
            parts = /^(\d{2})\/(\d{2})\/(\d{4})(?: (\d{2}):(\d{2}))?$/.exec(text);
            if (parts) [, day, month, year, hour = 0, minute = 0] = parts.map(item => item === undefined ? undefined : Number(item));
        } else {
            parts = /^(\d{4})-(\d{2})(?:-(\d{2}))?(?:T(\d{2}):(\d{2})(?::00(?:\.000)?)?)?$/.exec(text);
            if (parts) [, year, month, day = 1, hour = 0, minute = 0] = parts.map(item => item === undefined ? undefined : Number(item));
        }
        if (!parts || year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31 || hour > 23 || minute > 59) return undefined;
        const result = new Date(0);
        result.setFullYear(year, month - 1, day);
        result.setHours(hour, minute, 0, 0);
        return result.getFullYear() === year && result.getMonth() === month - 1 && result.getDate() === day &&
            result.getHours() === hour && result.getMinutes() === minute ? result : undefined;
    }

    function enhance(input) {
        if (!input.isConnected || input.closest('[v-cloak]') || fields.has(input) || input._flatpickr) return;
        const kind = input.type;
        if (kind === 'month' && typeof global.monthSelectPlugin !== 'function') return;
        const format = kind === 'datetime-local' ? 'Y-m-d\\TH:i' : (kind === 'month' ? 'Y-m' : 'Y-m-d');
        const displayFormat = kind === 'datetime-local' ? 'd/m/Y H:i' : (kind === 'month' ? 'm/Y' : 'd/m/Y');
        const labels = Array.from(input.labels || []);
        const defaultValue = input.defaultValue;
        const previousValueProperty = Object.getOwnPropertyDescriptor(input, 'value');
        const instance = global.flatpickr(input, {
            locale,
            dateFormat: format,
            altInput: true,
            altFormat: displayFormat,
            ariaDateFormat: 'd/m/Y',
            enableTime: kind === 'datetime-local',
            time_24hr: true,
            minuteIncrement: 1,
            allowInput: true,
            allowInvalidPreload: true,
            disableMobile: true,
            parseDate,
            minDate: input.min || undefined,
            maxDate: input.max || undefined,
            plugins: kind === 'month' ? [new global.monthSelectPlugin({ dateFormat: format, altFormat: displayFormat })] : []
        });
        if (!instance?.altInput) return;
        instance.config.allowInvalidPreload = false;

        input.dataset.dateInput = kind;
        const display = instance.altInput;
        display.dataset.dateDisplay = kind;
        display.placeholder = kind === 'datetime-local' ? 'dd/MM/yyyy HH:mm' : (kind === 'month' ? 'MM/yyyy' : 'dd/MM/yyyy');
        for (const attribute of ['aria-label', 'aria-labelledby', 'aria-describedby', 'title']) {
            if (input.hasAttribute(attribute)) display.setAttribute(attribute, input.getAttribute(attribute));
        }
        if (input.id) {
            display.id = `${input.id}-display`;
            for (const label of labels) if (label.htmlFor === input.id) label.htmlFor = display.id;
        }

        let synchronizing = false;
        // Vue writes input.value directly; update the display without emitting another change event.
        Object.defineProperty(input, 'value', {
            configurable: true,
            get() { return valueProperty.get.call(this); },
            set(value) {
                valueProperty.set.call(this, value);
                if (synchronizing) return;
                const current = instance.selectedDates[0] ? instance.formatDate(instance.selectedDates[0], format) : '';
                if (current === this.value) { syncAttributes(this); return; }
                synchronizing = true;
                instance.config.allowInvalidPreload = true;
                try { instance.setDate(this.value, false, format); }
                finally { instance.config.allowInvalidPreload = false; synchronizing = false; syncAttributes(this); }
            }
        });

        fields.set(input, { instance, labels, previousValueProperty, defaultValue });
        if (instance.timeContainer) openPickers.add(instance);
        syncAttributes(input);
    }

    function syncAttributes(input, attribute) {
        const field = fields.get(input);
        if (!field) return;
        const { instance } = field;
        const display = instance.altInput;
        display.disabled = input.disabled;
        display.required = input.required;
        display.readOnly = input.readOnly;
        if (attribute === 'min' || attribute === 'max') {
            const value = input.value;
            instance.set(attribute === 'min' ? 'minDate' : 'maxDate', input.getAttribute(attribute) || undefined);
            input.value = value;
        }
        const outsideRange = input.value && ((input.min && input.value < input.min) || (input.max && input.value > input.max));
        display.setCustomValidity(outsideRange ? 'Ngày đã chọn nằm ngoài khoảng cho phép.' : '');
    }

    function scan(root) {
        if (root.nodeType !== 1 && root.nodeType !== 9) return;
        if (root.matches?.(selector)) enhance(root);
        root.querySelectorAll(selector).forEach(enhance);
    }

    // Typing an hour/minute and then tapping outside the picker closed it before the typed value was
    // read (flatpickr only reads the time boxes on blur), so the display and the form kept the old
    // time. Blur the time box first so flatpickr commits it, then let the outside tap close the picker.
    function commitTypedTime(event) {
        const active = document.activeElement;
        for (const instance of openPickers) {
            if (!instance.isOpen || !instance.timeContainer?.contains(active)) continue;
            if (instance.calendarContainer?.contains(event.target)) continue;
            active.blur();
        }
    }

    function start() {
        for (const type of ['pointerdown', 'mousedown', 'touchstart'])
            document.addEventListener(type, commitTypedTime, { capture: true, passive: true });
        scan(document);
        new MutationObserver(mutations => {
            for (const mutation of mutations) {
                if (mutation.type === 'attributes') {
                    if (mutation.attributeName === 'data-v-app' || mutation.attributeName === 'v-cloak') scan(mutation.target);
                    else syncAttributes(mutation.target, mutation.attributeName);
                }
                else mutation.addedNodes.forEach(scan);
            }
            for (const [input, field] of fields) {
                if (input.isConnected) continue;
                fields.delete(input);
                openPickers.delete(field.instance);
                if (field.previousValueProperty) Object.defineProperty(input, 'value', field.previousValueProperty);
                else delete input.value;
                for (const label of field.labels) if (label.htmlFor === field.instance.altInput.id) label.htmlFor = input.id;
                field.instance.destroy();
                delete input._flatpickr;
                delete input.dataset.dateInput;
            }
        }).observe(document.body, {
            childList: true, subtree: true, attributes: true,
            attributeFilter: ['min', 'max', 'disabled', 'readonly', 'required', 'data-v-app', 'v-cloak']
        });
        document.addEventListener('reset', event => {
            setTimeout(() => {
                if (event.defaultPrevented) return;
                for (const [input, { defaultValue }] of fields) {
                    if (input.form === event.target) input.value = defaultValue;
                }
            }, 0);
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
    else start();
})(window);
