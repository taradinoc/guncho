/// <reference path="../app.ts" />
'use strict';
interface IGlkGameportController {
    removePrompts(): void;
}

interface IGlkGameportScope extends ng.IScope {
    lineEntered: Function;
}

function GlkGameportDirective($timeout: ng.ITimeoutService, glkService: IGlkService): ng.IDirective {
    if (angular.isUndefined(GlkOte)) {
        throw new Error('GlkOte is required');
    }

    var linkFn: ng.IDirectiveLinkFn =
        ($scope: IGlkGameportScope, $element: ng.IAugmentedJQuery, $attrs: ng.IAttributes,
            $controller: any, $transclude: ng.ITranscludeFunction) => {

            glkService.registerGameport($controller);

            var prevDom = glkService.restoreDom();
            if (prevDom) {
                $element.empty();
                $element.append(prevDom);
            } else {
                glkService.initialSetup();
                var windowFrame = $element.find('> :first-child > :first-child .WindowFrame');
                var windowHeight = windowFrame.innerHeight();
                windowFrame.css({ position: 'relative', height: windowHeight + 'px' });
            }

            var unsubscribe = glkService.events.$on('lineEntered',
                (event, line) => {
                    $scope.lineEntered({ $line: line });
                });

            $scope.$on('$destroy',
                event => {
                    glkService.saveDom($element.children().first().detach());
                });
        };

    var myController = function ($scope: ng.IScope, $element: ng.IAugmentedJQuery, $attrs: ng.IAttributes) {
        this.removePrompts = () => {
            $element.find('.Style_prompt').remove();
        };
    };

    return {
        restrict: 'E',
        scope: {
            lineEntered: '&'
        },
        controller: myController,
        link: linkFn,
        templateUrl: '/app/directives/glkGameport.html'
    };
}
GlkGameportDirective.$inject = ['$timeout', 'glkService'];

app.directive('glkGameport', GlkGameportDirective);
