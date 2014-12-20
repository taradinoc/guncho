///<reference path="../app.ts" />
'use strict';
interface ISignUpControllerScope extends ng.IScope {
    savedSuccessfully: boolean;
    message: string;
    registration: IRegistration;

    signUp(): void;
}

class SignUpController {
    public static $inject = ['$scope', '$timeout', '$location',
        'authService'];
    constructor($scope: ISignUpControllerScope, $timeout: ng.ITimeoutService, $location: ng.ILocationService,
        authService: IAuthService) {

        $scope.savedSuccessfully = false;
        $scope.message = '';

        $scope.registration = {
            userName: '',
            password: '',
            confirmPassword: ''
        };

        var startTimer = function () {
            var timer = $timeout(
                () => {
                    $timeout.cancel(timer);
                    $location.path('/login');
                }, 2000);
        };

        $scope.signUp = function () {

            authService.saveRegistration($scope.registration).then(
                response => {
                    $scope.savedSuccessfully = true;
                    $scope.message = "Registration was successful! You'll be redirected to the login page in 2 seconds.";
                    startTimer();
                },
                response => {
                    var errors: any[] = [];
                    for (var key in response.data.modelState) {
                        if (response.data.modelState.hasOwnProperty(key)) {
                            for (var i = 0; i < response.data.modelState[key].length; i++) {
                                errors.push(response.data.modelState[key][i]);
                            }
                        }
                    }
                    $scope.message = 'Registration error: ' + errors.join(' ');
                });
        };
    }
}

app.controller('signUpController', SignUpController);
