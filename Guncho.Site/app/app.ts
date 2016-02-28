'use strict';
function configureRoutes($routeProvider: ng.route.IRouteProvider) {
    'use strict';
    $routeProvider.when("/credits", {
        templateUrl: "/app/views/credits.html"
    });

    //$routeProvider.when("/about", {
    //    templateUrl: "/app/views/about.html"
    //});

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

    $routeProvider.when("/realms/list/:query", {
        controller: "listRealmsController",
        templateUrl: "/app/views/listRealms.html",
    });

    $routeProvider.when("/realms/edit/:realmName", {
        controller: "editRealmController",
        templateUrl: "/app/views/editRealm.html"
    });

    $routeProvider.when("/realms/new", {
        controller: "newRealmController",
        templateUrl: "/app/views/newRealm.html"
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
    .constant('serviceBase', '/api')
    .constant('signalrBase', '/signalr')
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
