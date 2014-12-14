/// <reference path="../app.ts" />
/* tslint:disable:no-string-literal */
'use strict';
interface IEditRealmControllerScope extends ng.IScope {
    settingsForm: {
        realmName: string;
        compiler: ICompilerOptions;
        privacy: string;
        acl: Array<{
            user: string; access: string;
        }>;

        newAclEntryUser?: string;
        newAclEntryAccess?: string;

        loaded?: boolean;
        realmLoaded?: boolean;
        compilersLoaded?: boolean;
    };

    assetsForm: {
        selectedAsset: IEditingAsset;
    };

    compilers: ICompilerOptions[];
    privacyLevels: string[];
    accessLevels: string[];

    realm: IRealmResource;
    manifest: IRealmAssetManifestResource;
    assets: IEditingAsset[];

    saveSettings(): void;
    saveAssets(): void;
    loadSelectedAsset(): void;
    deleteAclEntry(index: number): void;
    addAclEntry(): void;
    isSettingsFormDirty(): boolean;
    isAssetsFormDirty(): boolean;
}

interface IEditingAsset extends IAsset {
    loaded?: boolean;
    dirty?: boolean;
    data?: string;
}

class EditRealmController {
    public static $inject = [
        '$scope', '$http',
        '$routeParams', 'serviceBase',
        'Realm',
        'RealmAssetManifest'
    ];
    constructor($scope: IEditRealmControllerScope, $http: ng.IHttpService,
        $routeParams: ng.route.IRouteParamsService, serviceBase: string,
        Realm: IRealmResourceClass,
        RealmAssetManifest: IRealmAssetManifestResourceClass) {

        $scope.settingsForm = {
            realmName: '',
            compiler: { language: '', version: '' },
            privacy: '',
            acl: []
        };
        $scope.assetsForm = {
            selectedAsset: null
        };

        $scope.privacyLevels = [
            'private', 'hidden', 'public', 'joinable', 'viewable'
        ];
        $scope.accessLevels = [
            'visible', 'invited', 'viewSource',
            'editSource', 'editSettings', 'editAccess', 'safetyOff',
        ];

        // TODO: use $resource for assets
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
                (asset, key) => {
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
            // TODO: implement saveSettings
            alert('FIXME');
        };

        $scope.deleteAclEntry = index => {
            $scope.settingsForm.acl.splice(index, 1);
        };

        $scope.addAclEntry = () => {
            $scope.settingsForm.acl.push({
                user: $scope.settingsForm.newAclEntryUser,
                access: $scope.settingsForm.newAclEntryAccess
            });
            $scope.settingsForm.newAclEntryUser = null;
            $scope.settingsForm.newAclEntryAccess = null;
        };

        $scope.isSettingsFormDirty = () => {
            var form = $scope.settingsForm;
            return (form.realmName !== $scope.realm.name ||
                form.compiler.language !== $scope.realm.compiler.language ||
                form.compiler.version !== $scope.realm.compiler.version);
        };

        $scope.isAssetsFormDirty = () => {
            for (var i = 0; i < $scope.assets.length; i++) {
                if ($scope.assets[i].dirty) {
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
                $scope.settingsForm.privacy = $scope.realm.privacy;
                $scope.settingsForm.acl = angular.copy($scope.realm.acl);
                $scope.settingsForm.realmLoaded = true;
                updateLoaded();
            });

        // TODO: use $resource for compilers
        $http.get(serviceBase + '/realms/compilers').then(
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

app.controller('editRealmController', EditRealmController);
