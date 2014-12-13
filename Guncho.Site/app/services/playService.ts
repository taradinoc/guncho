/// <reference path="../../scripts/typings/signalr/signalr.d.ts" />
'use strict';
module app {
    export interface IPlayService {
        start(): void;
        stop(): void;

        events: ng.IScope;

        sendCommand(command: string): void;
    }

    export interface IHubConnectionFunc {
        (url?: string, queryString?: any, logging?: boolean): HubConnection;
    }

    export class PlayService implements IPlayService {
        private connection: HubConnection;
        private proxy: HubProxy;
        public events: ng.IScope;

        public static $inject = ['$rootScope', '$log', 'hubConnection', 'signalrBase'];
        constructor($rootScope: ng.IRootScopeService, $log: ng.ILogService, hubConnection: IHubConnectionFunc, signalrBase: string) {
            var connection = hubConnection(signalrBase);
            this.connection = connection;
            this.proxy = connection.createHubProxy('PlayHub');

            var events = $rootScope.$new();
            this.events = events;

            this.proxy.on('writeLine',
                line => {
                    $log.log('writeLine(' + line + ')');
                    events.$emit('writeLine', line);
                    $rootScope.$apply();
                });
        }

        public start() {
            this.connection.start();
        }

        public stop() {
            this.connection.stop();
        }

        public sendCommand(command: string) {
            this.proxy.invoke('sendCommand', command);
        }
    }
}
