'use strict';
module app {
    export interface IListRealmsControllerScope {
        heading: string;
        realms: IRealmResource[];
    }

    export interface IListRealmsFilter {
        ownedByActor?: boolean;
    }
    
    export class ListRealmsController {
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
}
