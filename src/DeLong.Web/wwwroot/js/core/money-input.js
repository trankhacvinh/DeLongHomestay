(function () {
    function sync(element, binding) {
        element.__delongMoneyBinding = binding.value;
        const value = Number(binding.value.value || 0);
        const current = Number(element.inputmask?.unmaskedvalue() || element.value.replace(/\./g, '') || 0);
        if (current !== value) {
            if (element.inputmask) element.inputmask.setValue(String(value));
            else element.value = String(value);
        }
    }
    window.DeLongMoneyInput = { directives: { moneyInput: {
        mounted(element, binding) {
            element.value = String(Number(binding.value.value || 0));
            if (window.Inputmask) new Inputmask({ alias: 'numeric', groupSeparator: '.', radixPoint: ',',
                autoGroup: true, digits: 0, allowMinus: false, rightAlign: false,
                nullable: false, showMaskOnHover: false, showMaskOnFocus: false }).mask(element);
            element.__delongMoneyBinding = binding.value;
            element.__delongMoneyListener = () => {
                const value = Number(element.inputmask?.unmaskedvalue() || element.value.replace(/\./g, '') || 0);
                const model = element.__delongMoneyBinding;
                if (Number.isFinite(value) && value !== Number(model.value || 0)) model.set(value);
            };
            element.addEventListener('input', element.__delongMoneyListener);
            element.addEventListener('change', element.__delongMoneyListener);
        },
        updated: sync,
        beforeUnmount(element) {
            element.removeEventListener('input', element.__delongMoneyListener);
            element.removeEventListener('change', element.__delongMoneyListener);
            element.inputmask?.remove();
            delete element.__delongMoneyListener;
            delete element.__delongMoneyBinding;
        }
    } } };
})();
