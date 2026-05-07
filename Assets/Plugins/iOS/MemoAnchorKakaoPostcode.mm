#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

static NSString * const MemoAnchorBaseUrl = @"https://postcode.map.kakao.com/";
static NSString * const MemoAnchorCallbackMethod = @"OnAddressSearchResult";
static NSString * const MemoAnchorScriptHandlerName = @"MemoAnchorAddressBridge";

static NSString *MemoAnchorPostcodeHtml(void)
{
    return @"<!DOCTYPE html><html><head><meta charset='utf-8'>"
           "<meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>"
           "<style>html,body{margin:0;padding:0;width:100%;min-height:100%;overflow-x:hidden;overflow-y:auto;}"
           "body{box-sizing:border-box;padding-top:50px;}"
           "#wrap{width:100%;height:100%;}"
           "</style>"
           "<script src='https://t1.kakaocdn.net/mapjsapi/bundle/postcode/prod/postcode.v2.js'></script>"
           "</head><body><div id='wrap'></div><script>"
           "var wrap=document.getElementById('wrap');"
           "function resizeWrap(size){wrap.style.height=size.height+'px';}"
           "function buildAddress(data){var addr=data.userSelectedType==='R'?data.roadAddress:data.jibunAddress;"
           "var extra='';"
           "if(data.userSelectedType==='R'){"
           "if(data.bname!==''&&/[동로가]$/.test(data.bname)){extra+=data.bname;}"
           "if(data.buildingName!==''&&data.apartment==='Y'){extra+=(extra!==''?', ':'')+data.buildingName;}"
           "if(extra!==''){addr+=' ('+extra+')';}}"
           "return {zonecode:data.zonecode,address:addr,roadAddress:data.roadAddress,jibunAddress:data.jibunAddress,buildingName:data.buildingName,bname:data.bname};}"
           "window.onload=function(){new daum.Postcode({oncomplete:function(data){"
           "window.webkit.messageHandlers.MemoAnchorAddressBridge.postMessage(JSON.stringify(buildAddress(data)));"
           "},onresize:function(size){resizeWrap(size);},width:'100%',height:'100%'}).embed(wrap);};"
           "</script></body></html>";
}

@interface MemoAnchorKakaoPostcodeViewController : UIViewController <WKScriptMessageHandler>
@property (nonatomic, copy) NSString *unityGameObjectName;
@property (nonatomic, strong) WKWebView *webView;
@end

@implementation MemoAnchorKakaoPostcodeViewController

- (void)viewDidLoad
{
    [super viewDidLoad];

    self.view.backgroundColor = UIColor.whiteColor;
    self.title = @"Address";

    UIBarButtonItem *closeButton = [[UIBarButtonItem alloc] initWithBarButtonSystemItem:UIBarButtonSystemItemClose
                                                                                  target:self
                                                                                  action:@selector(onTapClose)];
    self.navigationItem.leftBarButtonItem = closeButton;

    WKUserContentController *contentController = [[WKUserContentController alloc] init];
    [contentController addScriptMessageHandler:self name:MemoAnchorScriptHandlerName];

    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    configuration.userContentController = contentController;

    self.webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:configuration];
    self.webView.backgroundColor = UIColor.whiteColor;
    self.webView.opaque = YES;
    self.webView.translatesAutoresizingMaskIntoConstraints = NO;
    [self.view addSubview:self.webView];

    UILayoutGuide *safeArea = self.view.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [self.webView.leadingAnchor constraintEqualToAnchor:safeArea.leadingAnchor],
        [self.webView.trailingAnchor constraintEqualToAnchor:safeArea.trailingAnchor],
        [self.webView.topAnchor constraintEqualToAnchor:safeArea.topAnchor],
        [self.webView.bottomAnchor constraintEqualToAnchor:safeArea.bottomAnchor]
    ]];

    [self.webView loadHTMLString:MemoAnchorPostcodeHtml()
                         baseURL:[NSURL URLWithString:MemoAnchorBaseUrl]];

    UINavigationBarAppearance *appearance = [[UINavigationBarAppearance alloc] init];
    [appearance configureWithOpaqueBackground];
    appearance.backgroundColor = UIColor.whiteColor;
    appearance.shadowColor = UIColor.clearColor;
    self.navigationController.navigationBar.standardAppearance = appearance;
    self.navigationController.navigationBar.scrollEdgeAppearance = appearance;
}

- (void)dealloc
{
    [self.webView.configuration.userContentController removeScriptMessageHandlerForName:MemoAnchorScriptHandlerName];
}

- (void)onTapClose
{
    [self dismissViewControllerAnimated:YES completion:nil];
}

- (void)userContentController:(WKUserContentController *)userContentController
      didReceiveScriptMessage:(WKScriptMessage *)message
{
    if (![message.name isEqualToString:MemoAnchorScriptHandlerName]) {
        return;
    }

    if (![message.body isKindOfClass:NSString.class]) {
        return;
    }

    NSString *payload = (NSString *)message.body;
    UnitySendMessage(self.unityGameObjectName.UTF8String, MemoAnchorCallbackMethod.UTF8String, payload.UTF8String);
    [self dismissViewControllerAnimated:YES completion:nil];
}

@end

static UIViewController *MemoAnchorTopViewController(void)
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

    UIViewController *rootViewController = targetWindow.rootViewController;
    UIViewController *topViewController = rootViewController;

    while (topViewController.presentedViewController != nil) {
        topViewController = topViewController.presentedViewController;
    }

    return topViewController;
}

extern "C" void MemoAnchor_OpenKakaoPostcodeSearch(const char *unityGameObjectName)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *topViewController = MemoAnchorTopViewController();
        if (topViewController == nil) {
            return;
        }

        MemoAnchorKakaoPostcodeViewController *viewController = [[MemoAnchorKakaoPostcodeViewController alloc] init];
        viewController.unityGameObjectName = [NSString stringWithUTF8String:unityGameObjectName];

        UINavigationController *navigationController = [[UINavigationController alloc] initWithRootViewController:viewController];
        navigationController.modalPresentationStyle = UIModalPresentationFullScreen;

        [topViewController.view endEditing:YES];
        [topViewController presentViewController:navigationController animated:YES completion:nil];
    });
}
