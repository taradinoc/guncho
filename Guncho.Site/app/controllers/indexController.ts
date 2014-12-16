/// <reference path="../app.ts" />
'use strict';
interface IIndexControllerScope extends ng.IScope {
    authentication: IAuthentication;
    gunchoClientVersion: string;
    pendingMsgCount: IMessageCount;

    logOut(): void;
}

class IndexController {
    public static $inject = ['$scope', '$location',
        'authService', 'playService', 'glkService',
        'gunchoClientVersion'];
    constructor($scope: IIndexControllerScope, $location: ng.ILocationService,
        authService: IAuthService, playService: IPlayService, glkService: IGlkService,
        gunchoClientVersion: string) {

        $scope.authentication = authService.authentication;
        $scope.gunchoClientVersion = gunchoClientVersion;
        $scope.pendingMsgCount = playService.messageCount;

        $scope.logOut = () => {
            authService.logout();
            $location.path('/home');
        };

        function say(text: string) {
            glkService.updateText([
                {
                    content: [{ style: 'normal', text: text }]
                }
            ]);
        }

        var unsubscribe = playService.events.$on('writeLine',
            (event, line) => { say(line); });

        $scope.$on('$destroy', () => { unsubscribe(); });
    }
}

app.controller('indexController', IndexController);
