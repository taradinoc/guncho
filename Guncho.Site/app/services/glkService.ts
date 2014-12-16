/// <reference path="../app.ts" />
'use strict';
interface IGlkService {
    /**
     * Gets the previously saved gameport element, or null if none has been saved.
     * Prompts are removed first, and the saved gameport element is cleared upon return.
     */
    restoreDom(): JQuery;
    /**
     * Performs one-time GlkOte initialization. This must be called by the glkGameport
     * directive when restoreDom() has returned null and a new element has been created
     * from the template.
     * 
     * @param removePrompts A function that removes all past lines that were printed with the 'prompt' style.
     */
    initialSetup(removePrompts: () => void): void;
    /**
     * Saves the gameport element to be restored later. It must have already been detached.
     */
    saveDom(gameportDom: JQuery): void;
    /**
     * Sends an input event, printing any pending output and recreating the input line.
     * This must be called after reattaching the gameport element.
     */
    interrupt(): void;

    updateText(update: GlkContentTextUpdate[]): void;

    filterInputForDisplay(input: string): string;

    events: ng.IScope;
}

class GlkService implements IGlkService {
    private savedDom: JQuery = null;
    private removePrompts: () => void = null;

    private metrics: GlkMetrics = null;
    private initialSetupDone = false;
    private windowsCreated = false;
    private lastOutputWasPrompt = false;
    private generation = 0;
    private pendingUpdate: GlkUpdate = null;
    private bufferWindowId = 1;

    private $log: ng.ILogService;

    public events: ng.IScope;

    public static $inject: string[] = ['$rootScope', '$log'];
    constructor($rootScope: ng.IRootScopeService, $log: ng.ILogService) {
        this.events = $rootScope.$new();
        this.$log = $log;
    }

    //#region Public Methods
    public initialSetup(removePrompts: () => void) {
        if (!this.initialSetupDone) {
            this.initialSetupDone = true;
            this.removePrompts = removePrompts;
            GlkOte.init({
                accept: event => {
                    this.accept(event);
                }
            });
        }
    }

    public saveDom(gameportDom: JQuery) {
        this.savedDom = gameportDom;
    }

    public restoreDom() {
        var result = this.savedDom;
        this.savedDom = null;
        return result;
    }

    public updateText(update: GlkContentTextUpdate[]) {
        this.appendUpdate(
            {
                type: 'update',
                content: [{ id: this.bufferWindowId, text: update }]
            });
        this.lastOutputWasPrompt = false;
        this.interrupt();
    }

    public filterInputForDisplay(input: string) {
        return input;
    }

    public interrupt() {
        if (this.windowsCreated) {
            GlkOte.extevent(null);
        }
    }
    //#endregion

    //#region Private Methods
    private appendUpdate(update: GlkUpdate) {
        var pending = this.pendingUpdate;

        if (!pending || pending.type !== update.type) {
            this.pendingUpdate = update;
            return;
        }

        if (angular.isDefined(update.message)) {
            pending.message = update.message;
        }

        if (angular.isDefined(update.disable)) {
            pending.disable = update.disable;
        }

        if (update.specialinput) {
            pending.specialinput = update.specialinput;
        }

        // TODO: remove earlier entries that are superseded by later ones
        if (update.windows) {
            pending.windows = (pending.windows || []).concat(update.windows);
        }

        if (update.content) {
            pending.content = (pending.content || []).concat(update.content);
        }

        if (update.input) {
            pending.input = (pending.input || []).concat(update.input);
        }
    }

    private showPrompt() {
        this.appendUpdate(
            {
                type: 'update',
                content: [
                    { id: this.bufferWindowId, text: [{ content: [{ style: 'prompt', text: '> ' }] }] }
                ]
            });
        this.lastOutputWasPrompt = true;
    }

    private accept(event: GlkEvent) {
        if (angular.isDefined(event.gen)) {
            this.generation = event.gen + 1;
        }
        if (angular.isDefined(event.metrics)) {
            this.metrics = event.metrics;
        }
        var forceInput = false;
        switch (event.type) {
            case 'init':
                this.appendUpdate({
                    type: 'update',
                    windows: [{
                        id: this.bufferWindowId,
                        type: 'buffer',
                        rock: 69105,
                        left: 1,
                        top: 1,
                        width: 500,
                        height: 500
                    }]
                });
                this.windowsCreated = true;
                forceInput = true;
                break;

            case 'line':
                var input: string = event.value;
                var displayInput = this.filterInputForDisplay(input);
                this.appendUpdate(
                    {
                        type: 'update',
                        content: [{
                            id: this.bufferWindowId,
                            text: [
                            {
                                append: true,
                                content: [{ style: 'normal', text: '> ' }, { style: 'input', text: displayInput }]
                            }
                        ] }]
                    });
                this.lastOutputWasPrompt = false;
                this.events.$emit('lineEntered', input);
                break;

            case 'external':
                // nada
                break;

            default:
                // nada
                break;
        }

        // refresh prompt
        if (!this.lastOutputWasPrompt) {
            this.removePrompts();
            this.showPrompt();
        }

        // refresh input line
        // TODO: this will move the cursor to the end of the input line every time an event happens :(
        this.appendUpdate({
            type: 'update',
            input: [{
                id: this.bufferWindowId,
                type: 'line',
                gen: this.generation,
                maxlen: 256,
                initial: event.partial ? event.partial[this.bufferWindowId] : null
            }]
        });

        this.select();
    }

    private flushUpdate() {
        if (this.pendingUpdate) {
            GlkOte.update(this.pendingUpdate);
            this.pendingUpdate = null;
        }
    }

    private select() {
        if (this.pendingUpdate) {
            this.pendingUpdate.gen = this.generation++;
            this.flushUpdate();
        } else {
            GlkOte.update({ type: 'pass' });
        }
    }
    //#endregion
}

app.service('glkService', GlkService);
