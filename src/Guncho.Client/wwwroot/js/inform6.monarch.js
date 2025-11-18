(function () {
    const globalScope = typeof self !== 'undefined'
        ? self
        : (typeof window !== 'undefined' ? window : undefined);

    if (!globalScope) {
        return;
    }

    const list = (items) => items
        .split('|')
        .map((item) => item.trim().toLowerCase())
        .filter(Boolean);

    const languageId = 'inform6';

    const controlKeywords = list('box|break|continue|do|else|for|give|if|inversion|jump|move|new_line|objectloop|print|print_ret|quit|remove|return|rfalse|rtrue|spaces|string|switch|until|while');
    const directiveKeywords = list('abbreviate|array|attribute|constant|default|dictionary|end|endif|extend|global|ifdef|iffalse|ifndef|ifnot|iftrue|import|include|link|message|messageerror|messagefatalerror|messagewarning|origsource|property|property_additive|release|replace|serial|statusline|statusline_time|statusline_score|stub|switches|system_file|undef|verb|verb_meta|zcharacter|zcharacter_table');
    const deprecatedDirectives = list('fake_action|ifv3|ifv5|lowstring|nearby|trace');
    const otherKeywords = list('alias|first|last|only');
    const operatorKeywords = list('class|has|hasnt|in|notin|ofclass|or|private|provides|with');
    const supportClasses = list('class|compassdirection|object|routine|string');

    const supportFunctions = list('Achieved|AddToScope|AfterRoutines|AllowPushDir|Banner|call|Cap|Centre|ChangePlayer|child|children|CommonAncestor|copy|create|destroy|DictionaryLookup|GetGNAOfObject|HasLightSource|IndirectlyContains|IsSeeThrough|Length|Locale|LoopOverScope|LowerCase|LTI_Insert|metaclass|MoveFloatingObjects|NextEntry|NextWord|NextWordStopped|NounDomain|ObjectIsUntouchable|OffersLight|parent|ParseToken|PlaceInScope|PlayerTo|print_to_array|PrintCapitalised|PrintOrRun|PrintOrRunVal|PrintToBuffer|PronounNotice|PronounValue|random|recreate|remaining|ScopeWithin|SetColour|SetPronoun|SetTime|sibling|StartDaemon|StartTimer|StopDaemon|StopTimer|TestScope|TryNumber|UnsignedCompare|UpperCase|WordAddress|WordInProperty|WordLength|WriteListFrom|YesOrNo');
    const supportFunctionsGlulx = list('ClearScreen|DecimalNumber|DrawStatusLine|glk|KeyCharPrimitive|KeyDelay|MainWindow|MoveCursor|PrintAnything|PrintAnyToArray|ScreenHeight|ScreenWidth|StatusLineHeight');
    const propertyNames = list('add_to_scope|after|article|articles|before|before_implicit|cant_go|capacity|compass_look|d_to|daemon|describe|description|door_dir|door_to|e_to|each_turn|ext_initialise|ext_messages|found_in|grammar|in_to|initial|inside_description|invent|life|list_together|n_to|name|ne_to|number|nw_to|orders|out_to|parse_name|plural|react_after|react_before|s_to|se_to|short_name|short_name_indef|sw_to|time_left|time_out|u_to|w_to|when_closed|when_off|when_on|when_open|with_key');
    const supportConstants = list('library_english|library_grammar|library_parser|library_verblib|library_version|always_bit|conceal_bit|defart_bit|english_bit|fullinv_bit|indent_bit|isare_bit|newline_bit|noarticle_bit|partinv_bit|recurse_bit|terse_bit|workflag_bit|gpr_fail|gpr_multiple|gpr_number|gpr_preposition|gpr_reparse|creature_token|held_token|multi_token|multiexcept_token|multiheld_token|multiinside_token|number_token|noun_token|elementary_tt|scope_tt|reparse_code|eachturn_reason|loopoverscope_reason|parsing_reason|react_after_reason|react_before_reason|talking_reason|testscope_reason|anima_pe|askscope_pe|cantsee_pe|except_pe|itgone_pe|junkafter_pe|mmulti_pe|multi_pe|notheld_pe|nothing_pe|number_pe|scenery_pe|stuck_pe|toofew_pe|toolit_pe|upto_pe|vague_pe|verb_pe|clr_azure|clr_black|clr_blue|clr_cyan|clr_default|clr_green|clr_magenta|clr_purple|clr_red|clr_white|clr_yellow|win_all|win_main|win_status|target_glulx|target_zcode|wordsize|float_infinity|float_ninfinity|float_nan|gg_mainwin_rock|gg_quotewin_rock|gg_statuswin_rock');
    const supportVariables = list('c_style|compass|d_obj|deadflag|e_obj|etype|in_obj|keep_silent|multiple_object|n_obj|ne_obj|notify_mode|nw_obj|out_obj|parsed_number|parser_action|parser_inflection|parser_one|parser_two|s_obj|score|se_obj|selfobj|sys_statusline_flag|take_flag|thedark|turns|u_obj|verb_word|verb_wordnum|w_obj|wn|gg_arguments|gg_statuswin_cursize|gg_statuswin_size|fullscore|lmode1|lmode2|lmode3|miscellany|notifyoff|notifyon|objects|places|pluralfound|prompt|pronouns|quit|restart|restore|save|score|scriptoff|scripton|thesame|verify|version|close|disrobe|drop|eat|empty|emptyt|enter|examine|exit|getoff|go|goin|insert|inv|invtall|invwide|letgo|lock|look|open|puton|receive|remove|search|switchoff|switchon|take|transfer|unlock|wait|wear|answer|ask|askfor|askto|attack|blow|burn|buy|climb|consult|cut|dig|drink|fill|give|jump|jumpin|jumpon|jumpover|kiss|listen|lookup|mild|no|notunderstood|order|pray|pull|push|pushdir|rub|set|setto|show|sing|sleep|smell|sorry|squeeze|strong|swim|swing|taste|tell|think|throwat|thrownat|tie|touch|turn|vaguego|wake|wakeother|wave|wavehands|yes|absent|animate|clothing|concealed|container|door|edible|enterable|female|general|light|lockable|locked|male|moved|neuter|on|open|openable|pluralname|proper|scenery|scored|static|supporter|switchable|talkable|transparent|visited|workflag|worn|creature|held|multi|multiexcept|multiheld|multiinside|noun|number|topic|action|action_to_be|actor|consult_from|consult_word|indef_mode|inp1|inp2|inventory_stage|lm_n|lm_o|location|player|real_location|scope_reason|scope_stage|second|self|standard_interpreter|the_time|vague_word|gg_event|gg_mainwin|gg_statuswin');
    const opcodeWords = list('catch|pull|push|quit|restore|save|throw|aread|buffer_mode|check_unicode|encode_text|erase_window|input_stream|loadb|output_stream|print_table|print_unicode|read_char|set_color|set_cursor|set_window|sound_effect|split_window|tokenise|accelfunc|accelparam|acos|add|aload|aloadb|aloadbit|aloads|asin|astore|astoreb|astorebit|astores|atan|atan2|binarysearch|bitand|bitnot|bitor|bitxor|call|callf|callfi|callfii|callfiii|ceil|copy|copyb|copys|cos|debugtrap|div|exp|fadd|fdiv|floor|fmod|fmul|fsub|ftonumn|ftonumz|gestalt|getiosys|getmemsize|getstringtbl|glk|jeq|jfeq|jfge|jfgt|jfle|jflt|jfne|jge|jgeu|jgt|jgtu|jisinf|jisnan|jle|jleu|jltu|jne|jlt|jnz|jump|jumpabs|jz|linearsearch|linkedsearch|log|malloc|mcopy|mfree|mod|mul|mzero|neg|nop|numtof|pow|protect|random|restart|restoreundo|return|saveundo|setiosys|setmemsize|setrandom|setstringtbl|sexb|sexs|shiftl|sin|sqrt|sshiftr|stkcopy|stkcount|stkpeek|stkroll|stkswap|streamchar|streamnum|streamstr|streamunichar|sub|tailcall|tan|ushiftr|verify');

    const monarchLanguage = {
        defaultToken: '',
        ignoreCase: true,
        tokenPostfix: '.inform6',
        brackets: [
            { open: '[', close: ']', token: 'delimiter.square' },
            { open: '(', close: ')', token: 'delimiter.parenthesis' },
            { open: '{', close: '}', token: 'delimiter.brace' }
        ],
        controlKeywords,
        directiveKeywords,
        deprecatedDirectives,
        otherKeywords,
        operatorKeywords,
        supportClasses,
        supportFunctions,
        supportFunctionsGlulx,
        propertyNames,
        supportConstants,
        supportVariables,
        opcodeWords,
        tokenizer: {
            root: [
                { include: '@whitespace' },
                [/!%.*$/, 'keyword.directive'],
                [/!.*$/, 'comment'],
                [/\bfont\s+(?:on|off)\b/, 'keyword.control'],
                [/\bstyle\s+(?:roman|bold|underline|reverse|fixed)\b/, 'keyword.control'],
                [/(#)([a-zA-Z_]\w*)/, [
                    'keyword.directive',
                    {
                        cases: {
                            '@directiveKeywords': 'keyword.directive',
                            '@deprecatedDirectives': 'invalid.deprecated',
                            '@default': 'identifier'
                        }
                    }
                ]],
                [/\b(?:VorpleExecuteJavaScriptCommand|BuildCommand)\s*\(/, { token: 'support.function', next: '@jsInvocation' }],
                [/"/, { token: 'string.quote', next: '@stringDouble' }],
                [/\'/, { token: 'string.quote', next: '@stringSingle' }],
                [/\$\$[01]+\b/, 'number.binary'],
                [/\$[0-9a-fA-F]+\b/, 'number.hex'],
                [/\$[+\-]\d+(?:\.\d+)?(?:e[+\-]\d+)?\b/, 'number.float'],
                [/[-+]?\d+\b/, 'number'],
                [/@"[SBR]*\d:\d+"/, 'support.function.opcode'],
                [/@[a-zA-Z_]\w*/, {
                    cases: {
                        '@opcodeWords': 'support.function.opcode',
                        '@default': 'operator'
                    }
                }],
                [/##[a-zA-Z]+\b/, 'variable.language'],
                [/@[0-9]{2}/, 'variable.predefined'],
                [/<=|>=|==|~=|<|>|=/, 'operator.comparison'],
                [/&&|\|\||~~/, 'operator.logical'],
                [/(-->|->)/, 'operator.pointer'],
                [/\+\+|--/, 'operator'],
                [/[-+*/%]/, 'operator.arithmetic'],
                [/&|\||~/, 'operator.bitwise'],
                [/[,;]/, 'delimiter'],
                [/\./, 'delimiter.accessor'],
                [/[:]/, 'delimiter'],
                [/\[|\]|\{|\}|\(|\)/, '@brackets'],
                [/[a-zA-Z_]\w*/, {
                    cases: {
                        '@controlKeywords': 'keyword.control',
                        '@directiveKeywords': 'keyword.directive',
                        '@deprecatedDirectives': 'invalid.deprecated',
                        '@otherKeywords': 'keyword',
                        '@operatorKeywords': 'keyword.operator',
                        '@supportClasses': 'type.identifier',
                        '@supportFunctions': 'support.function',
                        '@supportFunctionsGlulx': 'support.function',
                        '@propertyNames': 'support.variable.property',
                        '@supportConstants': 'constant.language',
                        '@supportVariables': 'variable.predefined',
                        '@default': 'identifier'
                    }
                }],
                [/_[a-zA-Z0-9]+/, 'identifier']
            ],

            whitespace: [
                [/\s+/, 'white']
            ],

            stringDouble: [
                [/"/, { token: 'string.quote', next: '@pop' }],
                [/@\{[0-9a-fA-F]{7,}\}/, 'invalid'],
                [/@\{[0-9a-fA-F]{1,6}\}/, 'string.escape'],
                [/@(?:@\d{1,3}|[\^`:][aeiouAEIOU]|:y|'[aeiouyAEIOUY]|c[cC]|~[anoANO]|\/[oO]|o[aA]|ss|oe|ae|OE|AE|th|Th|et|Et|LL|!!|\?\?|<<|>>)/, 'string.escape'],
                [/@[0-9]{2}/, 'variable.predefined'],
                [/\^/, 'string.escape'],
                [/~/, 'string.escape'],
                [/@/, 'invalid'],
                [/\\./, 'string.escape'],
                [/[^"@\\]+/, 'string'],
                [/./, 'string']
            ],

            stringSingle: [
                [/\/\/(?:p)?(?=')/, 'string.escape'],
                [/@\{[0-9a-fA-F]{7,}\}/, 'invalid'],
                [/@\{[0-9a-fA-F]{1,6}\}/, 'string.escape'],
                [/@(?:@\d{1,3}|[\^`:][aeiouAEIOU]|:y|'[aeiouyAEIOUY]|c[cC]|~[anoANO]|\/[oO]|o[aA]|ss|oe|ae|OE|AE|th|Th|et|Et|LL|!!|\?\?|<<|>>)/, 'string.escape'],
                [/@[0-9]{2}/, 'variable.predefined'],
                [/\^/, 'string.escape'],
                [/~/, 'string.escape'],
                [/@/, 'invalid'],
                [/\\./, 'string.escape'],
                [/'/, { token: 'string.quote', next: '@pop' }],
                [/[^'@\\]+/, 'string'],
                [/./, 'string']
            ],

            jsInvocation: [
                [/"/, { token: 'string.quote', next: '@jsEmbedded', nextEmbedded: 'javascript' }],
                [/\)/, { token: 'delimiter.parenthesis', next: '@pop' }],
                [/[,]/, 'delimiter'],
                [/[^\)"]+/, 'argument'],
                [/./, 'argument']
            ],

            jsEmbedded: [
                [/"/, { token: 'string.quote', next: '@pop', nextEmbedded: '@pop' }],
                [/[^"\\]+/, 'string'],
                [/\\./, 'string.escape'],
                [/./, 'string']
            ]
        }
    };

    const languageConfiguration = {
        comments: {
            lineComment: '!'
        },
        brackets: [
            ['[', ']'],
            ['(', ')'],
            ['{', '}']
        ],
        autoClosingPairs: [
            { open: '"', close: '"', notIn: ['string'] },
            { open: '\'', close: '\'', notIn: ['string'] },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '{', close: '}' }
        ],
        surroundingPairs: [
            { open: '"', close: '"' },
            { open: '\'', close: '\'' },
            { open: '[', close: ']' },
            { open: '(', close: ')' }
        ]
    };

    const registerLanguage = () => {
        if (typeof globalScope.monaco === 'undefined') {
            return;
        }

        const alreadyRegistered = globalScope.monaco.languages.getLanguages().some((l) => l.id === languageId);
        if (!alreadyRegistered) {
            globalScope.monaco.languages.register({
                id: languageId,
                extensions: ['.inf', '.i6', '.i6t', '.h'],
                aliases: ['Inform 6', 'inform6']
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

    globalScope.gunchoInform6 = globalScope.gunchoInform6 || {};
    globalScope.gunchoInform6.ensureRegistration = registerLanguage;
})();
