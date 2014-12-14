module app {
    'use strict';
    export interface IIndexControllerScope extends ng.IScope {
        authentication: IAuthentication;
        gunchoClientVersion: string;

        logOut(): void;
    }

    export class IndexController {
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
}
