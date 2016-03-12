/// <reference path="../app.ts" />
'use strict';
interface IAuthInterceptorService {
    request(config: ng.IRequestConfig): ng.IRequestConfig;
    responseError(rejection: any): void;
}

class AuthInterceptorService implements IAuthInterceptorService {
    request: (config: ng.IRequestConfig) => ng.IRequestConfig;
    responseError: (rejection: any) => ng.IPromise<any>;

    public static $inject = ['$q', '$location', 'localStorageService'];
    constructor($q: ng.IQService, $location: ng.ILocationService,
        localStorageService: ng.localStorage.ILocalStorageService) {

        this.request = (config: ng.IRequestConfig) => {
            config.headers = config.headers || {};

            var authData: IAuthorizationData = localStorageService.get('authorizationData');
            if (authData) {
                config.headers['Authorization'] = 'Bearer ' + authData.token;
            }

            return config;
        };

        this.responseError = rejection => {
            if (rejection.status === 401) {
                $location.path('/login');
            }
            return $q.reject(rejection);
        };
    }
}

app.service('authInterceptorService', AuthInterceptorService);
