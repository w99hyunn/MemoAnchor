#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

static NSString * const MemoAnchorBaseUrl = @"https://postcode.map.kakao.com/";
static NSString * const MemoAnchorCallbackMethod = @"OnAddressSearchResult";
static NSString * const MemoAnchorScriptHandlerName = @"MemoAnchorAddressBridge";
static NSInteger const MemoAnchorPostcodeOverlayTag = 5108001;

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

@interface MemoAnchorKakaoPostcodeOverlayView : UIView <WKScriptMessageHandler>
@property (nonatomic, copy) NSString *unityGameObjectName;
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, assign) BOOL scriptHandlerRegistered;
- (void)dismiss;
@end

@implementation MemoAnchorKakaoPostcodeOverlayView

- (instancetype)initWithUnityGameObjectName:(NSString *)unityGameObjectName
{
    self = [super initWithFrame:CGRectZero];
    if (self == nil) {
        return nil;
    }

    self.unityGameObjectName = unityGameObjectName;
    self.backgroundColor = UIColor.whiteColor;

    WKUserContentController *contentController = [[WKUserContentController alloc] init];
    [contentController addScriptMessageHandler:self name:MemoAnchorScriptHandlerName];
    self.scriptHandlerRegistered = YES;

    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    configuration.userContentController = contentController;

    self.webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:configuration];
    self.webView.backgroundColor = UIColor.whiteColor;
    self.webView.opaque = YES;
    self.webView.translatesAutoresizingMaskIntoConstraints = NO;

    UIView *topBar = [[UIView alloc] initWithFrame:CGRectZero];
    topBar.backgroundColor = UIColor.whiteColor;
    topBar.translatesAutoresizingMaskIntoConstraints = NO;

    UIButton *closeButton = [UIButton buttonWithType:UIButtonTypeSystem];
    [closeButton setTitle:@"닫기" forState:UIControlStateNormal];
    [closeButton setTitleColor:[UIColor colorWithRed:0.16 green:0.16 blue:0.16 alpha:1.0] forState:UIControlStateNormal];
    closeButton.titleLabel.font = [UIFont systemFontOfSize:16.0 weight:UIFontWeightRegular];
    closeButton.translatesAutoresizingMaskIntoConstraints = NO;
    [closeButton addTarget:self action:@selector(onTapClose) forControlEvents:UIControlEventTouchUpInside];

    [self addSubview:topBar];
    [self addSubview:self.webView];
    [topBar addSubview:closeButton];

    UILayoutGuide *safeArea = self.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [topBar.leadingAnchor constraintEqualToAnchor:self.leadingAnchor],
        [topBar.trailingAnchor constraintEqualToAnchor:self.trailingAnchor],
        [topBar.topAnchor constraintEqualToAnchor:safeArea.topAnchor],
        [topBar.heightAnchor constraintEqualToConstant:52.0],

        [closeButton.trailingAnchor constraintEqualToAnchor:topBar.trailingAnchor constant:-12.0],
        [closeButton.topAnchor constraintEqualToAnchor:topBar.topAnchor],
        [closeButton.bottomAnchor constraintEqualToAnchor:topBar.bottomAnchor],
        [closeButton.widthAnchor constraintEqualToConstant:72.0],

        [self.webView.leadingAnchor constraintEqualToAnchor:self.leadingAnchor],
        [self.webView.trailingAnchor constraintEqualToAnchor:self.trailingAnchor],
        [self.webView.topAnchor constraintEqualToAnchor:topBar.bottomAnchor],
        [self.webView.bottomAnchor constraintEqualToAnchor:self.bottomAnchor]
    ]];

    [self.webView loadHTMLString:MemoAnchorPostcodeHtml()
                         baseURL:[NSURL URLWithString:MemoAnchorBaseUrl]];

    return self;
}

- (void)dealloc
{
    [self removeScriptMessageHandler];
}

- (void)onTapClose
{
    [self dismiss];
}

- (void)dismiss
{
    [self removeScriptMessageHandler];
    [self removeFromSuperview];
}

- (void)removeScriptMessageHandler
{
    if (!self.scriptHandlerRegistered) {
        return;
    }

    [self.webView.configuration.userContentController removeScriptMessageHandlerForName:MemoAnchorScriptHandlerName];
    self.scriptHandlerRegistered = NO;
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
    [self dismiss];
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

        UIView *existingOverlay = [topViewController.view viewWithTag:MemoAnchorPostcodeOverlayTag];
        if ([existingOverlay isKindOfClass:MemoAnchorKakaoPostcodeOverlayView.class]) {
            [(MemoAnchorKakaoPostcodeOverlayView *)existingOverlay dismiss];
        } else {
            [existingOverlay removeFromSuperview];
        }

        [topViewController.view endEditing:YES];

        MemoAnchorKakaoPostcodeOverlayView *overlay = [[MemoAnchorKakaoPostcodeOverlayView alloc] initWithUnityGameObjectName:[NSString stringWithUTF8String:unityGameObjectName]];
        overlay.tag = MemoAnchorPostcodeOverlayTag;
        overlay.translatesAutoresizingMaskIntoConstraints = NO;
        [topViewController.view addSubview:overlay];
        [NSLayoutConstraint activateConstraints:@[
            [overlay.leadingAnchor constraintEqualToAnchor:topViewController.view.leadingAnchor],
            [overlay.trailingAnchor constraintEqualToAnchor:topViewController.view.trailingAnchor],
            [overlay.topAnchor constraintEqualToAnchor:topViewController.view.topAnchor],
            [overlay.bottomAnchor constraintEqualToAnchor:topViewController.view.bottomAnchor]
        ]];
    });
}
