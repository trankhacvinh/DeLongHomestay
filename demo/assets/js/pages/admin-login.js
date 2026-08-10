import { qs, toast } from '../core.js';
if (sessionStorage.getItem('delong_demo_auth') === '1') location.href = 'index.html';
qs('#loginForm').addEventListener('submit', e => {
  e.preventDefault();
  const user = qs('#username').value.trim(); const pass = qs('#password').value;
  if (user === 'admin' && pass === 'demo123') { sessionStorage.setItem('delong_demo_auth', '1'); location.href = 'index.html'; }
  else toast('Tài khoản demo: admin / demo123', 'danger');
});
