/// <reference path="../app.ts" />
'use strict';
interface IPlayControllerScope extends ng.IScope {
    lines: string[];
    command: string;

    keyPress(event: KeyboardEvent): void;
}

class PlayController {
    public static $inject = ['$scope', 'playService'];
    constructor(private $scope: IPlayControllerScope, private playService: IPlayService) {
        $scope.keyPress = event => {
            if (event.keyCode === 13) {
                playService.sendCommand($scope.command);
                $scope.command = null;
            }
        };

        $scope.$on('$viewContentLoaded',
            () => { playService.start(); });

        var unsubscribe : Function[] = [];
        unsubscribe.push(playService.events.$on(
            'writeLine',
            (event, line) => {
                $scope.lines.push(line);
            }));
        unsubscribe.push(playService.events.$on(
            'connectionStateChanged',
            (event, oldState, newState) => {
                $scope.lines.push('[Connection state: ' + oldState + ' -> ' + newState + ']');
            }));
        unsubscribe.push(playService.events.$on(
            'connectionSlow',
            event => {
                $scope.lines.push('[Wait for it...]');
            }));

        $scope.$on('$destroy',
            () => {
                playService.stop();
                angular.forEach(unsubscribe, (value, key) => {
                    value();
                });
            });

        $scope.lines = [];
    }
}

app.controller('playController', PlayController);
