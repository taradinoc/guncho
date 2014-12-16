/// <reference path="../app.ts" />
'use strict';
interface IGlkGameportScope extends ng.IScope {
    lineEntered: Function;
}

function GlkGameportDirective($timeout: ng.ITimeoutService, $log: ng.ILogService, glkService: IGlkService): ng.IDirective {
    if (angular.isUndefined(GlkOte)) {
        throw new Error('GlkOte is required');
    }

    var linkFn: ng.IDirectiveLinkFn =
        ($scope: IGlkGameportScope, $element: ng.IAugmentedJQuery, $attrs: ng.IAttributes,
            $controller: any, $transclude: ng.ITranscludeFunction) => {

            var prevDom = glkService.restoreDom();
            if (prevDom) {
                $element.empty();
                $element.append(prevDom);
                glkService.interrupt();
            } else {
                var children = $element.children();
                var xxxcount = 1;
                glkService.initialSetup(() => {
                    children.find('.Style_prompt').remove();
                    children.find('.BufferLine:empty').remove();
                });
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
                    unsubscribe();
                    glkService.saveDom($element.children().first().detach());
                });
        };

    return {
        restrict: 'E',
        scope: {
            lineEntered: '&'
        },
        link: linkFn,
        templateUrl: '/app/directives/glkGameport.html'
    };
}
GlkGameportDirective.$inject = ['$timeout', '$log', 'glkService'];

app.directive('glkGameport', GlkGameportDirective);
