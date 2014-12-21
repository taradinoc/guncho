/// <reference path="../app.ts" />
/* tslint:disable:no-string-literal */
'use strict';
interface IEditProfileControllerScope extends ng.IScope {
    character: {
        profile: IProfileResource;
        pronouns_1p: IPronouns;
        pronouns_2p: IPronouns;
        pronouns_3p: IPronouns;

        savedSuccessfully?: boolean;
        message: string;
    };
    passwords: {
        oldPassword: string;
        newPassword: string;
        confirmNewPassword: string;

        savedSuccessfully?: boolean;
        message: string;
    };

    genders: IPresetGender[];

    changePassword(): void;
    saveCharacter(): void;
    setDefaultPronouns(gender: IPresetGender): void;
}

interface IPresetGender {
    key: string;
    pronouns_1p: IPronouns;
    pronouns_2p: IPronouns;
    pronouns_3p: IPronouns;
}

interface IPronouns {
    [property: string]: string;

    /**
     * Subjective pronoun (I, he, they)
     */
    s: string;
    /**
     * Objective pronoun (me, him, them)
     */
    o: string;
    /**
     * Possessive determiner (my, his, their)
     */
    p: string;
    /**
     * Possessive pronoun (mine, his, theirs)
     */
    ps: string;
    /**
     * Reflexive pronoun (myself, himself, themselves)
     */
    r: string;
    /**
     * Is/are/am verb
     */
    v: string;
}

class EditProfileController {
    // #region Preset Genders
    public static presetGenders: IPresetGender[] = [
        {
            key: 'male',
            pronouns_1p: {
                s: 'I',
                o: 'me',
                p: 'my',
                ps: 'mine',
                r: 'myself',
                v: 'am'
            },
            pronouns_2p: {
                s: 'you',
                o: 'you',
                p: 'your',
                ps: 'yours',
                r: 'yourself',
                v: 'are'
            },
            pronouns_3p: {
                s: 'he',
                o: 'him',
                p: 'his',
                ps: 'his',
                r: 'himself',
                v: 'is'
            }
        },
        {
            key: 'female',
            pronouns_1p: {
                s: 'I',
                o: 'me',
                p: 'my',
                ps: 'mine',
                r: 'myself',
                v: 'am'
            },
            pronouns_2p: {
                s: 'you',
                o: 'you',
                p: 'your',
                ps: 'yours',
                r: 'yourself',
                v: 'are'
            },
            pronouns_3p: {
                s: 'she',
                o: 'her',
                p: 'her',
                ps: 'hers',
                r: 'herself',
                v: 'is'
            }
        },
        {
            key: 'indeterminate',
            pronouns_1p: {
                s: 'I',
                o: 'me',
                p: 'my',
                ps: 'mine',
                r: 'myself',
                v: 'am'
            },
            pronouns_2p: {
                s: 'you',
                o: 'you',
                p: 'your',
                ps: 'yours',
                r: 'yourself',
                v: 'are'
            },
            pronouns_3p: {
                s: 'they',
                o: 'them',
                p: 'their',
                ps: 'theirs',
                r: 'themselves',
                v: 'are'
            }
        },
        {
            key: 'neuter',
            pronouns_1p: {
                s: 'I',
                o: 'me',
                p: 'my',
                ps: 'mine',
                r: 'myself',
                v: 'am'
            },
            pronouns_2p: {
                s: 'you',
                o: 'you',
                p: 'your',
                ps: 'yours',
                r: 'yourself',
                v: 'are'
            },
            pronouns_3p: {
                s: 'it',
                o: 'it',
                p: 'its',
                ps: 'its',
                r: 'itself',
                v: 'is'
            }
        },
        {
            key: 'plural',
            pronouns_1p: {
                s: 'we',
                o: 'us',
                p: 'our',
                ps: 'ours',
                r: 'ourselves',
                v: 'are'
            },
            pronouns_2p: {
                s: 'you',
                o: 'you',
                p: 'your',
                ps: 'yours',
                r: 'yourselves',
                v: 'are'
            },
            pronouns_3p: {
                s: 'they',
                o: 'them',
                p: 'their',
                ps: 'theirs',
                r: 'themselves',
                v: 'are'
            }
        },
        {
            key: 'excellent-male',
            pronouns_1p: {
                s: 'we',
                o: 'us',
                p: 'our',
                ps: 'ours',
                r: 'ourselves',
                v: 'are'
            },
            pronouns_2p: {
                s: 'Your Excellency',
                o: 'Your Excellency',
                p: "Your Excellency's",
                ps: "Your Excellency's",
                r: 'yourself',
                v: 'is'
            },
            pronouns_3p: {
                s: 'His Excellency',
                o: 'His Excellency',
                p: "His Excellency's",
                ps: "His Excellency's",
                r: 'himself',
                v: 'is'
            }
        },
        {
            key: 'excellent-female',
            pronouns_1p: {
                s: 'we',
                o: 'us',
                p: 'our',
                ps: 'ours',
                r: 'ourselves',
                v: 'are'
            },
            pronouns_2p: {
                s: 'Your Excellency',
                o: 'Your Excellency',
                p: "Your Excellency's",
                ps: "Your Excellency's",
                r: 'yourself',
                v: 'is'
            },
            pronouns_3p: {
                s: 'Her Excellency',
                o: 'Her Excellency',
                p: "Her Excellency's",
                ps: "Her Excellency's",
                r: 'herself',
                v: 'is'
            }
        }
    ];
    // #endregion

    public static $inject = ['$scope', '$http', 'Profile', 'serviceBase'];
    constructor(private $scope: IEditProfileControllerScope, $http: ng.IHttpService, Profile: IProfileResourceClass, serviceBase: string) {

        var sanitizePronoun = (pronoun: string) => {
            return pronoun.replace(/[|=]/, '');
        };

        var packPronouns = (pronouns: IPronouns) => {
            var parts: string[] = [];
            for (var key in pronouns) {
                if (pronouns.hasOwnProperty(key)) {
                    parts.push(key + '=' + sanitizePronoun(pronouns[key]));
                }
            }
            return parts.join('|');
        };

        var unpackPronouns = (packed: string) => {
            var result: IPronouns = { s: null, o: null, p: null, ps: null, r: null, v: null };
            var outerParts = packed.split('|');
            angular.forEach(outerParts, part => {
                var innerParts = part.split('=', 2);
                if (innerParts.length === 2) {
                    var key = innerParts[0], value = innerParts[1];
                    if (result.hasOwnProperty(key)) {
                        result[key] = value;
                    }
                }
            });
            return result;
        };

        $scope.character = {
            profile: Profile.getMy(),
            pronouns_1p: { s: null, o: null, p: null, ps: null, r: null, v: null },
            pronouns_2p: { s: null, o: null, p: null, ps: null, r: null, v: null },
            pronouns_3p: { s: null, o: null, p: null, ps: null, r: null, v: null },
            message: ''
        };
        $scope.character.profile.$promise.then(profile => {
            $scope.character.pronouns_1p = unpackPronouns(profile.attributes['pronouns_1p']);
            $scope.character.pronouns_2p = unpackPronouns(profile.attributes['pronouns_2p']);
            $scope.character.pronouns_3p = unpackPronouns(profile.attributes['pronouns_3p']);
        });
        $scope.passwords = {
            oldPassword: '',
            newPassword: '',
            confirmNewPassword: '',
            message: ''
        };
        $scope.genders = EditProfileController.presetGenders;

        $scope.changePassword = () => {
            $http.post(serviceBase + '/account/password/my',
                {
                    oldPassword: $scope.passwords.oldPassword,
                    newPassword: $scope.passwords.newPassword,
                    confirmNewPassword: $scope.passwords.confirmNewPassword
                }).success(response => {
                    $scope.passwords.savedSuccessfully = true;
                    $scope.passwords.message = 'Password changed successfully.';
                }).error(response => {
                    $scope.passwords.savedSuccessfully = false;
                    if (response.modelState && response.modelState['']) {
                        $scope.passwords.message = response.modelState[''].join(' ');
                    } else {
                        $scope.passwords.message = 'Failed to change password.';
                    }
                });
        };

        $scope.saveCharacter = () => {
            // update pronoun attributes
            $scope.character.profile.attributes['pronouns_1p'] = packPronouns($scope.character.pronouns_1p);
            $scope.character.profile.attributes['pronouns_2p'] = packPronouns($scope.character.pronouns_2p);
            $scope.character.profile.attributes['pronouns_3p'] = packPronouns($scope.character.pronouns_3p);
            $scope.character.profile.$update().then(
                () => {
                    // success
                    $scope.character.savedSuccessfully = true;
                    $scope.character.message = 'Character saved successfully.';
                },
                () => {
                    // error
                    $scope.character.savedSuccessfully = false;
                    $scope.character.message = 'Failed to save character.';
                });
        };

        $scope.setDefaultPronouns = (genderItem: IPresetGender) => {
            $scope.character.pronouns_1p = genderItem.pronouns_1p;
            $scope.character.pronouns_2p = genderItem.pronouns_2p;
            $scope.character.pronouns_3p = genderItem.pronouns_3p;
        };
    }
}

app.controller('editProfileController', EditProfileController);
