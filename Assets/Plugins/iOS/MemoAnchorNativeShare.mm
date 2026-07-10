#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

static UIViewController *MemoAnchorShareTopViewController(void)
{
    UIWindow *targetWindow = nil;

    if (@available(iOS 13.0, *)) {
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
            if (![scene isKindOfClass:UIWindowScene.class]) {
                continue;
            }

            UIWindowScene *windowScene = (UIWindowScene *)scene;
            if (windowScene.activationState != UISceneActivationStateForegroundActive) {
                continue;
            }

            for (UIWindow *window in windowScene.windows) {
                if (window.isKeyWindow) {
                    targetWindow = window;
                    break;
                }
            }

            if (targetWindow != nil) {
                break;
            }
        }
    }

    if (targetWindow == nil) {
        targetWindow = UIApplication.sharedApplication.delegate.window;
    }

    UIViewController *topViewController = targetWindow.rootViewController;
    while (topViewController.presentedViewController != nil) {
        topViewController = topViewController.presentedViewController;
    }

    return topViewController;
}

extern "C" void MemoAnchor_ShareText(const char *subjectValue, const char *textValue)
{
    (void)subjectValue;
    NSString *text = textValue == nullptr ? @"" : [NSString stringWithUTF8String:textValue];

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *topViewController = MemoAnchorShareTopViewController();
        if (topViewController == nil) {
            return;
        }

        UIActivityViewController *shareController = [[UIActivityViewController alloc]
            initWithActivityItems:@[text]
            applicationActivities:nil];
        UIPopoverPresentationController *popover = shareController.popoverPresentationController;
        if (popover != nil) {
            popover.sourceView = topViewController.view;
            popover.sourceRect = CGRectMake(CGRectGetMidX(topViewController.view.bounds),
                                            CGRectGetMaxY(topViewController.view.bounds),
                                            1.0,
                                            1.0);
            popover.permittedArrowDirections = 0;
        }

        [topViewController presentViewController:shareController animated:YES completion:nil];
    });
}
