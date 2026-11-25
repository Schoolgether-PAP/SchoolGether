// Shared by the public pages and the application. Contains presentation preferences only.
(() => {
  const root = document.documentElement;
  const key = 'saguilearn.appearance';
  const system = matchMedia('(prefers-color-scheme: dark)');
  let theme = 'system', collapsed = false;
  try {
    const saved = JSON.parse(localStorage.getItem(key) || '{}');
    if (['light', 'dark', 'system'].includes(saved.theme)) theme = saved.theme;
    collapsed = saved.collapsed === true;
  } catch { /* Browser storage can be unavailable. Preferences still work for this page. */ }
  function apply() {
    root.dataset.theme = theme === 'system' ? (system.matches ? 'dark' : 'light') : theme;
    root.dataset.sidebar = collapsed ? 'collapsed' : 'expanded';
    root.style.colorScheme = root.dataset.theme;
  }
  function save() {
    apply();
    try { localStorage.setItem(key, JSON.stringify({theme, collapsed})); } catch { }
  }
  window.saguiPreferences = {
    get: () => ({theme, collapsed}),
    setTheme(value) { if (['light', 'dark', 'system'].includes(value)) { theme = value; save(); } },
    toggleSidebar() { collapsed = !collapsed; save(); return collapsed; }
  };
  system.addEventListener('change', apply);
  addEventListener('storage', e => {
    if (e.key !== key) return;
    try {
      const saved = JSON.parse(e.newValue || '{}');
      theme = ['light', 'dark', 'system'].includes(saved.theme) ? saved.theme : 'system';
      collapsed = saved.collapsed === true;
      apply();
    } catch { }
  });
  apply();
  document.addEventListener('DOMContentLoaded', () => {
    if (!document.body.classList.contains('company-site')) return;
    const nav = document.querySelector('.main-nav');
    if (!nav) return;
    const label = document.createElement('label');
    label.className = 'appearance-control';
    label.append(document.createTextNode('Aspeto '));
    const select = document.createElement('select');
    select.setAttribute('aria-label', 'Aspeto do site');
    for (const [value, text] of [['system','Automático'],['light','Claro'],['dark','Escuro']]) {
      const option = document.createElement('option'); option.value = value; option.textContent = text;
      select.append(option);
    }
    select.value = theme;
    select.addEventListener('change', () => window.saguiPreferences.setTheme(select.value));
    label.append(select); nav.append(label);
  });
})();
