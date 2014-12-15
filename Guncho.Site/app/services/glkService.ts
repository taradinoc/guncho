/// <reference path="../app.ts" />
'use strict';
interface IGlkService {
    registerGameport(gameport: IGlkGameportController): void;
    /**
     * Gets the previously saved gameport element, or null if none has been saved.
     * The saved gameport element is removed after this function returns it.
     */
    restoreDom(): JQuery;
    /**
     * Performs one-time GlkOte initialization. This must be called by the glkGameport
     * directive when restoreDom() has returned null and a new element has been created
     * from the template.
     */
    initialSetup(): void;
    /**
     * Saves the gameport element to be restored later. It must have already been detached.
     */
    saveDom(gameportDom: JQuery): void;

    updateText(update: GlkContentTextUpdate[]): void;

    filterInputForDisplay(input: string): string;

    events: ng.IScope;
}

class GlkService implements IGlkService {
    private gameport: IGlkGameportController;
    private savedDom: JQuery = null;

    private metrics: GlkMetrics = null;
    private initialSetupDone = false;
    private windowsCreated = false;
    private generation = 0;
    private pendingUpdate: GlkUpdate = null;
    private bufferWindowId = 1;

    public events: ng.IScope;

    public static $inject: string[] = ['$rootScope'];
    constructor($rootScope: ng.IRootScopeService) {
        this.events = $rootScope.$new();
    }

    //#region Public Methods
    public registerGameport(gameport: IGlkGameportController) {
        this.gameport = gameport;
    }

    public initialSetup() {
        if (!this.initialSetupDone) {
            this.initialSetupDone = true;
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
        if (this.windowsCreated) {
            GlkOte.extevent(null);
        }
    }

    public filterInputForDisplay(input: string) {
        return input;
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
        this.gameport.removePrompts();
        this.appendUpdate(
            {
                type: 'update',
                content: [
                    { id: this.bufferWindowId, text: [{ content: [{ style: 'prompt', text: '> ' }] }] }
                ]
            });
    }

    private accept(event: GlkEvent) {
        if (angular.isDefined(event.gen)) {
            this.generation = event.gen + 1;
        }
        if (angular.isDefined(event.metrics)) {
            this.metrics = event.metrics;
        }
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
                    }],
                    input: [{
                        id: this.bufferWindowId,
                        type: 'line',
                        gen: this.generation,
                        maxlen: 256
                    }],
                });
                this.showPrompt();
                break;

            case 'line':
                var input: string = event.value;
                var displayInput = this.filterInputForDisplay(input);
                this.updateText([
                    {
                        append: true,
                        content: [{ style: 'normal', text: '> ' }, { style: 'input', text: displayInput }]
                    }
                ]);
                this.showPrompt();
                this.appendUpdate({
                    type: 'update',
                    input: [{
                        id: this.bufferWindowId,
                        type: 'line',
                        gen: this.generation,
                        maxlen: 256
                    }]
                });
                this.events.$emit('lineEntered', input);
                break;

            case 'external':
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
                this.showPrompt();
                break;

            default:
                // nada
                break;
        }

        this.select();
        this.windowsCreated = true;
    }

    private select() {
        if (this.pendingUpdate) {
            this.pendingUpdate.gen = this.generation++;
            GlkOte.update(this.pendingUpdate);
            this.pendingUpdate = null;
        } else {
            GlkOte.update({ type: 'pass' });
        }
    }
    //#endregion
}

app.service('glkService', GlkService);
