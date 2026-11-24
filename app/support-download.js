export async function download(name, type, stream) {
    const bytes = await stream.arrayBuffer();
    const url = URL.createObjectURL(new Blob([bytes], {type: type || 'application/octet-stream'}));
    const link = document.createElement('a'); link.href = url; link.download = name; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
