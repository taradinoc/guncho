'use strict';

module app {
    export interface ILoginData {
        userName: string;
        password: string;
    }

    export interface IAuthentication {
        isAuth: boolean;
        userName: string;
    }

    export interface IAuthorizationData {
        token: string;
        userName: string;
    }

    export interface IAuthService {
        authentication: IAuthentication;
        saveRegistration(registration: {}): ng.IHttpPromiseCallbackArg<{}>;
        login(loginData: ILoginData): ng.IPromise<{}>;
        logout(): void;
        fillAuthData(): void;
    }

    interface IAccessTokenResponse {
        access_token: string;
    }

    export class AuthService implements IAuthService {
        constructor(private $http: ng.IHttpService, private $q: ng.IQService,
            private localStorageService: ng.localStorage.ILocalStorageService, private serviceBase: string) { }

        authentication = { isAuth: false, userName: "" };

        saveRegistration(registration: {}): ng.IHttpPromiseCallbackArg<{}> {
            return this.$http.post(this.serviceBase + 'account/register', registration)
                .then(response => {
                    return response;
                });
        }

        login(loginData: ILoginData): ng.IPromise<{}> {
            var data = "grant_type=password&username=" + loginData.userName + "&password=" + loginData.password;

            var deferred = this.$q.defer();

            this.$http.post(
                this.serviceBase + 'token',
                data,
                {
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
                })
                .success(
                    (response: IAccessTokenResponse) => {
                        this.localStorageService.set('authorizationData', { token: response.access_token, userName: loginData.userName });

                        this.authentication.isAuth = true;
                        this.authentication.userName = loginData.userName;

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
        }

        fillAuthData(): void {
            var authData : IAuthorizationData = this.localStorageService.get('authorizationData');
            if (authData) {
                this.authentication.isAuth = true;
                this.authentication.userName = authData.userName;
            }
        }
    }
}
