'use strict';
function configureRoutes($routeProvider: ng.route.IRouteProvider) {
    'use strict';
    $routeProvider.when("/credits", {
        templateUrl: "/app/views/credits.html"
    });

    $routeProvider.when("/home", {
        templateUrl: "/app/views/home.html"
    });

    $routeProvider.when("/login", {
        controller: "loginController",
        templateUrl: "/app/views/login.html"
    });

    $routeProvider.when("/play", {
        controller: "playController",
        templateUrl: "/app/views/play.html"
    });

    $routeProvider.when("/profile/my/edit", {
        controller: "editProfileController",
        templateUrl: "/app/views/editProfile.html"
    });

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

    $routeProvider.when("/signup", {
        controller: "signUpController",
        templateUrl: "/app/views/signUp.html"
    });

    $routeProvider.otherwise({ redirectTo: "/home" });
}

function configureAuthInterceptor($httpProvider: ng.IHttpProvider) {
    'use strict';
    $httpProvider.interceptors.push('authInterceptorService');
}

var app = angular
    .module('GunchoApp',
    ['ngRoute', 'ngResource', 'LocalStorageModule', 'angular-loading-bar', 'ui.unique', 'ui.bootstrap', 'ui.validate'])
    .constant('gunchoClientVersion', '1.1')
    .constant('serviceBase', 'http://localhost:4109/api')
    .constant('signalrBase', 'http://localhost:4109/signalr')
    .config(configureRoutes)
    .config(configureAuthInterceptor)
    .value('hubConnection', $.hubConnection)
    .value('signalR', $.signalR);

function appMain() {
    'use strict';
    app.run(['authService', function (authService: IAuthService) {
        authService.fillAuthData();
    }]);
}
