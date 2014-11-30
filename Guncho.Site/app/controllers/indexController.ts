'use strict';
module app {
    export interface IIndexControllerScope extends ng.IScope {
        logOut(): void;
        authentication: IAuthentication;
    }

    export class IndexController {
        constructor(private $scope: IIndexControllerScope, private $location: ng.ILocationService, private authService: IAuthService) {
            $scope.authentication = authService.authentication;

            $scope.logOut = () => {
                authService.logout();
                $location.path('/home');
            };
        }
    }
}
