export function scrollTranscriptToBottom(element) {
    if (!element) {
        return;
    }

    try {
        element.scrollTop = element.scrollHeight;
    } catch (error) {
        // accessing DOM can fail if the element is disposed; ignore silently
    }
}

export function focusElement(element) {
    try {
        if (element && element.focus) {
            element.focus();
        }
    } catch { }
}
