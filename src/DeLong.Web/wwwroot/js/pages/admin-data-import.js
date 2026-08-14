(function () {
    const root = document.getElementById('data-import-page');
    if (!root) return;
    const initial = JSON.parse(document.getElementById('data-import-data')?.textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                propertyName: initial.propertyName,
                selectedFile: null,
                preview: null,
                imported: null,
                previewing: false,
                importing: false,
                rowFilter: 'all',
                filters: [
                    { value: 'all', label: 'Tất cả' },
                    { value: 'ready', label: 'Sẵn sàng' },
                    { value: 'duplicate', label: 'Trùng' },
                    { value: 'error', label: 'Cần sửa' }
                ],
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            templateUrl() {
                return `/api/admin/properties/${this.propertyId}/imports/bookings/template`;
            },
            filteredRows() {
                const rows = this.preview?.rows || [];
                return this.rowFilter === 'all' ? rows : rows.filter(row => row.state === this.rowFilter);
            }
        },
        methods: {
            selectFile(event) {
                const file = event.target.files?.[0] || null;
                this.selectedFile = file;
                this.preview = null;
                this.imported = null;
                this.rowFilter = 'all';
            },
            reset() {
                this.selectedFile = null;
                this.preview = null;
                this.imported = null;
                this.rowFilter = 'all';
                if (this.$refs.fileInput) this.$refs.fileInput.value = '';
            },
            createFormData() {
                const data = new FormData();
                if (this.selectedFile) data.append('file', this.selectedFile, this.selectedFile.name);
                return data;
            },
            async previewFile() {
                if (!this.selectedFile || this.previewing) return;
                this.previewing = true;
                this.imported = null;
                try {
                    this.preview = await DeLongApi.postForm(
                        `/api/admin/properties/${this.propertyId}/imports/bookings/preview`,
                        this.createFormData());
                    this.rowFilter = this.preview.errorRows > 0 ? 'error' : 'all';
                    if (this.preview.format === 'legacy-calendar') {
                        this.notify('Đã nhận diện lịch màu cũ; cần chuyển sang mẫu có tên khách và SĐT để import an toàn.', 'error');
                    } else if (this.preview.totalRows === 0) {
                        this.notify('File đúng cấu trúc nhưng chưa có dòng booking.', 'error');
                    } else if (this.preview.errorRows > 0) {
                        this.notify(`Có ${this.preview.errorRows} dòng cần sửa trước khi import.`, 'error');
                    } else {
                        this.notify('Xem trước hoàn tất. Chưa có dữ liệu nào được ghi.', 'success');
                    }
                } catch (error) {
                    this.preview = null;
                    this.notify(error.message || 'Không thể đọc file Excel.', 'error');
                } finally {
                    this.previewing = false;
                }
            },
            async commitImport() {
                if (!this.selectedFile || !this.preview || this.preview.errorRows > 0 || this.preview.readyRows === 0 || this.importing) return;
                if (!window.confirm(`Import ${this.preview.readyRows} lượt đặt vào ${this.propertyName}? Dòng trùng chính xác sẽ được bỏ qua.`)) return;
                this.importing = true;
                try {
                    this.imported = await DeLongApi.postForm(
                        `/api/admin/properties/${this.propertyId}/imports/bookings/commit`,
                        this.createFormData());
                    this.notify(`Đã import ${this.imported.importedRows} lượt đặt.`, 'success');
                    await this.previewFile();
                } catch (error) {
                    this.notify(error.message || 'Không thể import dữ liệu.', 'error');
                } finally {
                    this.importing = false;
                }
            },
            formatBytes(value) {
                const bytes = Number(value || 0);
                if (bytes < 1024) return `${bytes} B`;
                if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
                return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
            },
            stateLabel(state) {
                return ({ ready: 'Sẵn sàng', duplicate: 'Trùng · bỏ qua', error: 'Cần sửa' })[state] || state;
            },
            formatDateTime(value) {
                if (!value) return 'Không đọc được';
                const date = new Date(value);
                if (Number.isNaN(date.getTime())) return 'Không đọc được';
                return new Intl.DateTimeFormat('vi-VN', {
                    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
                    timeZone: 'Asia/Ho_Chi_Minh'
                }).format(date);
            },
            money(value) {
                return `${new Intl.NumberFormat('vi-VN').format(Number(value || 0))} đ`;
            },
            notify(message, type) {
                if (this.toast.timer) window.clearTimeout(this.toast.timer);
                this.toast.show = true;
                this.toast.message = message;
                this.toast.type = type || 'success';
                this.toast.timer = window.setTimeout(() => { this.toast.show = false; }, 5000);
            }
        }
    }).mount(root);
})();
