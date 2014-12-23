interface GlkOte {
    version: string;
    /**
     * The document calls this to begin the game. The simplest way to do this
     * is to give the <body> tag an onLoad="GlkOte.init();" attribute.
     */
    init(iface?: GlkGame): void;
    /**
     * The game calls this to update the screen state. The argument includes
     * all the information about new windows, new text, and new input requests --
     * everything necessary to construct a new display state for the user.
     */
    update(arg: GlkUpdate): void;
    /**
     * Cause an immediate input event, of type "external". This invokes
     * Game.accept(), just like any other event.
     */
    extevent(val: any): void;
    /**
     * Return the game interface object that was provided to init(). Call
     * this if a subsidiary library (e.g., dialog.js) needs to imitate some
     * display setting. Do not try to modify the object; it will probably
     * not do what you want.
     */
    getinterface(): GlkGame;
    /**
     * Log the message in the browser's error log, if it has one. (This shows
     * up in Safari, in Opera, and in Firefox if you have Firebug installed.)
     */
    log(msg: string): void;
    /**
     * Display the red error pane, with a message in it. This is called on
     * fatal errors.
     * 
     * Deliberately does not use any Prototype functionality, because this
     * is called when Prototype couldn't be loaded.
     */
    error(msg: string): void;
}

interface GlkGame {
    accept(event: GlkEvent): void;
    gameport?: string;
    windowport?: string;
    spacing?: number;
    inspacing?: number;
    outspacing?: number;
    inspacingx?: number;
    inspacingy?: number;
    outspacingx?: number;
    outspacingy?: number;
    detect_external_links?: boolean;
    regex_external_links?: RegExp;
}

interface GlkEvent {
    /**
     * 'init', 'line', 'char', 'hyperlink', 'arrange', 'specialresponse',
     * 'external', or 'refresh'.
     */
    type: string;
    gen: number;
    metrics?: GlkMetrics;
    response?: string;
    value?: any;
    window?: number;
    partial?: {
        [window: number]: string;
    };
}

interface GlkMetrics {
    width: number;
    height: number;
    outspacingx: number;
    outspacingy: number;
    gridcharwidth: number;
    gridcharheight: number;
    gridmarginx: number;
    gridmarginy: number;
    buffercharwidth: number;
    buffercharheight: number;
}

interface GlkUpdate {
    /**
     * 'error', 'pass', 'retry', or 'update'.
     */
    type: string;
    message?: string;
    gen?: number;
    windows?: GlkWindowUpdate[];
    content?: GlkContentUpdate[];
    input?: GlkInputUpdate[];
    disable?: boolean;
    specialinput?: GlkSpecialInputUpdate;
}

interface GlkWindowUpdate {
    id: number;
    /**
     * 'buffer' or 'grid'.
     */
    type: string;
    rock: any;
    left: number;
    top: number;
    width: number;
    height: number;
    gridheight?: number;
    gridwidth?: number;
}

interface GlkContentUpdate {
    id: number;

    /**
     * For grid window updates.
     */
    lines?: GlkContentLineUpdate[];

    /**
     * For buffer window updates.
     */
    text?: GlkContentTextUpdate[];
    /**
     * For buffer window updates.
     */
    clear?: boolean;
}

interface GlkContentLineUpdate {
    line: number;
    /**
     * Normally an array of GlkContentLineUpdate, but can also contain pairs of strings (style, text).
     */
    content?: GlkContentLineDataUpdate[];
}

interface GlkContentTextUpdate {
    append?: boolean;
    /**
     * Normally an array of GlkContentLineUpdate, but can also contain pairs of strings (style, text).
     */
    content: GlkContentLineDataUpdate[]; 
}

interface GlkContentLineDataUpdate {
    style: string;
    text: string;
    hyperlink?: boolean;
}

interface GlkInputUpdate {
    id: number;
    hyperlink?: boolean;
    gen?: number;
    /**
     * 'char' or 'line'.
     */
    type?: string;
    /**
     * For line input only.
     */
    maxlen?: number;
    /**
     * For line input only.
     */
    initial?: string;
    /**
     * For line input only.
     */
    terminators?: string[];
}

interface GlkSpecialInputUpdate {
    type: string;
    filemode?: string;
    filetype?: string;
    gameid?: string;
}

declare var GlkOte: GlkOte;
