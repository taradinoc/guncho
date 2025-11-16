let bufferWindowId = 1;
let generation = 0;
let windowsCreated = false;
let lastOutputWasPrompt = false;
let dotnetRef = null;
let pendingUpdate = null;

function removePrompts() {
    try {
        const el = document.getElementById('gameport');
        if (!el) {
            return;
        }

        el.querySelectorAll('.BufferLine .Style_prompt').forEach(node => {
            if (node.parentElement && node.parentElement.parentNode) {
                node.parentElement.parentNode.removeChild(node.parentElement);
            } else if (node.parentElement) {
                node.parentElement.remove();
            }
        });

        el.querySelectorAll('.BufferLine').forEach(div => {
            const text = div.textContent || '';
            if (!text.trim() && div.parentNode) {
                div.parentNode.removeChild(div);
            }
        });
    } catch (err) {
        // Ignore DOM cleanup errors
    }
}

function appendUpdate(fragment) {
    if (!pendingUpdate) {
        pendingUpdate = { type: fragment.type || 'update' };
    }

    if (fragment.message !== undefined) {
        pendingUpdate.message = fragment.message;
    }

    if (fragment.disable !== undefined) {
        pendingUpdate.disable = fragment.disable;
    }

    if (fragment.specialinput) {
        pendingUpdate.specialinput = fragment.specialinput;
    }

    if (fragment.windows) {
        pendingUpdate.windows = (pendingUpdate.windows || []).concat(fragment.windows);
    }

    if (fragment.content) {
        pendingUpdate.content = (pendingUpdate.content || []).concat(fragment.content);
    }

    if (fragment.input) {
        pendingUpdate.input = (pendingUpdate.input || []).concat(fragment.input);
    }
}

function flushUpdates(sendPassWhenEmpty = false) {
    if (pendingUpdate) {
        pendingUpdate.gen = generation++;
        window.GlkOte.update(pendingUpdate);
        pendingUpdate = null;
    } else if (sendPassWhenEmpty) {
        window.GlkOte.update({ type: 'pass' });
    }
}

function showPrompt() {
    appendUpdate({
        type: 'update',
        content: [
            {
                id: bufferWindowId,
                text: [
                    {
                        content: [{ style: 'prompt', text: '> ' }]
                    }
                ]
            }
        ]
    });
    lastOutputWasPrompt = true;
}

function ensureInputLine(partial) {
    appendUpdate({
        type: 'update',
        input: [
            {
                id: bufferWindowId,
                type: 'line',
                gen: generation,
                maxlen: 256,
                initial: partial || null
            }
        ]
    });
}

export function init(element, dotnet) {
    dotnetRef = dotnet;

    if (!window.GlkOte) {
        throw new Error('GlkOte is not loaded.');
    }

    window.GlkOte.init({
        accept: (event) => {
            if (typeof event.gen === 'number') {
                generation = event.gen + 1;
            }

            let forcePrompt = false;

            switch (event.type) {
                case 'init':
                    appendUpdate({
                        type: 'update',
                        windows: [
                            {
                                id: bufferWindowId,
                                type: 'buffer',
                                rock: 69105,
                                left: 1,
                                top: 1,
                                width: element.clientWidth || 500,
                                height: element.clientHeight || 400
                            }
                        ]
                    });
                    windowsCreated = true;
                    forcePrompt = true;
                    break;

                case 'line':
                    (function handleLineEvent() {
                        const input = event.value || '';
                        appendUpdate({
                            type: 'update',
                            content: [
                                {
                                    id: bufferWindowId,
                                    text: [
                                        {
                                            content: [
                                                { style: 'normal', text: '> ' },
                                                { style: 'input', text: input }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        });
                        lastOutputWasPrompt = false;
                        if (dotnetRef) {
                            dotnetRef.invokeMethodAsync('OnGlkLineEntered', input);
                        }
                    })();
                    break;

                default:
                    break;
            }

            if (windowsCreated && (forcePrompt || !lastOutputWasPrompt)) {
                removePrompts();
                showPrompt();
            }

            if (windowsCreated) {
                const partial = event.partial && event.partial[bufferWindowId]
                    ? event.partial[bufferWindowId]
                    : null;
                ensureInputLine(partial);
            }

            flushUpdates(true);
        }
    });
}

export function appendText(line) {
    if (!windowsCreated) {
        return;
    }

    const safeLine = line === null || line === undefined ? '' : String(line);
    appendUpdate({
        type: 'update',
        content: [
            {
                id: bufferWindowId,
                text: [
                    {
                        content: [
                            { style: 'normal', text: safeLine }
                        ]
                    }
                ]
            }
        ]
    });

    lastOutputWasPrompt = false;
    removePrompts();
    showPrompt();
    ensureInputLine();
    flushUpdates();
}

export function interrupt() {
    if (windowsCreated && window.GlkOte) {
        window.GlkOte.extevent(null);
    }
}

export function dispose() {
    dotnetRef = null;
}
