// Object URLs avoid copying the original photographs into WASM/base64 memory.
const selections = new WeakMap();

export function clearPreviews(input) {
    for (const url of selections.get(input) || []) URL.revokeObjectURL(url);
    selections.delete(input);
}

export function createPreviews(input, limit) {
    clearPreviews(input);
    const urls = Array.from(input.files || []).slice(0, limit).map(file => URL.createObjectURL(file));
    selections.set(input, urls);
    return urls;
}
