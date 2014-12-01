'use strict';
module app {
    export interface IIndexControllerScope extends ng.IScope {
        authentication: IAuthentication;
        globals: any;

        logOut(): void;
    }

    export class IndexController {
        constructor($scope: IIndexControllerScope, $location: ng.ILocationService, authService: IAuthService) {
            $scope.authentication = authService.authentication;
            $scope.globals = globals;

            $scope.logOut = () => {
                authService.logout();
                $location.path('/home');
            };
        }
    }
}
