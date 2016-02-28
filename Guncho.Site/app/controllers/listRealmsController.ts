/// <reference path="../app.ts" />
'use strict';
interface IListRealmsControllerScope {
    heading: string;
    realms: IRealmResource[];
}

class ListRealmsController {
    public static $inject = [
        '$scope', '$routeParams',
        'Realm'
    ];
    constructor($scope: IListRealmsControllerScope, $routeParams: ng.route.IRouteParamsService,
        Realm: IRealmResourceClass) {

        var query = 'query' in $routeParams ? $routeParams['query'] : '';
        switch (query) {
            case 'my':
                $scope.heading = "My Realms";
                $scope.realms = Realm.queryMy();
                break;

            default:
                $scope.heading = "All Realms";
                $scope.realms = Realm.query();
                break;
        }
    }
}

app.controller('listRealmsController', ListRealmsController);
