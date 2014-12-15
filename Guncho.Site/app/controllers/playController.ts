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

        $scope.$on('$viewContentLoaded',
            () => { playService.start(); });

        function say(text: string) {
            glkService.updateText([
                {
                    content: [{ style: 'normal', text: text }]
                }
            ]);
        }

        var unsubscribe : Function[] = [];
        unsubscribe.push(playService.events.$on(
            'writeLine',
            (event, line) => { say(line); }));
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
                }, 17000);
            }));

        $scope.$on('$destroy',
            () => {
                playService.stop();
                angular.forEach(unsubscribe, (value, key) => {
                    value();
                });
            });

        $scope.sendCommand = command => {
            playService.sendCommand(command);
        };
    }
}

app.controller('playController', PlayController);
