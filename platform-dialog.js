const owners = new WeakMap();
export function open(dialog, callback) {
    const previous = document.activeElement;
    owners.set(dialog, previous);
    dialog.addEventListener('cancel', event => { event.preventDefault(); callback.invokeMethodAsync('CancelAsync'); });
    dialog.showModal();
}
export function close(dialog) {
    dialog.close();
    const previous = owners.get(dialog);
    if (previous?.isConnected) previous.focus();
    owners.delete(dialog);
}
