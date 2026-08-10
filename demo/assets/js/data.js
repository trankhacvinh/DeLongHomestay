export const DEMO_VERSION = '1.0.0';
export const STORAGE_KEY = 'delong_homestay_demo_v1';

export const seedData = {
  meta: {
    version: DEMO_VERSION,
    seededAt: '2026-08-10T14:20:00',
    source: 'De Long Homestay Excel migration demo'
  },
  settings: {
    property: {
      id: 'P00003',
      name: 'De Long Homestay',
      publicName: 'De Long Retreat Homestay',
      address: 'Hẻm 39, Nguyễn Đình Chiểu, khu Phước Hải, Long Thành, Đồng Nai',
      phone: '0352291921',
      fanpage: 'De Long Retreat Homestay',
      checkInNote: 'Khung giờ là preset. Nhân viên có thể chỉnh giờ thực tế theo thỏa thuận với khách.'
    },
    sources: ['Facebook', 'Zalo', 'Người thân', 'Khách cũ', 'Walk-in'],
    roomMethods: ['Theo khung', 'Qua đêm', 'Combo', 'Cả ngày'],
    expenseCategories: [
      'Chi phí thuê nhà', 'Chi phí quản lý', 'Chi phí ăn uống/cafe', 'Chi phí % Sale',
      'Chi phí điện', 'Chi phí nước', 'Chi phí Marketing', 'Chi phí lương',
      'Chi phí game/bài couple', 'Chi phí sửa chữa duy tu', 'Quà/đối ngoại',
      'Vệ sinh máy lạnh', 'Tài khoản MXH/Wifi', 'Đồ decor/đồ khác',
      'Đồ tiêu hao', 'Công thợ', 'Vật tư thi công', 'Nội thất gỗ', 'Chi phí khác'
    ],
    cleaningStaff: ['Cô Thúy'],
    bookingColors: {
      booking: '#eac48f',
      debt_or_repair: '#ff1900',
      multi_slot: '#fba52d',
      family_friend: '#50f83a',
      extra_note: '#d53dff',
      flexible: '#fff700',
      pass_room: '#2beffd',
      mixed: '#292929'
    }
  },
  rooms: [
    {
      id: 'R001', code: 'COCO-1', name: 'CoCo Blue #1', capacity: 2, beds: 1,
      hasBathtub: true, status: 'clean', image: 'assets/images/coco-blue.svg',
      amenities: ['Bồn tắm', 'Wifi', 'Điều hòa', 'Smart TV', 'Nước nóng'],
      description: 'Tông xanh dịu, phù hợp cặp đôi và khách cần không gian thư giãn riêng tư.',
      slots: [
        { id: 'R001-S1', start: '10:30', end: '13:30', price: 250000, label: 'Khung 1' },
        { id: 'R001-S2', start: '14:00', end: '17:00', price: 250000, label: 'Khung 2' },
        { id: 'R001-S3', start: '17:30', end: '20:30', price: 250000, label: 'Khung 3' },
        { id: 'R001-S4', start: '21:00', end: '09:30', price: 360000, label: 'Qua đêm' }
      ]
    },
    {
      id: 'R002', code: 'ABAUS-2', name: 'Abaus #2', capacity: 2, beds: 1,
      hasBathtub: false, status: 'dirty', image: 'assets/images/abaus.svg',
      amenities: ['Wifi', 'Điều hòa', 'Smart TV', 'Nước nóng'],
      description: 'Phòng gọn gàng, mức giá dễ tiếp cận và phù hợp cho các khung nghỉ ngắn.',
      slots: [
        { id: 'R002-S1', start: '11:00', end: '14:00', price: 210000, label: 'Khung 1' },
        { id: 'R002-S2', start: '14:30', end: '17:30', price: 210000, label: 'Khung 2' },
        { id: 'R002-S3', start: '18:00', end: '21:00', price: 210000, label: 'Khung 3' },
        { id: 'R002-S4', start: '21:30', end: '10:00', price: 330000, label: 'Qua đêm' }
      ]
    },
    {
      id: 'R003', code: 'HK-3', name: 'Hongkong #3', capacity: 2, beds: 1,
      hasBathtub: false, status: 'clean', image: 'assets/images/hongkong.svg',
      amenities: ['Wifi', 'Điều hòa', 'Smart TV', 'Nước nóng'],
      description: 'Không gian ấm, cá tính và riêng tư, phù hợp khách thích phong cách hiện đại.',
      slots: [
        { id: 'R003-S1', start: '11:00', end: '14:00', price: 250000, label: 'Khung 1' },
        { id: 'R003-S2', start: '14:30', end: '17:30', price: 250000, label: 'Khung 2' },
        { id: 'R003-S3', start: '18:00', end: '21:00', price: 250000, label: 'Khung 3' },
        { id: 'R003-S4', start: '21:30', end: '10:00', price: 360000, label: 'Qua đêm' }
      ]
    },
    {
      id: 'R004', code: 'MOON-4', name: 'Moon Stone #4', capacity: 2, beds: 1,
      hasBathtub: true, status: 'cleaning', image: 'assets/images/moon-stone.svg',
      amenities: ['Bồn tắm', 'Wifi', 'Điều hòa', 'Smart TV', 'Nước nóng'],
      description: 'Phòng có bồn tắm, tông màu mát và không gian thư giãn cho cặp đôi.',
      slots: [
        { id: 'R004-S1', start: '11:30', end: '14:30', price: 270000, label: 'Khung 1' },
        { id: 'R004-S2', start: '15:00', end: '18:00', price: 270000, label: 'Khung 2' },
        { id: 'R004-S3', start: '18:30', end: '21:30', price: 270000, label: 'Khung 3' },
        { id: 'R004-S4', start: '22:00', end: '10:30', price: 390000, label: 'Qua đêm' }
      ]
    },
    {
      id: 'R005', code: 'AMBER-5', name: 'Amber Stay #5', capacity: 2, beds: 1,
      hasBathtub: true, status: 'clean', image: 'assets/images/amber-stay.svg',
      amenities: ['Bồn tắm', 'Wifi', 'Điều hòa', 'Smart TV', 'Nước nóng'],
      description: 'Tông màu ấm, trang trí boutique, mức giá cao hơn cho trải nghiệm thư giãn.',
      slots: [
        { id: 'R005-S1', start: '12:00', end: '15:00', price: 300000, label: 'Khung 1' },
        { id: 'R005-S2', start: '15:30', end: '18:30', price: 300000, label: 'Khung 2' },
        { id: 'R005-S3', start: '19:00', end: '22:00', price: 300000, label: 'Khung 3' },
        { id: 'R005-S4', start: '22:30', end: '11:00', price: 439000, label: 'Qua đêm' }
      ]
    },
    {
      id: 'R006', code: 'ROMAN-6', name: 'La Roman #6', capacity: 2, beds: 1,
      hasBathtub: true, status: 'clean', image: 'assets/images/la-roman.svg',
      amenities: ['Bồn tắm', 'Wifi', 'Điều hòa', 'Smart TV', 'Nước nóng'],
      description: 'Phong cách lãng mạn, có bồn tắm và phù hợp cả khung ngắn lẫn qua đêm.',
      slots: [
        { id: 'R006-S1', start: '12:00', end: '15:00', price: 270000, label: 'Khung 1' },
        { id: 'R006-S2', start: '15:30', end: '18:30', price: 270000, label: 'Khung 2' },
        { id: 'R006-S3', start: '19:00', end: '22:00', price: 270000, label: 'Khung 3' },
        { id: 'R006-S4', start: '22:30', end: '11:00', price: 390000, label: 'Qua đêm' }
      ]
    }
  ],
  customers: [
    { id: 'KHfd4a89ce', name: 'Dương', phone: '0935527193', citizenId: '', addedAt: '2026-03-01T09:00:00' },
    { id: 'C002', name: 'Nguyễn Minh', phone: '0909123456', citizenId: '', addedAt: '2026-08-02T10:00:00' },
    { id: 'C003', name: 'Trần Mai', phone: '0988123456', citizenId: '', addedAt: '2026-08-05T10:00:00' },
    { id: 'C004', name: 'Hoàng Nam', phone: '0917123456', citizenId: '', addedAt: '2026-08-07T10:00:00' }
  ],
  bookings: [
    {
      id: 'BK26081001', roomId: 'R001', customerId: 'KHfd4a89ce', guestName: 'Dương', phone: '0935527193',
      source: 'Facebook', staff: 'Admin', method: 'Theo khung', createdAt: '2026-08-09T09:15:00',
      checkIn: '2026-08-10T14:00', checkOut: '2026-08-10T17:00', basePrice: 250000, surcharge: 0,
      total: 250000, status: 'confirmed', note: 'Khách cũ.', colorKey: 'booking'
    },
    {
      id: 'BK26081002', roomId: 'R004', customerId: 'C002', guestName: 'Nguyễn Minh', phone: '0909123456',
      source: 'Zalo', staff: 'Admin', method: 'Qua đêm', createdAt: '2026-08-09T11:30:00',
      checkIn: '2026-08-10T22:00', checkOut: '2026-08-11T10:30', basePrice: 390000, surcharge: 60000,
      total: 450000, status: 'pending', note: 'Thêm 1 gối, check-in có thể trễ 30 phút.', colorKey: 'flexible'
    },
    {
      id: 'BK26081003', roomId: 'R006', customerId: 'C003', guestName: 'Trần Mai', phone: '0988123456',
      source: 'Facebook', staff: 'Admin', method: 'Theo khung', createdAt: '2026-08-10T08:20:00',
      checkIn: '2026-08-10T15:30', checkOut: '2026-08-10T18:30', basePrice: 270000, surcharge: 0,
      total: 270000, status: 'checked-in', note: '', colorKey: 'booking'
    },
    {
      id: 'BK26081101', roomId: 'R005', customerId: 'C004', guestName: 'Hoàng Nam', phone: '0917123456',
      source: 'Người thân', staff: 'Admin', method: 'Qua đêm', createdAt: '2026-08-10T09:00:00',
      checkIn: '2026-08-11T22:30', checkOut: '2026-08-12T11:00', basePrice: 439000, surcharge: 0,
      total: 439000, status: 'confirmed', note: 'Bạn bè/người thân.', colorKey: 'family_friend'
    },
    {
      id: 'BK26081201', roomId: 'R003', customerId: 'C002', guestName: 'Nguyễn Minh', phone: '0909123456',
      source: 'Khách cũ', staff: 'Admin', method: 'Theo khung', createdAt: '2026-08-10T10:00:00',
      checkIn: '2026-08-12T15:30', checkOut: '2026-08-12T17:30', basePrice: 250000, surcharge: 0,
      total: 250000, status: 'confirmed', note: 'Linh hoạt khung giờ theo lịch Excel.', colorKey: 'flexible'
    }
  ],
  payments: [
    { id: 'PAY001', bookingId: 'BK26081001', paidAt: '2026-08-09T09:20:00', amount: 100000, method: 'Chuyển khoản', note: 'Cọc' },
    { id: 'PAY002', bookingId: 'BK26081003', paidAt: '2026-08-10T15:35:00', amount: 270000, method: 'Tiền mặt', note: 'Thanh toán đủ khi nhận phòng' },
    { id: 'PAY003', bookingId: 'BK26081101', paidAt: '2026-08-10T09:05:00', amount: 150000, method: 'Chuyển khoản', note: 'Cọc' }
  ],
  expenses: [
    { id: 'EXP001', spentAt: '2026-08-08T18:00:00', propertyId: 'P00003', category: 'Đồ tiêu hao', content: 'Nước suối, khăn giấy, đồ tắm', amount: 420000, note: '' },
    { id: 'EXP002', spentAt: '2026-08-09T15:00:00', propertyId: 'P00003', category: 'Chi phí điện', content: 'Tạm tính tiền điện', amount: 1650000, note: 'Demo' }
  ],
  housekeeping: [
    { id: 'HK001', roomId: 'R002', status: 'dirty', staff: 'Cô Thúy', updatedAt: '2026-08-10T11:10:00', note: 'Khách vừa checkout' },
    { id: 'HK002', roomId: 'R004', status: 'cleaning', staff: 'Cô Thúy', updatedAt: '2026-08-10T13:50:00', note: 'Đang thay ga' }
  ],
  activity: [
    { id: 'ACT001', at: '2026-08-10T13:50:00', text: 'Bắt đầu dọn Moon Stone #4', type: 'housekeeping' },
    { id: 'ACT002', at: '2026-08-10T09:05:00', text: 'Ghi nhận cọc 150.000đ cho BK26081101', type: 'payment' },
    { id: 'ACT003', at: '2026-08-10T08:20:00', text: 'Tạo booking cho Trần Mai - La Roman #6', type: 'booking' }
  ]
};
