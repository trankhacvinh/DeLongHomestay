(function () {
    const root = document.getElementById('housekeeping-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('housekeeping-page-data').textContent || '{}');
    const { createApp } = Vue;
    const timeZone = initial.timeZoneId || 'Asia/Ho_Chi_Minh';

    function parseDateKey(key) {
        const [year, month, day] = String(key || '').split('-').map(Number);
        return new Date(Date.UTC(year, month - 1, day));
    }

    function dateKey(value) {
        return `${value.getUTCFullYear()}-${String(value.getUTCMonth() + 1).padStart(2, '0')}-${String(value.getUTCDate()).padStart(2, '0')}`;
    }

    function addDays(key, amount) {
        const value = parseDateKey(key);
        value.setUTCDate(value.getUTCDate() + amount);
        return dateKey(value);
    }

    function emptyReportForm() {
        return { open: false, roomId: '', inspectionType: 'Routine', severity: 'Normal', content: '', tags: [], photos: [], saving: false };
    }

    async function optimizePhoto(file) {
        if (!/^image\/(jpeg|png|webp)$/i.test(file.type)) return file;
        const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
        try {
            const maxEdge = 1920;
            const scale = Math.min(1, maxEdge / Math.max(bitmap.width, bitmap.height));
            const width = Math.max(1, Math.round(bitmap.width * scale));
            const height = Math.max(1, Math.round(bitmap.height * scale));
            const canvas = document.createElement('canvas');
            canvas.width = width;
            canvas.height = height;
            canvas.getContext('2d', { alpha: false }).drawImage(bitmap, 0, 0, width, height);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .82));
            if (!blob || blob.size >= file.size) return file;
            const baseName = file.name.replace(/\.[^.]+$/, '') || 'anh-phong';
            return new File([blob], `${baseName}.jpg`, { type: 'image/jpeg', lastModified: Date.now() });
        } finally {
            bitmap.close();
        }
    }

    const vm = createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                rooms: initial.rooms || [],
                today: initial.today,
                selectedDate: initial.today,
                mode: 'status',
                taskFilter: 'all',
                schedule: initial.schedule || { calendar: [] },
                conditionTags: initial.conditionTags || [],
                conditionReports: initial.conditionReports || [],
                expandedReportId: null,
                reportForm: emptyReportForm(),
                scheduleLoading: false,
                scheduleQueued: false,
                copied: false,
                canManage: window.DeLongHousekeepingCanManage === true,
                savingId: null,
                toast: { show: false, message: '', type: 'success', timer: null },
                refreshInFlight: false,
                refreshQueued: false
            };
        },
        computed: {
            cleanRooms() { return this.rooms.filter(x => x.status === 0); },
            dirtyRooms() { return this.rooms.filter(x => x.status === 1); },
            cleaningRooms() { return this.rooms.filter(x => x.status === 2); },
            scheduleDays() {
                return (this.schedule?.calendar || []).map(day => ({
                    date: day.date,
                    tasks: (day.tasks || []).filter(task => this.taskFilter === 'all' || task.kind === this.taskFilter)
                }));
            },
            taskCount() {
                return this.scheduleDays.reduce((total, day) => total + day.tasks.length, 0);
            },
            scheduleText() {
                return this.scheduleDays
                    .filter(day => day.tasks.length)
                    .map(day => [
                        this.dateText(day.date),
                        ...day.tasks.map(task => `${this.taskTime(task)} ${task.text}`)
                    ].join('\n'))
                    .join('\n\n');
            },
            conditionTagGroups() {
                const groups = new Map();
                for (const tag of this.conditionTags) {
                    const category = tag.category || 'Khác';
                    if (!groups.has(category)) groups.set(category, []);
                    groups.get(category).push(tag);
                }
                return Array.from(groups, ([category, tags]) => ({ category, tags }));
            }
        },
        methods: {
            openTextMode() {
                this.mode = 'text';
                this.taskFilter = 'all';
            },
            updatedText(room) {
                if (!room.updatedAtUtc) return 'Chưa cập nhật';
                return new Intl.DateTimeFormat('vi-VN', {
                    timeZone,
                    day: '2-digit', month: '2-digit',
                    hour: '2-digit', minute: '2-digit', hour12: false
                }).format(new Date(room.updatedAtUtc));
            },
            statusText(status) {
                return ({ 0: 'Sạch', 1: 'Bẩn', 2: 'Đang dọn' })[status] || 'Không xác định';
            },
            dateText(key) {
                return new Intl.DateTimeFormat('vi-VN', {
                    day: 'numeric', month: 'numeric', timeZone: 'UTC'
                }).format(parseDateKey(key));
            },
            weekdayText(key) {
                return new Intl.DateTimeFormat('vi-VN', {
                    weekday: 'long', timeZone: 'UTC'
                }).format(parseDateKey(key));
            },
            taskTime(task) {
                return new Intl.DateTimeFormat('vi-VN', {
                    timeZone,
                    hour: '2-digit', minute: '2-digit', hour12: false
                }).format(new Date(task.atUtc));
            },
            moveDate(amount) {
                this.selectedDate = addDays(this.selectedDate, amount);
                this.loadSchedule();
            },
            goToday() {
                this.selectedDate = this.today;
                this.loadSchedule();
            },
            async loadSchedule() {
                if (!this.selectedDate) return;
                if (this.scheduleLoading) {
                    this.scheduleQueued = true;
                    return;
                }
                this.scheduleLoading = true;
                this.copied = false;
                try {
                    const query = new URLSearchParams({ date: this.selectedDate, days: '1' });
                    this.schedule = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/housekeeping/schedule?${query}`);
                } catch (error) {
                    this.notify(error.message || 'Không thể tải lịch công việc.', 'error');
                } finally {
                    this.scheduleLoading = false;
                    if (this.scheduleQueued) {
                        this.scheduleQueued = false;
                        setTimeout(() => this.loadSchedule(), 0);
                    }
                }
            },
            async copyScheduleText() {
                if (!this.scheduleText) return;
                try {
                    await navigator.clipboard.writeText(this.scheduleText);
                } catch {
                    const input = document.createElement('textarea');
                    input.value = this.scheduleText;
                    input.setAttribute('readonly', '');
                    input.style.position = 'fixed';
                    input.style.opacity = '0';
                    document.body.appendChild(input);
                    input.select();
                    document.execCommand('copy');
                    input.remove();
                }
                this.copied = true;
                this.notify('Đã sao chép lịch dọn phòng.', 'success');
                setTimeout(() => { this.copied = false; }, 2200);
            },
            async startTask(task) {
                let room = this.rooms.find(item => item.roomId === task.roomId);
                if (!room) {
                    await this.refreshRooms();
                    room = this.rooms.find(item => item.roomId === task.roomId);
                }
                if (room) await this.setStatus(room, 2);
            },
            async refreshRooms() {
                if (this.refreshInFlight) {
                    this.refreshQueued = true;
                    return;
                }
                this.refreshInFlight = true;
                try {
                    const rooms = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/housekeeping`);
                    this.rooms = Array.isArray(rooms) ? rooms : [];
                } catch {
                    // SSE reconnect/focus/fallback poll will retry. Keep the last known board.
                } finally {
                    this.refreshInFlight = false;
                    if (this.refreshQueued) {
                        this.refreshQueued = false;
                        setTimeout(() => this.refreshRooms(), 0);
                    }
                }
            },
            async setStatus(room, status) {
                if (!this.canManage) return;
                this.savingId = room.roomId;
                try {
                    const updated = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/housekeeping/rooms/${room.roomId}/status`,
                        { status });
                    const index = this.rooms.findIndex(x => x.roomId === updated.roomId);
                    if (index >= 0) this.rooms.splice(index, 1, updated);
                    this.notify(`${updated.roomName}: ${this.statusText(updated.status)}.`, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể cập nhật trạng thái phòng.', 'error');
                } finally {
                    this.savingId = null;
                }
            },
            openReportForm(roomId) {
                this.reportForm = emptyReportForm();
                this.reportForm.open = true;
                this.reportForm.roomId = roomId || '';
            },
            closeReportForm() {
                if (this.reportForm.saving) return;
                for (const photo of this.reportForm.photos) URL.revokeObjectURL(photo.previewUrl);
                this.reportForm = emptyReportForm();
            },
            async addReportPhotos(event) {
                const input = event.target;
                const available = Math.max(0, 12 - this.reportForm.photos.length);
                const files = Array.from(input.files || []).slice(0, available);
                input.value = '';
                if (!files.length) {
                    if (available === 0) this.notify('Mỗi báo cáo tối đa 12 ảnh.', 'error');
                    return;
                }
                for (const source of files) {
                    const photo = { key: `${Date.now()}-${Math.random()}`, file: source, previewUrl: URL.createObjectURL(source), optimizing: true };
                    this.reportForm.photos.push(photo);
                    try {
                        const optimized = await optimizePhoto(source);
                        if (optimized !== source) {
                            URL.revokeObjectURL(photo.previewUrl);
                            photo.file = optimized;
                            photo.previewUrl = URL.createObjectURL(optimized);
                        }
                    } catch {
                        // Server vẫn xác thực và tối ưu lại; giữ ảnh gốc nếu trình duyệt không giải mã được.
                    } finally {
                        photo.optimizing = false;
                    }
                }
            },
            removeReportPhoto(index) {
                const [removed] = this.reportForm.photos.splice(index, 1);
                if (removed) URL.revokeObjectURL(removed.previewUrl);
            },
            toggleReportTag(name) {
                const index = this.reportForm.tags.indexOf(name);
                if (index >= 0) this.reportForm.tags.splice(index, 1);
                else this.reportForm.tags.push(name);
            },
            async submitConditionReport() {
                if (!this.reportForm.roomId) return this.notify('Vui lòng chọn phòng.', 'error');
                if (!this.reportForm.photos.length) return this.notify('Vui lòng chụp hoặc chọn ít nhất một ảnh.', 'error');
                if (!this.reportForm.content && !this.reportForm.tags.length) return this.notify('Hãy nhập nội dung hoặc chọn nội dung mẫu.', 'error');
                this.reportForm.saving = true;
                try {
                    const form = new FormData();
                    form.append('roomId', this.reportForm.roomId);
                    form.append('inspectionType', this.reportForm.inspectionType);
                    form.append('severity', this.reportForm.severity);
                    form.append('content', this.reportForm.content);
                    for (const tag of this.reportForm.tags) form.append('tags', tag);
                    for (const photo of this.reportForm.photos) form.append('files', photo.file, photo.file.name);
                    const report = await DeLongApi.postForm(`/api/admin/properties/${this.propertyId}/housekeeping/reports`, form);
                    this.conditionReports.unshift(report);
                    this.expandedReportId = report.id;
                    this.reportForm.saving = false;
                    this.closeReportForm();
                    this.notify('Đã lưu báo cáo tình trạng phòng.', 'success');
                } catch (error) {
                    this.reportForm.saving = false;
                    this.notify(error.message || 'Không thể gửi báo cáo.', 'error');
                }
            },
            toggleReport(reportId) {
                this.expandedReportId = this.expandedReportId === reportId ? null : reportId;
            },
            async changeReportStatus(report, status) {
                try {
                    const updated = await DeLongApi.post(`/api/admin/properties/${this.propertyId}/housekeeping/reports/${report.id}/status`, { status });
                    const index = this.conditionReports.findIndex(x => x.id === report.id);
                    if (index >= 0) this.conditionReports.splice(index, 1, updated);
                    this.notify('Đã cập nhật trạng thái báo cáo.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể cập nhật báo cáo.', 'error');
                }
            },
            inspectionTypeText(value) { return ({ 0: 'Trước nhận phòng', 1: 'Sau trả phòng', 2: 'Kiểm tra định kỳ', 3: 'Báo sự cố' })[value] || 'Kiểm tra phòng'; },
            severityText(value) { return ({ 0: 'Bình thường', 1: 'Cần theo dõi', 2: 'Khẩn cấp' })[value] || 'Bình thường'; },
            severityClass(value) { return `severity-${value}`; },
            reportStatusText(value) { return ({ 0: 'Mới báo', 1: 'Đang xử lý', 2: 'Đã hoàn thành' })[value] || 'Mới báo'; },
            reportDateText(value) {
                return new Intl.DateTimeFormat('vi-VN', { timeZone, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(value));
            },
            formatBytes(value) {
                if (value < 1024 * 1024) return `${Math.max(1, Math.round(value / 1024))} KB`;
                return `${(value / 1024 / 1024).toFixed(1)} MB`;
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3000);
                this.toast = { show: true, message, type, timer };
            }
        }
    }).mount(root);

    document.addEventListener('delong:operations-change', event => {
        const detail = event.detail || {};
        if (detail.propertyId && detail.propertyId !== vm.propertyId) return;
        const type = String(detail.type || '');
        if (type === 'stream.reconnected' || type === 'housekeeping.changed' || type.startsWith('booking.')) {
            vm.refreshRooms();
            vm.loadSchedule();
        }
    });
    window.addEventListener('focus', () => { vm.refreshRooms(); vm.loadSchedule(); });
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) { vm.refreshRooms(); vm.loadSchedule(); }
    });

    const fallbackPoll = setInterval(() => {
        if (!document.hidden) { vm.refreshRooms(); vm.loadSchedule(); }
    }, 15000);
    window.addEventListener('beforeunload', () => clearInterval(fallbackPoll), { once: true });
})();
