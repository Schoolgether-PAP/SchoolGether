const toggle = document.querySelector('.menu-toggle');
const nav = document.querySelector('#main-navigation');
function closeMenu() { toggle.setAttribute('aria-expanded', 'false'); nav.classList.remove('is-open'); }
toggle.addEventListener('click', () => {
  const open = toggle.getAttribute('aria-expanded') !== 'true';
  toggle.setAttribute('aria-expanded', String(open)); nav.classList.toggle('is-open', open);
});
nav.querySelectorAll('a').forEach(link => link.addEventListener('click', closeMenu));
document.addEventListener('click', event => { if (!event.target.closest('.site-header')) closeMenu(); });
document.addEventListener('keydown', event => { if (event.key === 'Escape' && nav.classList.contains('is-open')) { closeMenu(); toggle.focus(); } });
const dialog = document.querySelector('#privacy-dialog');
document.querySelector('#privacy-open').addEventListener('click', () => dialog.showModal());
document.querySelector('#privacy-close').addEventListener('click', () => dialog.close());
dialog.addEventListener('click', event => {
  if (event.target !== dialog) return;
  const box = dialog.getBoundingClientRect();
  if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) dialog.close();
});


const form = document.querySelector('#contact-form');
form?.addEventListener('submit', event => {
  event.preventDefault();
  if (!form.reportValidity()) return;
  const data = new FormData(form);
  const subject = 'Contacto SchoolGether — ' + String(data.get('school') || '').trim();
  const body = ['Nome: ' + data.get('name'), 'E-mail: ' + data.get('email'), 'Instituição: ' + data.get('school'), '', String(data.get('message') || '')].join('\r\n');
  const link = document.createElement('a');
  link.href = 'mailto:saguilearn@gmail.com?subject=' + encodeURIComponent(subject) + '&body=' + encodeURIComponent(body);
  link.click();
  document.querySelector('#contact-status').textContent = 'Mensagem preparada no seu programa de correio. Confirme aí o envio; se não abrir, escreva para saguilearn@gmail.com.';
});
