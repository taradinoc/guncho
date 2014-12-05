/// <reference path="services/authService.ts" />
/// <reference path="services/authInterceptorService.ts" />
/// <reference path="services/resources.ts" />
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

    angular.module('GunchoApp', ['ngRoute', 'ngResource', 'LocalStorageModule', 'angular-loading-bar', 'ui.unique', 'ui.bootstrap'])
        .constant('gunchoClientVersion', '1.1')
        .constant('serviceBase', 'http://localhost:4109/api/') 
        .config(configureRoutes)
        .config(configureAuthInterceptor)
        .service('authService', AuthService)
        .service('authInterceptorService', AuthInterceptorService)
        .factory('Realm', RealmResourceFactory)
        .factory('RealmAsset', RealmAssetResourceFactory)
        .factory('RealmAssetManifest', RealmAssetManifestResourceFactory)
        .controller('loginController', LoginController)
        .controller('indexController', IndexController)
        .controller('homeController', HomeController)
        .controller('listRealmsController', ListRealmsController)
        .controller('editRealmController', EditRealmController)
        .run(['authService', function (authService: IAuthService) {
            authService.fillAuthData();
        }]);
}

(() => { })();