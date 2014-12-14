/// <reference path="../app.ts" />
'use strict';
interface ILoginControllerScope extends ng.IScope {
    loginData: ILoginData;
    message: string;
    login(): void;
}

class LoginController {
    public static $inject = ['$scope', '$location', 'authService'];
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

app.controller('loginController', LoginController);
