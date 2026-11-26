const $ = (selector, scope = document) => scope.querySelector(selector);
const $$ = (selector, scope = document) => [...scope.querySelectorAll(selector)];
const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)');
const goTo = (element) => element.scrollIntoView({ behavior: reduceMotion.matches ? 'instant' : 'smooth', block: 'start' });

const menuToggle = $('.menu-toggle');
const mainNav = $('#main-navigation');
function closeMenu() { menuToggle.setAttribute('aria-expanded', 'false'); mainNav.classList.remove('is-open'); }
menuToggle.addEventListener('click', () => { const open = menuToggle.getAttribute('aria-expanded') !== 'true'; menuToggle.setAttribute('aria-expanded', String(open)); mainNav.classList.toggle('is-open', open); });
$$('a', mainNav).forEach(link => link.addEventListener('click', closeMenu));
document.addEventListener('click', event => { if (!event.target.closest('.site-header')) closeMenu(); });
document.addEventListener('keydown', event => { if (event.key === 'Escape' && mainNav.classList.contains('is-open')) { closeMenu(); menuToggle.focus(); } });

const tabs = $$('[role="tab"]');
function activateTab(name, focus = false) {
  tabs.forEach(tab => { const active = tab.dataset.tab === name; tab.setAttribute('aria-selected', String(active)); tab.tabIndex = active ? 0 : -1; $(`#panel-${tab.dataset.tab}`).hidden = !active; if (active && focus) tab.focus(); });
}
tabs.forEach((tab, index) => {
  tab.addEventListener('click', () => activateTab(tab.dataset.tab));
  tab.addEventListener('keydown', event => { let next; if (event.key === 'ArrowRight') next = (index + 1) % tabs.length; if (event.key === 'ArrowLeft') next = (index - 1 + tabs.length) % tabs.length; if (event.key === 'Home') next = 0; if (event.key === 'End') next = tabs.length - 1; if (next !== undefined) { event.preventDefault(); activateTab(tabs[next].dataset.tab, true); } });
});
$$('[data-open-tab]').forEach(button => button.addEventListener('click', () => activateTab(button.dataset.openTab, true)));
$$('[data-demo]').forEach(button => button.addEventListener('click', () => { activateTab(button.dataset.demo); goTo($('#demonstracao')); $('#demonstracao').setAttribute('tabindex', '-1'); $('#demonstracao').focus({ preventScroll: true }); }));

// Todos os nomes, mensagens e atividades desta demonstração são exemplos.
const activities = [
  { day: 0, row: 2, span: 1, title: 'Português', group: '7.º A', time: '09:00–10:00', tone: 'sand', description: 'Aula de Português: leitura e escrita de um texto narrativo.' },
  { day: 0, row: 4, span: 1, title: 'Matemática', group: '8.º B', time: '11:00–12:00', tone: 'blue', description: 'Aula de Matemática com trabalho em pequenos grupos.' },
  { day: 0, row: 7, span: 1, title: 'Conselho de turma', group: '8.º ano', time: '14:00–15:00', tone: 'sage', description: 'Reunião de acompanhamento do trabalho da turma.' },
  { day: 1, row: 2, span: 1, title: 'Ciências Naturais', group: '7.º A', time: '09:00–10:00', tone: 'sand', description: 'Atividade prática de Ciências Naturais.' },
  { day: 1, row: 4, span: 1, title: 'Português', group: '7.º B', time: '11:00–12:00', tone: 'sand', description: 'Leitura orientada e preparação de materiais para a aula.' },
  { day: 2, row: 2, span: 1, title: 'História', group: '8.º A', time: '09:00–10:00', tone: 'blue', description: 'Análise de fontes e discussão em turma.' },
  { day: 2, row: 7, span: 1, title: 'Departamento', group: 'Línguas', time: '14:00–15:00', tone: 'sage', description: 'Reunião do departamento de Línguas para planificar atividades.' },
  { day: 3, row: 2, span: 1, title: 'Matemática', group: '7.º A', time: '09:00–10:00', tone: 'sand', description: 'Resolução de problemas e partilha das estratégias da turma.' },
  { day: 3, row: 4, span: 1, title: 'Ciências Naturais', group: '7.º B', time: '11:00–12:00', tone: 'plum', description: 'Aula de exploração e consolidação das aprendizagens.' },
  { day: 4, row: 2, span: 3, title: 'Visita de estudo', group: 'Museu da Ciência · 8.º ano', time: '09:00–12:00', tone: 'sage', description: 'Visita de estudo organizada para as turmas do 8.º ano.' }
];
let weekOffset = 0;
let calendarView = 'week';
const weekNames = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex'];
const monthFormatter = new Intl.DateTimeFormat('pt-PT', { month: 'short', timeZone: 'UTC' });
function weekDates() { return weekNames.map((_, day) => new Date(Date.UTC(2026, 10, 16 + weekOffset * 7 + day, 12))); }
function activityButton(activity, index, agenda = false) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = agenda ? 'agenda-item' : `calendar-event tone-${activity.tone}`;
  button.setAttribute('aria-label', `${weekNames[activity.day]}, ${activity.title}, ${activity.group}, ${activity.time}. Ver atividade.`);
  if (agenda) {
    const date = weekDates()[activity.day];
    button.innerHTML = `<span class="agenda-date">${weekNames[activity.day]}<strong>${date.getUTCDate()} ${monthFormatter.format(date)}</strong></span><span class="agenda-event tone-${activity.tone}"><strong>${activity.title}</strong><small>${activity.group}</small></span><span class="agenda-time">${activity.time}</span>`;
  } else {
    button.style.gridColumn = activity.day + 2;
    button.style.gridRow = `${activity.row} / span ${activity.span}`;
    button.innerHTML = `<strong>${activity.title}</strong><small>${activity.group}</small>`;
  }
  button.addEventListener('click', () => {
    $('#event-title').textContent = `${activity.title} · ${activity.group}`;
    $('#event-description').textContent = `${activity.time}. ${activity.description}`;
    $('#event-detail').hidden = false;
    $$('.calendar-event').forEach(item => item.removeAttribute('aria-pressed'));
    button.setAttribute('aria-pressed', 'true');
    $('#close-event').focus({ preventScroll: true });
  });
  button.dataset.activity = String(index);
  return button;
}
function renderCalendar() {
  const dates = weekDates();
  const first = dates[0], last = dates[4];
  const sameMonth = first.getUTCMonth() === last.getUTCMonth();
  $('.week-label').textContent = `${first.getUTCDate()}${sameMonth ? '' : ` ${monthFormatter.format(first)}`}–${last.getUTCDate()} ${monthFormatter.format(last)} ${last.getUTCFullYear()}`;
  const grid = $('#calendar-grid'); grid.replaceChildren();
  dates.forEach((date, day) => { const cell = document.createElement('div'); cell.className = 'calendar-day'; cell.style.gridColumn = day + 2; cell.style.gridRow = 1; cell.innerHTML = `${weekNames[day]}<span>${date.getUTCDate()}</span>`; grid.append(cell); });
  ['09:00','10:00','11:00','12:00','13:00','14:00'].forEach((hour, index) => {
    const time = document.createElement('span'); time.className = 'calendar-hour'; time.style.gridColumn = 1; time.style.gridRow = index + 2; time.textContent = hour; grid.append(time);
    for (let day = 0; day < 5; day++) { const cell = document.createElement('span'); cell.className = 'calendar-cell'; cell.style.gridColumn = day + 2; cell.style.gridRow = index + 2; cell.setAttribute('aria-hidden', 'true'); grid.append(cell); }
  });
  activities.forEach((activity, index) => grid.append(activityButton(activity, index)));
  const agenda = $('#agenda-list'); agenda.replaceChildren();
  const shown = calendarView === 'day' ? activities.filter(item => item.day === 0) : activities.filter((item, index) => activities.findIndex(other => other.day === item.day) === index);
  shown.forEach(activity => agenda.append(activityButton(activity, activities.indexOf(activity), true)));
  $('#event-detail').hidden = true;
}
$$('[data-week]').forEach(button => button.addEventListener('click', () => { weekOffset += Number(button.dataset.week); renderCalendar(); }));
$$('[data-view]').forEach(button => button.addEventListener('click', () => { calendarView = button.dataset.view; $('#panel-agenda').classList.toggle('is-day', calendarView === 'day'); $$('[data-view]').forEach(item => item.setAttribute('aria-pressed', String(item === button))); renderCalendar(); }));
$('#close-event').addEventListener('click', () => { $('#event-detail').hidden = true; $('.calendar-event')?.focus({ preventScroll: true }); });
renderCalendar();
$$('[data-material]').forEach(button => button.addEventListener('click', () => { $('#material-title').textContent = button.dataset.material; $('#material-detail').hidden = false; }));

const planIds = { essencial: 'essential', profissional: 'evolution', institucional: 'campus' };
$$('[data-plan]').forEach(button => button.addEventListener('click', () => { const destination = new URL('app/', document.baseURI); destination.searchParams.set('view', 'aderir'); destination.searchParams.set('plan', planIds[button.dataset.plan]); location.assign(destination); }));

const privacyDialog = $('#privacy-dialog');
$('#privacy-open').addEventListener('click', () => privacyDialog.showModal());
$('#privacy-close').addEventListener('click', () => privacyDialog.close());
privacyDialog.addEventListener('click', event => { if (event.target === privacyDialog) { const box = privacyDialog.getBoundingClientRect(); if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) privacyDialog.close(); } });

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
