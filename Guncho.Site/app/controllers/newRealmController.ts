/// <reference path="../app.ts" />
'use strict';
interface INewRealmControllerScope extends ng.IScope {
    newRealmForm: {
        savedSuccessfully: boolean;
        message: string;
        submitting: boolean;
    };

    newRealm: {
        name: string;
    };

    createRealm(): void;
}

class NewRealmController {
    public static $inject = ['$scope', '$location', 'Realm'];
    constructor($scope: INewRealmControllerScope, $location: ng.ILocationService, Realm: IRealmResourceClass) {
        $scope.newRealmForm = { savedSuccessfully: false, message: '', submitting: false };
        $scope.newRealm = { name: '' };

        $scope.createRealm = () => {
            var realm = new Realm({ name: $scope.newRealm.name });
            $scope.newRealmForm.submitting = true;
            realm.$create(
                (savedRealm: IRealm) => {
                    // success
                    $location.path('/realms/edit/' + savedRealm.name);
                },
                (error: ng.IHttpPromiseCallbackArg<any>) => {
                    // error
                    $scope.newRealmForm.message =
                        error.data.modelState && error.data.modelState.compiler ? error.data.modelState.compiler.join(' ') : error.data.message;
                    $scope.newRealmForm.submitting = false;
                });
        };
    }
}

app.controller('newRealmController', NewRealmController);
