/// <reference path="../app.ts" />
'use strict';
interface IRealm {
    name: string;
    owner: string;
    uri: string;
    compiler?: ICompilerOptions;
    runtime?: IRuntimeOptions;
    manifestUri?: string;
    privacy: string;
    acl?: Array<{ user: string; access: string; }>;
}

interface ICompilerOptions {
    language: string;
    version: string;
    supportedRuntimes?: IRuntimeOptions[];
}

interface IRuntimeOptions {
    platform: string;
}

interface IRealmResource extends IRealm, ng.resource.IResource<IRealm> {
    $update(): ng.IPromise<IRealm>;
    $update(params?: Object, success?: Function, error?: Function): ng.IPromise<IRealm>;
    $update(success: Function, error?: Function): ng.IPromise<IRealm>;
}

interface IRealmResourceClass extends ng.resource.IResourceClass<IRealmResource> {
    update(data: IRealm): IRealmResource;
    update(data: IRealm, params?: Object, success?: Function, error?: Function): IRealmResource;
    update(data: IRealm, success: Function, error?: Function): IRealmResource;

    queryMy(): ng.resource.IResourceArray<IRealmResource>;
    queryMy(params: Object): ng.resource.IResourceArray<IRealmResource>;
    queryMy(success: Function, error?: Function): ng.resource.IResourceArray<IRealmResource>;
    queryMy(params: Object, success: Function, error?: Function): ng.resource.IResourceArray<IRealmResource>;
}

function RealmResourceFactory($resource: ng.resource.IResourceService, serviceBase: string) {
    return <IRealmResourceClass>$resource(
        serviceBase + '/realms/:name',
        { name: '@name' },
        {
            update: { method: 'PUT' },
            queryMy: {
                method: 'GET', params: { name: 'my' }, isArray: true
            }
        });
}
RealmResourceFactory.$inject = ['$resource', 'serviceBase'];
app.factory('Realm', RealmResourceFactory);

interface IAsset {
    version: number;
    path: string;
    uri: string;
    history: string;
    contentType: string;
}

interface IAssetManifest {
    version: number;
    history: string;
    assets: IAsset[];
}

interface IRealmAssetResource extends IAsset, ng.resource.IResource<IAsset> {
    $update(): ng.IPromise<IAsset>;
    $update(params?: Object, success?: Function, error?: Function): ng.IPromise<IAsset>;
    $update(success: Function, error?: Function): ng.IPromise<IAsset>;
}

interface IRealmAssetResourceClass extends ng.resource.IResourceClass<IRealmAssetResource> {
    update(data: IAsset): IRealmAssetResource;
    update(data: IAsset, params?: Object, success?: Function, error?: Function): IRealmAssetResource;
    update(data: IAsset, success: Function, error?: Function): IRealmAssetResource;
}

function RealmAssetResourceFactory($resource: ng.resource.IResourceService, serviceBase: string) {
    return <IRealmAssetResourceClass>$resource(
        serviceBase + '/realms/:realmName/asset/:path',
        { path: '@path' },
        {
            update: {
                method: 'PUT', url: '@uri'
            }
        });
}
RealmAssetResourceFactory.$inject = ['$resource', 'serviceBase'];
app.factory('RealmAsset', RealmAssetResourceFactory);

interface IRealmAssetManifestResource extends IAssetManifest, ng.resource.IResource<IAssetManifest> {
    $update(): ng.IPromise<IAssetManifest>;
    $update(params?: Object, success?: Function, error?: Function): ng.IPromise<IAssetManifest>;
    $update(success: Function, error?: Function): ng.IPromise<IAssetManifest>;
}

interface IRealmAssetManifestResourceClass extends ng.resource.IResourceClass<IRealmAssetManifestResource> {
}

function RealmAssetManifestResourceFactory($resource: ng.resource.IResourceService, serviceBase: string) {
    return <IRealmAssetManifestResourceClass>$resource(
        serviceBase + '/realms/:realmName/manifest');
}
RealmAssetManifestResourceFactory.$inject = ['$resource', 'serviceBase'];
app.factory('RealmAssetManifest', RealmAssetManifestResourceFactory);
