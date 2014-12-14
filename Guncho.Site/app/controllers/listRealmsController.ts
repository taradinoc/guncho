/// <reference path="../app.ts" />
'use strict';
interface IListRealmsControllerScope {
    heading: string;
    realms: IRealmResource[];
}

interface IListRealmsFilter {
    ownedByActor?: boolean;
}

class ListRealmsController {
    public static $inject = ['$scope', 'Realm', 'filter'];
    constructor($scope: IListRealmsControllerScope, Realm: IRealmResourceClass,
        filter: IListRealmsFilter) {

        if (filter && filter.ownedByActor) {
            $scope.heading = "My Realms";
            $scope.realms = Realm.queryMy();
        } else {
            $scope.heading = "All Realms";
            $scope.realms = Realm.query();
        }
    }
}

app.controller('listRealmsController', ListRealmsController);
