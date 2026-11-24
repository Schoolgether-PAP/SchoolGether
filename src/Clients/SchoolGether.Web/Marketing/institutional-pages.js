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
document.querySelector('#contact-form')?.addEventListener('submit', event => {
  event.preventDefault();
  document.querySelector('#contact-status').textContent = 'Mensagem validada na pré-visualização. Nada foi enviado. Para contactar a equipa, utilize o botão «Enviar e-mail à equipa».';
});
