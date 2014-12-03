'use strict';
module app {
    export interface IEditRealmControllerScope extends ng.IScope {
        settingsForm: {
            realmName: string;
            compiler: ICompilerOptions;
        };

        assetsForm: {
            selectedAsset: IEditingAsset;
        };

        compilers: ICompilerOptions[];
        realm: IRealm;
        assets: IAsset[];

        saveSettings(): void;
        saveAssets(): void;
        loadSelectedAsset(): void;
        isSettingsFormDirty(): boolean;
        isAssetsFormDirty(): boolean;
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
        public static $inject: string[] = [
            '$scope', '$http', '$routeParams', 'serviceBase'
        ];
        constructor($scope: IEditRealmControllerScope, $http: ng.IHttpService,
            $routeParams: ng.route.IRouteParamsService, serviceBase: string) {

            $scope.settingsForm = {
                realmName: '',
                compiler: { language: '', version: '' },
            };
            $scope.assetsForm = {
                selectedAsset: null,
            };

            $scope.loadSelectedAsset = () => {
                var asset = $scope.assetsForm.selectedAsset;
                if (!asset.loaded) {
                    $http.get(asset.uri).then(
                        (response: ng.IHttpPromiseCallbackArg<string>) => {
                            asset.data = response.data;
                            asset.dirty = false;
                            asset.loaded = true;
                        });
                }
            };

            $scope.saveAssets = () => {
                angular.forEach(
                    $scope.assets,
                    (asset: IEditingAsset, key: number) => {
                        if (asset.dirty) {
                            $http.put(asset.uri, asset.data, { headers: { 'Content-Type': asset.contentType } }).then(
                                response => {
                                    asset.dirty = false;
                                },
                                error => {
                                    // TODO: better error reporting
                                    alert("Failed PUT to " + asset.uri + ": " + error.status + " " + error.statusText);
                                });
                        }
                    });
            };

            $scope.saveSettings = () => {
                // TODO: code me
            };

            $scope.isSettingsFormDirty = () => {
                var form = $scope.settingsForm;
                return (form.realmName != $scope.realm.name ||
                    form.compiler.language != $scope.realm.compiler.language ||
                    form.compiler.version != $scope.realm.compiler.version);
            };

            $scope.isAssetsFormDirty = () => {
                for (var i = 0; i < $scope.assets.length; i++) {
                    if ((<IEditingAsset>$scope.assets[i]).dirty) {
                        return true;
                    }
                }
                return false;
            };

            var realmName = $routeParams['realmName'];
            
            $http.get(serviceBase + 'realms/' + encodeURIComponent(realmName)).then(
                (response: ng.IHttpPromiseCallbackArg<IRealm>) => {
                    $scope.realm = response.data;
                    $scope.settingsForm.realmName = $scope.realm.name;
                    $scope.settingsForm.compiler = angular.copy($scope.realm.compiler);
                });

            $http.get(serviceBase + 'realms/compilers').then(
                (response: ng.IHttpPromiseCallbackArg<ICompilerOptions[]>) => {
                    $scope.compilers = response.data;
                });

            $http.get(serviceBase + 'assets/realm/' + encodeURIComponent(realmName)).then(
                (response: ng.IHttpPromiseCallbackArg<IAssetManifest>) => {
                    $scope.assets = response.data.assets;
                    if ($scope.assets.length) {
                        $scope.assetsForm.selectedAsset = $scope.assets[0];
                        $scope.loadSelectedAsset();
                    }
                });
        }
    }
}