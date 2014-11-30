'use strict';

module app {
    export interface ILoginControllerScope extends ng.IScope {
        loginData: ILoginData;
        message: string;
        login(): void;
    }

    export class LoginController {
        constructor(private $scope: ILoginControllerScope, private $location: ng.ILocationService,
                private authService: IAuthService) {
            $scope.loginData = {
                userName: "",
                password: ""
            };

            $scope.message = "";

            $scope.login = () => {
                authService.login($scope.loginData).then(
                    response => {
                        $location.path('/realms/my');
                    },
                    err => {
                        $scope.message = err.error_description;
                    });
            };
        }
    }
}