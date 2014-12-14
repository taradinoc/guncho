/// <reference path="../app.ts" />
'use strict';
interface IIndexControllerScope extends ng.IScope {
    authentication: IAuthentication;
    gunchoClientVersion: string;

    logOut(): void;
}

class IndexController {
    public static $inject = ['$scope', '$location', 'authService', 'gunchoClientVersion'];
    constructor($scope: IIndexControllerScope, $location: ng.ILocationService, authService: IAuthService,
            gunchoClientVersion: string) {
        $scope.authentication = authService.authentication;
        $scope.gunchoClientVersion = gunchoClientVersion;

        $scope.logOut = () => {
            authService.logout();
            $location.path('/home');
        };
    }
}

app.controller('indexController', IndexController);
