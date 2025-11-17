(function () {
    const globalScope = typeof self !== 'undefined'
        ? self
        : (typeof window !== 'undefined' ? window : undefined);

    if (!globalScope) {
        return;
    }

    const languageId = 'inform7';

    const monarchLanguage = {
        defaultToken: '',
        ignoreCase: true,
        tokenPostfix: '.inform7',
        brackets: [
            { open: '[', close: ']', token: 'delimiter.square' },
            { open: '(', close: ')', token: 'delimiter.parenthesis' },
            { open: '{', close: '}', token: 'delimiter.brace' }
        ],
        tokenizer: {
            root: [
                { include: '@whitespace' },
                [/^".*$/, 'string.title'],
                [/^\s*(?:volume|book|part|chapter|section)\b.*$/, 'keyword.heading'],
                [/^\s*Table\s+.*$/, 'keyword.table'],
                [/\[preform\]\(-/, { token: 'metatag.preform', next: '@preform' }],
                [/\(-/, { token: 'metatag.inform6', next: '@inform6' }],
                [/([\[]js[\]])(\s*)(")/, [
                    { token: 'comment.block' },
                    'white',
                    { token: 'string.quote', next: '@jsEmbedded', nextEmbedded: 'javascript' }
                ]],
                [/(Execute\s+Javascript\s+(?:command|code)\s*)(")/, [
                    { token: 'keyword' },
                    { token: 'string.quote', next: '@jsEmbedded', nextEmbedded: 'javascript' }
                ]],
                [/\[/, { token: 'comment.block', next: '@comment' }],
                [/"/, { token: 'string.quote', next: '@string' }],
                [/[-+]?(?:0[xX][0-9a-fA-F]+|\d+)(?:\.\d+)?/, 'number'],
                [/[,.;:(){}]/, 'delimiter'],
                [/<=|>=|==|<>|<|>|=|\+|-|\*|\//, 'operator'],
                [/\w+/, 'identifier']
            ],

            whitespace: [
                [/\s+/, 'white']
            ],

            comment: [
                [/\[/, { token: 'comment.block', next: '@comment' }],
                [/\]/, { token: 'comment.block', next: '@pop' }],
                [/[^\[\]]+/, 'comment.block'],
                [/./, 'comment.block']
            ],

            string: [
                [/"/, { token: 'string.quote', next: '@pop' }],
                [/\[preform\]\(-/, { token: 'metatag.preform', next: '@preform' }],
                [/\(-/, { token: 'metatag.inform6', next: '@inform6' }],
                [/\[/, { token: 'annotation', next: '@textSubstitution' }],
                [/[^"\[]+/, 'string'],
                [/./, 'string']
            ],

            textSubstitution: [
                [/\]/, { token: 'annotation', next: '@pop' }],
                [/\[/, 'invalid'],
                [/[^\]]+/, 'annotation'],
                [/./, 'annotation']
            ],

            jsEmbedded: [
                [/"/, { token: 'string.quote', next: '@pop', nextEmbedded: '@pop' }],
                [/[^"\r\n]+/, 'string'],
                [/./, 'string']
            ],

            preform: [
                [/-\)/, { token: 'metatag.preform', next: '@pop' }],
                [/[^-]+/, 'metatag.preform'],
                [/./, 'metatag.preform']
            ],

            inform6: [
                [/-\)/, { token: 'metatag.inform6', next: '@pop' }],
                [/[^-]+/, 'metatag.inform6'],
                [/./, 'metatag.inform6']
            ]
        }
    };

    const languageConfiguration = {
        comments: {
            blockComment: ['[', ']']
        },
        brackets: [
            ['[', ']'],
            ['(', ')'],
            ['{', '}']
        ],
        autoClosingPairs: [
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '{', close: '}' },
            { open: '"', close: '"', notIn: ['string'] }
        ],
        surroundingPairs: [
            { open: '"', close: '"' },
            { open: '[', close: ']' },
            { open: '(', close: ')' }
        ]
    };

    const registerLanguage = () => {
        if (typeof globalScope.monaco === 'undefined') {
            return;
        }

        const alreadyRegistered = globalScope.monaco.languages.getLanguages().some(l => l.id === languageId);
        if (!alreadyRegistered) {
            globalScope.monaco.languages.register({
                id: languageId,
                extensions: ['.ni', '.i7x'],
                aliases: ['Inform 7', 'inform7']
            });
        }

        globalScope.monaco.languages.setMonarchTokensProvider(languageId, monarchLanguage);
        globalScope.monaco.languages.setLanguageConfiguration(languageId, languageConfiguration);
    };

    const ensureRegistration = () => {
        if (globalScope.require) {
            globalScope.require(['vs/editor/editor.main'], registerLanguage);
        } else {
            if (typeof globalScope.monaco !== 'undefined') {
                registerLanguage();
            } else {
                globalScope.addEventListener('monaco_ready', registerLanguage, { once: true });
            }
        }
    };

    ensureRegistration();

    globalScope.gunchoInform7 = globalScope.gunchoInform7 || {};
    globalScope.gunchoInform7.ensureRegistration = registerLanguage;
})();
