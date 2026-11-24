// Conservar os links anteriores sem perder parâmetros de adesão.
const requestedView = new URLSearchParams(location.search).get('view');
if (['area', 'entrar', 'aderir'].includes(requestedView)) {
  const target = new URL('app/', location.href);
  target.search = location.search;
  target.hash = location.hash;
  location.replace(target.href);
}
