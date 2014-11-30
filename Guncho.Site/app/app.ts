/// <reference path="globals.ts" />
/// <reference path="services/authService.ts" />
/// <reference path="services/authInterceptorService.ts" />
/// <reference path="controllers/homeController.ts" />
/// <reference path="controllers/indexController.ts" />
/// <reference path="controllers/loginController.ts" />
/// <reference path="controllers/listRealmsController.ts" />
module app {
    function configureRoutes($routeProvider: ng.route.IRouteProvider) {
        $routeProvider.when("/home", {
            controller: "homeController",
            templateUrl: "/app/views/home.html"
        });

        $routeProvider.when("/login", {
            controller: "loginController",
            templateUrl: "/app/views/login.html"
        });

        /*$routeProvider.when("/signup", {
            controller: "signupController",
            templateUrl: "/app/views/signup.html"
        });*/

        $routeProvider.when("/realms", {
            controller: "listRealmsController",
            templateUrl: "/app/views/listRealms.html",
            resolve: {
                filter: (): IListRealmsFilter => ({})
            }
        });

        $routeProvider.when("/realms/my", {
            controller: "listRealmsController",
            templateUrl: "/app/views/listRealms.html",
            resolve: {
                filter: (): IListRealmsFilter => ({ ownedByActor: true })
            }
        });

        $routeProvider.when("/realms/:realmName", {
            controller: "editRealmController",
            templateUrl: "/app/views/editRealm.html"
        });

        $routeProvider.otherwise({ redirectTo: "/home" });
    }

    function configureAuthInterceptor($httpProvider: ng.IHttpProvider) {
        $httpProvider.interceptors.push('authInterceptorService');
    }

    angular.module('GunchoApp', ['ngRoute', 'LocalStorageModule', 'angular-loading-bar'])
        .service('authService', AuthService)
        .service('authInterceptorService', AuthInterceptorService)
        .controller('loginController', LoginController)
        .controller('indexController', IndexController)
        .controller('homeController', HomeController)
        .controller('listRealmsController', ListRealmsController)
        .controller('editRealmController', EditRealmController)
        .config(configureRoutes)
        .config(configureAuthInterceptor)
        .run(['authService', function (authService: IAuthService) {
            authService.fillAuthData();
        }]);
}
