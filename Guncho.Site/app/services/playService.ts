/// <reference path="../app.ts" />
/// <reference path="../../scripts/typings/signalr/signalr.d.ts" />
'use strict';
interface IPlayService {
    start(): void;
    stop(): void;

    events: ng.IScope;

    sendCommand(command: string): void;
}

interface IHubConnectionFunc {
    (url?: string, queryString?: any, logging?: boolean): HubConnection;
}

class PlayService implements IPlayService {
    private connection: HubConnection;
    private proxy: HubProxy;
    public events: ng.IScope;

    public static $inject = [
        '$rootScope', '$timeout', '$log',
        'hubConnection', 'signalrBase', 'signalR'];
    constructor($rootScope: ng.IRootScopeService, $timeout: ng.ITimeoutService, $log: ng.ILogService,
        hubConnection: IHubConnectionFunc, signalrBase: string, signalR: SignalR) {

        var connection = hubConnection(signalrBase, null, true);
        this.connection = connection;
        this.proxy = connection.createHubProxy('PlayHub');

        var events = $rootScope.$new();
        this.events = events;

        var connectionStateMap: { [n: number]: string } = {};
        connectionStateMap[signalR.connectionState.connecting] = 'connecting';
        connectionStateMap[signalR.connectionState.connected] = 'connected';
        connectionStateMap[signalR.connectionState.reconnecting] = 'reconnecting';
        connectionStateMap[signalR.connectionState.disconnected] = 'disconnected';

        // hook connection lifecycle events
        this.connection.connectionSlow(
            () => {
                events.$emit('connectionSlow');
                $rootScope.$apply();
            });

        this.connection.stateChanged(
            change => {
                $timeout(() => {
                    events.$emit('connectionStateChanged',
                        connectionStateMap[change.oldState],
                        connectionStateMap[change.newState]);
                }, 0);
            });

        // hook client methods
        this.proxy.on('writeLine',
            line => {
                events.$emit('writeLine', line);
                $rootScope.$apply();
            });

        this.proxy.on('goodbye',
            () => {
                // if we close too soon after receiving the goodbye message, the connection will time out
                // instead of closing immediately. 50ms seems to be enough.
                $timeout(() => { connection.stop(); }, 50);
            });
    }

    public start() {
        this.connection.start();
    }

    public stop() {
        this.connection.stop(true, true);
    }

    public sendCommand(command: string) {
        this.proxy.invoke('sendCommand', command);
    }
}

app.service('playService', PlayService);
