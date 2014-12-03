'use strict';
module app {
    export interface IRealmsService {
        getRealmInfo(realmName: string): ng.IPromise<IRealm>;
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

    export interface IRealmResource extends IRealm, ng.resource.IResource<IRealm> {
        $update(): ng.IPromise<IRealm>;
        $update(params?: Object, success?: Function, error?: Function): ng.IPromise<IRealm>;
        $update(success: Function, error?: Function): ng.IPromise<IRealm>;
    }

    export interface IRealmResourceClass extends ng.resource.IResourceClass<IRealmResource> {
        update(data: IRealm): IRealmResource;
        update(data: IRealm, params?: Object, success?: Function, error?: Function): IRealmResource;
        update(data: IRealm, success: Function, error?: Function): IRealmResource;

        queryMy(): ng.resource.IResourceArray<IRealmResource>;
        queryMy(params: Object): ng.resource.IResourceArray<IRealmResource>;
        queryMy(success: Function, error?: Function): ng.resource.IResourceArray<IRealmResource>;
        queryMy(params: Object, success: Function, error?: Function): ng.resource.IResourceArray<IRealmResource>;
    }

    export function RealmResourceFactory($resource: ng.resource.IResourceService, serviceBase: string) {
        return <IRealmResourceClass>$resource(
            serviceBase + 'realms/:name',
            { name: '@name' },
            {
                update: { method: 'PUT' },
                queryMy: {
                    method: 'GET', params: { name: 'my' }, isArray: true
                },
            });
    }
}
