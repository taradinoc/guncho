/// <reference path="../app.ts" />
'use strict';
interface IPlayControllerScope extends ng.IScope {
    connectionState: string;
    connectionSlow: number;

    sendCommand(command: string): void;
}

class PlayController {
    public static $inject = ['$scope', '$timeout',
        'playService', 'glkService'];
    constructor(private $scope: IPlayControllerScope, $timeout: ng.ITimeoutService,
        playService: IPlayService, glkService: IGlkService) {

        // reset count in case messages were received while we were away
        playService.messageCount.reset();

        // set up scope
        $scope.connectionSlow = 0;
        $scope.sendCommand = command => {
            playService.sendCommand(command);
        };

        $scope.$on('$viewContentLoaded',
            () => { playService.start(); });

        var unsubscribe: Function[] = [];
        $scope.$on('$destroy',
            () => {
                angular.forEach(unsubscribe, (value, key) => {
                    value();
                });
            });

        // hook playService events

        // writeLine is handled by indexController to send the line to Glk, but we
        // also handle it here to keep the message count from increasing while output is visible.
        unsubscribe.push(playService.events.$on(
            'writeLine',
            (event, line) => { playService.messageCount.reset(); }));

        unsubscribe.push(playService.events.$on(
            'connectionStateChanged',
            (event, oldState, newState) => {
                $scope.connectionState = newState;
            }));
        unsubscribe.push(playService.events.$on(
            'connectionSlow',
            event => {
                $scope.connectionSlow++;
                $timeout(() => {
                    if ($scope.connectionSlow > 0) {
                        $scope.connectionSlow--;
                    }
                }, 5000);
            }));
    }
}

app.controller('playController', PlayController);
