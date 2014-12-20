/// <reference path="../app.ts" />
'use strict';
interface ILoginData {
    userName: string;
    password: string;
}

interface IRegistration {
    userName: string;
    password: string;
    confirmPassword: string;
}

interface IAuthentication {
    isAuth: boolean;
    userName: string;
}

interface IAuthorizationData {
    token: string;
    userName: string;
}

interface IAuthService {
    authentication: IAuthentication;
    saveRegistration(registration: {}): ng.IPromise<ng.IHttpPromiseCallbackArg<{}>>;
    login(loginData: ILoginData): ng.IPromise<{}>;
    logout(): void;
    fillAuthData(): void;
}

interface IAccessTokenResponse {
    access_token: string;
    expires_in: number;
    token_type: string;
    username: string;
}

class AuthService implements IAuthService {
    public static $inject = ['$http', '$q', 'localStorageService', 'serviceBase', 'signalR'];
    constructor(private $http: ng.IHttpService, private $q: ng.IQService,
        private localStorageService: ng.localStorage.ILocalStorageService, private serviceBase: string,
        private signalR: any) { }

    authentication = { isAuth: false, userName: "" };

    saveRegistration(registration: IRegistration): ng.IPromise<ng.IHttpPromiseCallbackArg<{}>> {
        this.logout();

        return this.$http.post(this.serviceBase + '/account', registration)
            .then(response => {
                return response;
            });
    }

    login(loginData: ILoginData): ng.IPromise<{}> {
        var data = "grant_type=password&username=" + encodeURIComponent(loginData.userName) +
            "&password=" + encodeURIComponent(loginData.password);

        var deferred = this.$q.defer();

        this.$http.post(
            this.serviceBase + '/token',
            data,
            {
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
            })
            .success(
                (response: IAccessTokenResponse) => {
                    this.localStorageService.set('authorizationData', { token: response.access_token, userName: response.username });
                    this.signalR.ajaxDefaults.headers = { Authorization: 'Bearer ' + response.access_token };

                    this.authentication.isAuth = true;
                    this.authentication.userName = response.username;

                    deferred.resolve(response);
                })
            .error(
                (err, status) => {
                    this.logout();
                    deferred.reject(err);
                });

        return deferred.promise;
    }

    logout(): void {
        this.localStorageService.remove('authorizationData');
        this.authentication.isAuth = false;
        this.authentication.userName = "";
        if (this.signalR.ajaxDefaults.headers) {
            delete this.signalR.ajaxDefaults.headers.Authorization;
        }
    }

    fillAuthData(): void {
        var authData : IAuthorizationData = this.localStorageService.get('authorizationData');
        if (authData) {
            this.authentication.isAuth = true;
            this.authentication.userName = authData.userName;
            this.signalR.ajaxDefaults.headers = { Authorization: 'Bearer ' + authData.token };
        }
    }
}

app.service('authService', AuthService);
