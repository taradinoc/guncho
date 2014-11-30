'use strict';
module app {
    export interface IListRealmsControllerScope {
        realms: any[];
    }

    export interface IListRealmsFilter {
        ownedByActor?: boolean;
    }

    export interface IRealm {
        name: string;
        owner: string;
        uri: string;
        compiler?: ICompilerOptions;
        runtime?: IRuntimeOptions;
        assets?: string;
    }

    export interface ICompilerOptions {
        language: string;
        version: string;
        supportedRuntimes?: IRuntimeOptions[];
    }

    export interface IRuntimeOptions {
        platform: string;
    }
    
    export class ListRealmsController {
        constructor($scope: IListRealmsControllerScope,
            $http: ng.IHttpService, filter: IListRealmsFilter) {

            $scope.realms = [];

            var url = app.serviceBase + "realms";
            if (filter && filter.ownedByActor) {
                url += "/my";
            }
            $http.get(url).then(
                (response: ng.IHttpPromiseCallbackArg<IRealm[]>) => {
                    $scope.realms = response.data;
                });
        }
    }
}
