export async function downloadCanvasImage(canvas: HTMLCanvasElement, filename: string): Promise<void> {
  const canvasEl = canvas as HTMLCanvasElement & {
    toBlob?: (callback: BlobCallback, type?: string, quality?: any) => void;
  };
  const saveBlob = (blob: Blob) =>
    new Promise<void>((resolve, reject) => {
      try {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = filename;
        anchor.click();
        URL.revokeObjectURL(url);
        resolve();
      } catch (error) {
        reject(error);
      }
    });

  if (typeof canvasEl.toBlob === 'function') {
    const blob = await new Promise<Blob>((resolve, reject) => {
      canvasEl.toBlob!((result) => {
        if (result) resolve(result);
        else reject(new Error('Canvas capture failed.'));
      });
    });
    await saveBlob(blob);
    return;
  }

  const dataUrl = canvasEl.toDataURL('image/png');
  const byteString = atob(dataUrl.split(',')[1] ?? '');
  const mimeString = dataUrl.split(',')[0]?.split(':')[1]?.split(';')[0] ?? 'image/png';
  const buffer = new ArrayBuffer(byteString.length);
  const view = new Uint8Array(buffer);
  for (let i = 0; i < byteString.length; i += 1) {
    view[i] = byteString.charCodeAt(i);
  }
  await saveBlob(new Blob([buffer], { type: mimeString }));
}
