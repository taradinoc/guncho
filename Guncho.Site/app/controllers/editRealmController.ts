'use strict';
module app {
    export interface IEditRealmControllerScope {
        realm: IRealm;
        compilers: ICompilerOptions[];
        assets: IAsset[];
        selectedAsset: IEditingAsset;

        saveSettings(): void;
        saveAssets(): void;
        loadSelectedAsset(): void;
    }

    export interface IAsset {
        version: number;
        path: string;
        uri: string;
        history: string;
        contentType: string;
    }

    export interface IEditingAsset extends IAsset {
        loaded?: boolean;
        dirty?: boolean;
        data?: string;
    }

    export interface IAssetManifest {
        version: number;
        history: string;
        assets: IAsset[];
    }

    export class EditRealmController {
        constructor($scope: IEditRealmControllerScope, $http: ng.IHttpService,
            $routeParams: ng.route.IRouteParamsService) {

            $scope.loadSelectedAsset = () => {
                var asset = $scope.selectedAsset;
                if (!asset.loaded) {
                    $http.get(asset.uri).then(
                        (response: ng.IHttpPromiseCallbackArg<string>) => {
                            asset.data = response.data;
                            asset.dirty = false;
                            asset.loaded = true;
                        });
                }
            };

            var realmName = $routeParams['realmName'];

            $http.get(app.serviceBase + 'realms/' + realmName).then(
                (response: ng.IHttpPromiseCallbackArg<IRealm>) => {
                    $scope.realm = response.data;
                });

            $http.get(app.serviceBase + 'realms/compilers').then(
                (response: ng.IHttpPromiseCallbackArg<ICompilerOptions[]>) => {
                    $scope.compilers = response.data;
                });

            $http.get(app.serviceBase + 'assets/realm/' + realmName).then(
                (response: ng.IHttpPromiseCallbackArg<IAssetManifest>) => {
                    $scope.assets = response.data.assets;
                });
        }
    }
}