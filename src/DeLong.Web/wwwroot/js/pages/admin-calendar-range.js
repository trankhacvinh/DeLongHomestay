(function () {
    function dateKey(value) {
        return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
    }

    function dayCount(from, to) {
        const fromUtc = Date.UTC(from.getFullYear(), from.getMonth(), from.getDate());
        const toUtc = Date.UTC(to.getFullYear(), to.getMonth(), to.getDate());
        return Math.round((toUtc - fromUtc) / 86400000) + 1;
    }

    window.DeLongCalendarRangePicker = {
        create(input, options) {
            if (!input || typeof window.flatpickr !== 'function') return null;

            const maxDays = Math.max(1, Number(options.maxDays || 31));
            let currentDates = [options.startDate, options.endDate].filter(Boolean);
            const instance = window.flatpickr(input, {
                mode: 'range',
                dateFormat: 'Y-m-d',
                altInput: true,
                altFormat: 'd/m/Y',
                defaultDate: currentDates,
                disableMobile: true,
                locale: {
                    firstDayOfWeek: 1,
                    rangeSeparator: ' đến '
                },
                onChange(selectedDates, _dateText, instance) {
                    if (selectedDates.length !== 2) return;
                    const days = dayCount(selectedDates[0], selectedDates[1]);
                    if (days > maxDays) {
                        instance.setDate(currentDates, false);
                        options.onError?.(`Chỉ có thể xem tối đa ${maxDays} ngày trong một lần.`);
                        return;
                    }

                    currentDates = [dateKey(selectedDates[0]), dateKey(selectedDates[1])];
                    instance.close();
                    options.onApply?.({
                        from: dateKey(selectedDates[0]),
                        to: dateKey(selectedDates[1]),
                        days
                    });
                }
            });

            return {
                setDate(dates, triggerChange) {
                    currentDates = Array.isArray(dates) ? dates.slice(0, 2) : [dates];
                    instance.setDate(currentDates, triggerChange === true);
                },
                destroy() {
                    instance.destroy();
                }
            };
        }
    };
})();
