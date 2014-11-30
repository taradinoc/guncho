/// <reference path="services/authService.ts" />
/// <reference path="services/authInterceptorService.ts" />
/// <reference path="controllers/homeController.ts" />
/// <reference path="controllers/indexController.ts" />
/// <reference path="controllers/loginController.ts" />
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
        });

        $routeProvider.when("/orders", {
            controller: "ordersController",
            templateUrl: "/app/views/orders.html"
        });*/

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
        .config(configureRoutes)
        .config(configureAuthInterceptor)
        .run(['authService', function (authService: IAuthService) {
            authService.fillAuthData();
        }]);
}
