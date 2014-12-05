'use strict';
module app {
    export interface IEditRealmControllerScope extends ng.IScope {
        settingsForm: {
            realmName: string;
            compiler: ICompilerOptions;

            loaded?: boolean;
            realmLoaded?: boolean;
            compilersLoaded?: boolean;
        };

        assetsForm: {
            selectedAsset: IEditingAsset;
        };

        compilers: ICompilerOptions[];
        realm: IRealmResource;
        manifest: IRealmAssetManifestResource;
        assets: IAsset[];

        saveSettings(): void;
        saveAssets(): void;
        loadSelectedAsset(): void;
        isSettingsFormDirty(): boolean;
        isAssetsFormDirty(): boolean;
    }


    export interface IEditingAsset extends IAsset {
        loaded?: boolean;
        dirty?: boolean;
        data?: string;
    }

    export class EditRealmController {
        public static $inject: string[] = [
            '$scope', '$http', '$routeParams', 'serviceBase',
            'Realm', 'RealmAssetManifest'
        ];
        constructor($scope: IEditRealmControllerScope, $http: ng.IHttpService,
            $routeParams: ng.route.IRouteParamsService, serviceBase: string,
            Realm: IRealmResourceClass,
            RealmAssetManifest: IRealmAssetManifestResourceClass) {

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

            var updateLoaded = () => {
                var f = $scope.settingsForm;
                f.loaded = f.realmLoaded && f.compilersLoaded;
            };

            $scope.realm = Realm.get({ name: realmName },
                () => {
                    $scope.settingsForm.realmName = $scope.realm.name;
                    $scope.settingsForm.compiler = angular.copy($scope.realm.compiler);
                    $scope.settingsForm.realmLoaded = true;
                    updateLoaded();
                });

            $http.get(serviceBase + 'realms/compilers').then(
                (response: ng.IHttpPromiseCallbackArg<ICompilerOptions[]>) => {
                    $scope.compilers = response.data;
                    $scope.settingsForm.compilersLoaded = true;
                    updateLoaded();
                });

            $scope.manifest = RealmAssetManifest.get({ realmName: realmName },
                () => {
                    $scope.assets = $scope.manifest.assets;
                    if ($scope.assets.length) {
                        $scope.assetsForm.selectedAsset = $scope.assets[0];
                        $scope.loadSelectedAsset();
                    }
                });
        }
    }
}